"""Streaming segment contract. Pure validation is separate from network/model work."""
import json
import math
import time
import urllib.request

SYSTEM = '''Plan a standing motion performance for a chibi character. Reply ONLY as NDJSON: one complete JSON object per line, no markdown, no outer array. Each line is an independently useful chronological segment. Do not wait to emit earlier segments.
Schema: {"intent":"Chinese readable intent","motion":"English MoMask description","seconds":4.0,"parts":["leftArm","rightArm","torso","head","support"],"completion":"idle","holdSeconds":0,"headTarget":"none","operation":"motion"}.
Allowed parts: leftArm,rightArm,torso,head,support. parts identifies the intended primary action and which existing holds to take over, NOT a strict animation mask. Add playbackScope: body (default, play the generated whole-body coordination while preserving unrelated holds) or parts (only the listed groups, for an explicit restriction such as only the right arm moves). Never select parts scope merely because the main gesture uses arms. Dance/groove, weight shifts and squats require support and torso in parts and body scope unless the user explicitly restricts movement. support includes pelvis and both legs as one group. Unrelated holds persist. MoMask generates human motion: describe a person, not chibi anatomy; Unity handles Toki proportions.
seconds describes MOTION only, 2..9.8 sec in 0.2 increments. Decide duration from the instruction and experimental context; do not default every segment to four seconds. Split longer requests into meaningful sequential phases, preserving order and total requested duration. Check EVERY line before emitting: 10 seconds is INVALID, so a 10-second phase needs subphases (for example 5 seconds developing the movement and 5 seconds continuing and settling), never a single seconds:10 line. For a 30-second request the motion durations must sum to 30, not just the first few phases. Complete all requested phases; do not stop after reaching the clip length limit. Never speed up or silently truncate to satisfy a length limit. If a request cannot be faithfully represented emit {"operation":"unsupported","intent":"explanation"} after any independently useful segments.
completion: idle (normal finish), hold (time after arrival, holdSeconds positive), infinite (until release, holdSeconds MUST be zero). Use infinite directly on the arrival motion for instructions such as keep holding until told to release. NEVER approximate indefinite holding with a large finite duration. Do not generate an additional motion just to maintain an already established hold: Unity maintains it independently while unrelated parts act or the request ends. A hold segment must describe ending in the intended held pose, NOT lowering the arm afterwards. Hold times are NOT subject to MoMask clip length limits. For finite holds include holdDurationSource with the exact span from the current instruction or temporary context specifying that finite duration; never invent a finite hold time. headTarget: none,left,right,partner,down,up. Use an explicit headTarget for requested independent looking; none for ordinary generated head movement. No fingers/exact contact, walking, jumping, sitting or cloth physics. Only standing gestures, leaning, turning upper body and shallow squats.
operation release releases listed parts without generating; operation reset releases all body holds and pending performance without generation; seconds may be omitted for these operations. Distinguish stopping from posing. Never invent success for previous requests: context contains observed state/results, not a guarantee of semantic success. Preserve left/right, direction, requested counts and sequence. Counts are desired model intent, not a verified guarantee. Conversational greetings may choose a modest appropriate gesture. No dialogue or TTS.
If fixedSeconds>0, use it for each motion segment as an explicit experiment override. Context is temporary experiment-only, unrelated to daily chat.'''
PARTS={'leftArm','rightArm','torso','head','support'}


def validate_segment(value):
    if not isinstance(value,dict):raise ValueError('Segment must be an object')
    operation=value.get('operation','motion')
    if operation not in ('motion','release','reset','unsupported'):raise ValueError('Unknown operation')
    if operation=='unsupported':
        if not isinstance(value.get('intent'),str):raise ValueError('Unsupported reason required')
        return dict(operation=operation,intent=value['intent'])
    parts=value.get('parts',sorted(PARTS) if operation=='reset' else [])
    if not isinstance(parts,list) or not parts or any(p not in PARTS for p in parts):raise ValueError('Explicit valid body parts required')
    output=dict(operation=operation,parts=list(dict.fromkeys(parts)),intent=str(value.get('intent','')))
    if operation!='motion':return output
    motion=value.get('motion');seconds=value.get('seconds')
    if not isinstance(motion,str) or not motion.strip():raise ValueError('English motion required')
    if any('\u3400'<=c<='\u9fff' for c in motion):raise ValueError('MoMask motion must be English')
    if type(seconds) not in (int,float) or not math.isfinite(seconds) or not 2<=seconds<=9.8:raise ValueError('Motion outside supported 2..9.8 seconds; split phases explicitly')
    frames=round(seconds*5)*4
    if abs(frames/20-seconds)>0.001:raise ValueError('Duration must use 0.2-second model units')
    completion=value.get('completion','idle');hold=value.get('holdSeconds',0)
    scope=value.get('playbackScope','body')
    if scope not in ('body','parts'):raise ValueError('Unknown playback scope')
    if completion not in ('idle','hold','infinite'):raise ValueError('Unknown completion')
    if type(hold) not in (int,float) or not math.isfinite(hold) or hold<0 or (completion=='hold' and hold<=0):raise ValueError('Invalid hold duration')
    if completion!='hold' and hold!=0:raise ValueError('Only finite hold may specify holdSeconds')
    target=value.get('headTarget','none')
    if target not in ('none','left','right','partner','down','up'):raise ValueError('Unknown head target')
    if target!='none' and 'head' not in parts:raise ValueError('Head target requires head ownership')
    return dict(output,motion=motion.strip(),seconds=seconds,frames=frames,completion=completion,holdSeconds=hold,headTarget=target,holdDurationSource=value.get('holdDurationSource',''),playbackScope=scope)


def validate_hold_source(segment,job):
    if segment.get('completion')!='hold':return
    source=segment.get('holdDurationSource','')
    instructions=[job['text']]+[turn.get('instruction','') for turn in job.get('context',[]) if isinstance(turn,dict)]
    if not isinstance(source,str) or not source.strip() or not any(source in text for text in instructions):
        raise ValueError('Finite hold requires a quoted duration from experiment instructions; indefinite holds must use infinite')


class SegmentParser:
    def __init__(self):self.buffer=''
    def feed(self,text,final=False):
        return list(self.iter_feed(text,final))
    def iter_feed(self,text,final=False):
        for value in self.iter_objects(text,final):yield validate_segment(value)
    def iter_objects(self,text,final=False):
        self.buffer+=text
        lines=self.buffer.split('\n');self.buffer=lines.pop()
        if final and self.buffer.strip():lines.append(self.buffer);self.buffer=''
        for line in lines:
            if line.strip():yield json.loads(line)


def validate_subphases(original,values):
    segments=[validate_segment(value) for value in values]
    if len(segments)<2 or abs(sum(s.get('seconds',0) for s in segments)-original['seconds'])>.001:
        raise ValueError('Replanned subphases must preserve the requested motion duration exactly')
    for i,s in enumerate(segments):
        if s['operation']!='motion' or set(s['parts'])!=set(original['parts']):raise ValueError('Replanned subphases changed body ownership')
        expected=original.get('completion','idle') if i==len(segments)-1 else 'idle'
        if s['completion']!=expected:raise ValueError('Replanned subphases changed completion')
        if i==len(segments)-1 and s['holdSeconds']!=original.get('holdSeconds',0):raise ValueError('Replanned subphases changed hold duration')
        s['splitFromSeconds']=original['seconds'];s['splitIndex']=i
    return segments


def stream_plan(job,config_path,cancelled,on_segment,on_token):
    config=json.loads(config_path.read_text(encoding='utf-8-sig'))
    endpoint=config.get('chatCompletionsUrl') or config['baseUrl'].rstrip('/')+'/v1/chat/completions'
    user=dict(instruction=job['text'],context=job.get('context',[]),runtime=job.get('runtime',''),fixedSeconds=job.get('seconds',4) if job.get('fixedDuration') else 0)
    payload=dict(model=config['model'],temperature=.2,stream=True,messages=[dict(role='system',content=SYSTEM),dict(role='user',content=json.dumps(user,ensure_ascii=False))])
    req=urllib.request.Request(endpoint,data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+config['apiKey'],'Content-Type':'application/json'})
    start=time.perf_counter();parser=SegmentParser();raw=[];first=-1
    def accept(value):
        duration=value.get('seconds') if isinstance(value,dict) else None
        if isinstance(duration,(int,float)) and math.isfinite(duration) and 9.8<duration<=120 and value.get('operation','motion')=='motion':
            # Replan only the invalid phase, never repeat a clip or truncate it.
            # Nothing from this phase is accepted until its total is verified.
            repair=dict(payload,stream=False,messages=[dict(role='system',content=SYSTEM+'\nYou are repairing ONE overlong phase. Return ONLY its chronological subphases as NDJSON. Each seconds <=9.8. Preserve the exact sum, body ownership and final completion. For 10 seconds emit two meaningful 5-second phases. No single overlong line.'),dict(role='user',content=json.dumps(value,ensure_ascii=False))])
            repair_req=urllib.request.Request(endpoint,data=json.dumps(repair).encode(),headers={'Authorization':'Bearer '+config['apiKey'],'Content-Type':'application/json'})
            if cancelled():raise InterruptedError()
            with urllib.request.urlopen(repair_req,timeout=60) as response:content=json.load(response)['choices'][0]['message']['content']
            raw.append('\n[duration subphase replan]\n'+content+'\n[end subphase replan]\n');on_token(''.join(raw),first,time.perf_counter()-start)
            if cancelled():raise InterruptedError()
            segments=validate_subphases(value,[json.loads(line) for line in content.splitlines() if line.strip()])
        else:segments=[validate_segment(value)]
        for segment in segments:
            validate_hold_source(segment,job);on_segment(segment,time.perf_counter()-start)
    try:
        with urllib.request.urlopen(req,timeout=60) as response:
            for line in response:
                if cancelled():raise InterruptedError()
                if not line.startswith(b'data:'):continue
                value=line[5:].strip()
                if value==b'[DONE]':break
                for choice in json.loads(value).get('choices',[]):
                    chunk=choice.get('delta',{}).get('content') or ''
                    if not chunk:continue
                    if first<0:first=time.perf_counter()-start
                    raw.append(chunk);on_token(''.join(raw),first,time.perf_counter()-start)
                    for value in parser.iter_objects(chunk):accept(value)
        for value in parser.iter_objects('',True):accept(value)
    finally:
        on_token(''.join(raw),first,time.perf_counter()-start)
