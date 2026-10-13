import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch
from types import SimpleNamespace
from motion_planner import SegmentParser,validate_segment,validate_hold_source,validate_subphases
from momask_playground import process_job

def segment(**kw):
    return dict(motion='Raise the left hand and keep it raised.',seconds=4,parts=['leftArm'],**kw)

class StreamingContractTests(unittest.TestCase):
    def test_fragmented_stream_and_trailing_segment(self):
        parser=SegmentParser();value=json.dumps(segment())
        self.assertEqual(parser.feed(value[:25]),[])
        self.assertEqual(parser.feed(value[25:]+'\n')[0]['frames'],80)
        self.assertEqual(parser.feed(value,True)[0]['parts'],['leftArm'])

    def test_reject_silent_truncation_and_head_without_ownership(self):
        for changes in [dict(seconds=12),dict(seconds=4.13),dict(seconds=float('nan')),dict(headTarget='left'),dict(parts=['unknown'])]:
            value=segment();value.update(changes)
            with self.subTest(changes=changes),self.assertRaises(ValueError):validate_segment(value)

    def test_hold_is_not_clamped_to_clip_length(self):
        result=validate_segment(segment(completion='hold',holdSeconds=35))
        self.assertEqual(result['holdSeconds'],35)
        self.assertEqual(result['frames'],80)

    def test_primary_parts_are_not_an_implicit_playback_mask(self):
        self.assertEqual(validate_segment(segment())['playbackScope'],'body')
        self.assertEqual(validate_segment(segment(playbackScope='parts'))['playbackScope'],'parts')
        with self.assertRaises(ValueError):validate_segment(segment(playbackScope='random'))

    def test_duration_replan_preserves_exact_budget_and_final_hold(self):
        original=segment(completion='infinite',holdSeconds=0);original['seconds']=10
        first=segment();first['seconds']=5
        last=segment(completion='infinite',holdSeconds=0);last['seconds']=5
        self.assertEqual(sum(s['seconds'] for s in validate_subphases(original,[first,last])),10)
        for changes in [dict(seconds=4),dict(parts=['rightArm']),dict(completion='idle')]:
            bad=dict(last,**changes)
            with self.assertRaises(ValueError):validate_subphases(original,[first,bad])

    def test_infinite_hold_cannot_be_a_fake_finite_timer(self):
        with self.assertRaises(ValueError):validate_segment(segment(completion='infinite',holdSeconds=9999))
        job=dict(text='左手一直保持，直到我让你放下。')
        with self.assertRaises(ValueError):validate_hold_source(validate_segment(segment(completion='hold',holdSeconds=9999)),job)
        finite=validate_segment(segment(completion='hold',holdSeconds=35,holdDurationSource='保持三十五秒'))
        validate_hold_source(finite,dict(text='左手抬起来，保持三十五秒。'))
        validate_hold_source(validate_segment(segment(completion='infinite',holdSeconds=0)),job)

    def test_invalid_later_line_does_not_discard_accepted_segment(self):
        stream=SegmentParser().iter_feed(json.dumps(segment())+'\n'+json.dumps(dict(operation='bad'))+'\n')
        self.assertEqual(next(stream)['frames'],80)
        with self.assertRaises(ValueError):next(stream)

    def test_first_generation_overlaps_planning_and_failure_is_retained(self):
        generated=threading.Event()
        def fake_stream(job,config,cancelled,emit,token):
            emit(validate_segment(segment()),.01)
            self.assertTrue(generated.wait(2),'GPU generation must start before planner finishes')
            emit(dict(operation='unsupported',intent='Cannot jump'),.02)
        def fake_generate(args,text,out):
            generated.set();return dict(status='GENERATED')
        with tempfile.TemporaryDirectory() as folder,patch('momask_playground.stream_plan',fake_stream):
            root=Path(folder);result=process_job(dict(text='test',useLlm=True,seed=11),root,root/'unused',SimpleNamespace(),fake_generate,lambda:False)
            self.assertEqual(result['status'],'FAILED')
            self.assertEqual(result['generatedMotionSeconds'],4)
            self.assertEqual(result['plannedMotionSeconds'],4)
            self.assertEqual(json.loads((root/'segments/000/ready.json').read_text())['status'],'GENERATED')
            self.assertEqual(json.loads((root/'segments/001/ready.json').read_text())['status'],'FAILED')

    def test_cancelled_inference_cannot_publish_playable_result(self):
        cancelled=[False]
        def fake_generate(args,text,out):cancelled[0]=True;return {}
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);result=process_job(dict(text='wave',useLlm=False,seconds=4,seed=11),root,root/'unused',SimpleNamespace(),fake_generate,lambda:cancelled[0])
            self.assertEqual(result['status'],'CANCELLED')
            self.assertEqual(result['generatedMotionSeconds'],0)
            self.assertEqual(json.loads((root/'segments/000/ready.json').read_text())['status'],'CANCELLED')

if __name__=='__main__':unittest.main()
