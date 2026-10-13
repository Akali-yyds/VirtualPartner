"""Independent contract checks against official recovery and labelled fixtures."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
import numpy as np
from motion_coordinates import root_quaternions, reflect_rotation
from foundation_audit import fixtures, facing_conflicts


class CoordinateTests(unittest.TestCase):
    def test_neck_reference_is_from_official_binding_not_generated_mean(self):
        sys.path.insert(0, 'F:/Project/MotionExperiments/momask')
        from visualization import BVH_mod as BVH
        import hashlib
        path=Path('F:/Project/MotionExperiments/momask/visualization/data/template.bvh')
        template=BVH.load(str(path),need_quater=True)
        positions=template.offsets.copy()
        for i,parent in enumerate(template.parents):
            if parent>=0:positions[i]+=positions[parent]
        p=positions[[0,1,5,9,2,6,10,3,7,11,4,8,12,14,18,13,15,19,16,20,17,21]]
        p[:,0]*=-1
        up=p[12]-p[9];up/=np.linalg.norm(up)
        forward=np.cross(p[17]-p[16],up);forward/=np.linalg.norm(forward)
        right=np.cross(up,forward)
        reference=np.stack([right,up,forward],axis=1).T@(p[15]-p[12]);reference/=np.linalg.norm(reference)
        calibration=json.loads((Path(__file__).resolve().parents[2]/'VirtualPartner/Assets/VirtualPartner/Resources/TokiFoundationCalibration.json').read_text())
        self.assertEqual(calibration['sourceNeckTemplateSha256'],hashlib.sha256(path.read_bytes()).hexdigest())
        np.testing.assert_allclose(reference,[calibration['sourceNeckBinding'][a] for a in 'xyz'],atol=1e-7)

    def test_official_root_inverse_and_basis(self):
        sys.path.insert(0, 'F:/Project/MotionExperiments/momask')
        import torch
        from utils.motion_process import recover_root_rot_pos
        from common.quaternion import qinv, qrot
        data=np.zeros((8,263),dtype=np.float32)
        data[:,0]=[.2,-.1,.3,-.4,.7,0,-.8,.1]
        official,_=recover_root_rot_pos(torch.tensor(data))
        local=torch.tensor([[0.,0.,1.]]*8)
        forward=qrot(qinv(official),local).numpy();forward[:,0]*=-1
        q=root_quaternions(data);y,w=q[:,1],q[:,3]
        unity_forward=np.stack([2*y*w, np.zeros(8), 1-2*y*y],axis=-1)
        np.testing.assert_allclose(forward,unity_forward,atol=2e-7)
        np.testing.assert_allclose(np.linalg.norm(q,axis=1),1,atol=1e-7)

    def test_basis_roundtrip(self):
        a=.7;c,s=np.cos(a),np.sin(a)
        rotation=np.array([[c,0,s],[0,1,0],[-s,0,c]])
        np.testing.assert_allclose(reflect_rotation(reflect_rotation(rotation)),rotation)
        np.testing.assert_allclose(reflect_rotation(rotation)@[-1,0,0],[-c,0,-s])

    def test_facing_diagnostic_is_not_a_global_facing_rule(self):
        with tempfile.TemporaryDirectory() as folder:
            fixtures(Path(folder))
            data=json.loads((Path(folder)/'neutral/motion.json').read_text())
            neutral=np.array([[[v[c] for c in 'xyz'] for v in f['positions']] for f in data['frames']])
            bad=neutral.copy()
            for base,tip in [(12,15),(7,10),(8,11)]:
                bad[:,tip,2]=2*bad[:,base,2]-bad[:,tip,2]
            # Every whole-clip yaw, including 180 degrees, preserves the
            # contradiction. Turning the entire clip cannot repair it.
            for a in [0, .7, np.pi, 4.2]:
                c,s=np.cos(a),np.sin(a)
                rotation=np.array([[c,0,s],[0,1,0],[-s,0,c]])
                self.assertEqual(facing_conflicts(neutral@rotation.T)['fraction'],0)
                self.assertEqual(facing_conflicts(bad@rotation.T)['fraction'],1)
            # A look-back alone must not label the whole body reversed.
            head_only=neutral.copy()
            head_only[:,15,2]=2*head_only[:,12,2]-head_only[:,15,2]
            self.assertEqual(facing_conflicts(head_only)['fraction'],0)

    def test_fixtures(self):
        with tempfile.TemporaryDirectory() as folder:
            fixtures(Path(folder))
            samples={}
            for path in Path(folder).glob('*/motion.json'):
                data=json.loads(path.read_text())
                p=np.array([[[v[c] for c in 'xyz'] for v in f['positions']] for f in data['frames']])
                samples[path.parent.name]=p
                self.assertEqual(len(p)/data['fps'],4)
                lengths=np.linalg.norm(p[:,1:]-p[:,data['parents'][1:]],axis=-1)
                self.assertLess(float(np.ptp(lengths,axis=0).max()),1e-6,path.parent.name)
                np.testing.assert_allclose(p[0],p[-1],atol=1e-7)
            self.assertLess(samples['neutral'][0,20,0],samples['neutral'][0,21,0])
            self.assertGreater(samples['left_arm'][40,20,1],samples['neutral'][40,20,1]+.3)
            np.testing.assert_allclose(samples['left_arm'][:,21],samples['neutral'][:,21])
            self.assertGreater(samples['right_arm'][40,21,1],samples['neutral'][40,21,1]+.3)
            self.assertTrue((samples['forward'][40,[20,21],2]>.3).all())
            self.assertGreater(samples['lean_forward'][40,15,2],samples['neutral'][40,15,2])
            np.testing.assert_allclose(samples['shallow_squat'][:,[7,8]],samples['neutral'][:,[7,8]])
            self.assertLess(samples['shallow_squat'][40,0,1],.89)
            self.assertEqual(facing_conflicts(samples['neutral'])['fraction'],0)
            conflict=samples['neutral'].copy()
            for base,tip in [(12,15),(7,10),(8,11)]:conflict[:,tip,2]=conflict[:,base,2]-(conflict[:,tip,2]-conflict[:,base,2])
            self.assertEqual(facing_conflicts(conflict)['fraction'],1)


if __name__=='__main__': unittest.main()
