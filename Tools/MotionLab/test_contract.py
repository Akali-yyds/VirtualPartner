import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
from lab import export_positions,save,HERE


class ContractTests(unittest.TestCase):
    def test_humanml_left_is_unity_left_after_reflection(self):
        with tempfile.TemporaryDirectory() as folder:
            p=np.zeros((2,22,3),np.float32);p[:,20,0]=1;p[:,21,0]=-1
            path=Path(folder)/'clip.json';export_positions(p,path,'fixture','test',1)
            clip=json.loads(path.read_text())
            self.assertLess(clip['frames'][0]['positions'][20]['x'],0)
            self.assertGreater(clip['frames'][0]['positions'][21]['x'],0)
            np.testing.assert_equal(p[:,20,0],1)  # exporter must not mutate raw evidence

    def test_reject_nonfinite_and_wrong_skeleton(self):
        with tempfile.TemporaryDirectory() as folder:
            for positions in [np.zeros((2,21,3)),np.full((2,22,3),np.nan)]:
                with self.assertRaises(ValueError):export_positions(positions,Path(folder)/'clip.json','fixture','test',1)

    def test_atomic_write_and_all_case_expectations(self):
        cases=json.loads((HERE/'cases.json').read_text(encoding='utf8'))
        self.assertEqual(len(cases),12)
        self.assertEqual(len({case['id'] for case in cases}),12)
        self.assertTrue(all(case['text'] and case['english'] and case['expectation'] for case in cases))
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'result.json';save(path,{'status':'FAILED'});save(path,{'status':'GENERATED'})
            self.assertEqual(json.loads(path.read_text())['status'],'GENERATED')
            self.assertFalse(path.with_suffix('.json.tmp').exists())


if __name__=='__main__':unittest.main()
