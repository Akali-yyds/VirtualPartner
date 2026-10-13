"""Immutable decode evidence and deterministic coordinate fixtures for gate 1."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import numpy as np
from lab import export_positions,save,PARENTS


def direction_metrics(p):
    def unit(v):return v/np.maximum(np.linalg.norm(v,axis=-1,keepdims=True),1e-8)
    forward=unit(np.cross(p[:,2]-p[:,1],p[:,9]-p[:,0]))
    result={}
    for name,v in [('neck',p[:,15]-p[:,12]),('leftToe',p[:,10]-p[:,7]),('rightToe',p[:,11]-p[:,8])]:
        dots=np.sum(unit(v)*forward,axis=-1)
        result[name]=dict(median=float(np.median(dots)),opposedFrames=np.flatnonzero(dots<-.25).tolist())
    return result


def facing_conflicts(p):
    """Multi-cue disagreement, NOT ground-truth facing or an automatic flip."""
    def unit(v):return v/np.maximum(np.linalg.norm(v,axis=-1,keepdims=True),1e-8)
    forward=unit(np.cross(p[:,2]-p[:,1],p[:,9]-p[:,0]))
    dots=np.stack([np.sum(unit(p[:,tip]-p[:,base])*forward,axis=-1) for base,tip in [(12,15),(7,10),(8,11)]],axis=-1)
    conflict=np.all(dots<-.25,axis=1)
    return dict(opposedNeckAndBothFeetFrames=np.flatnonzero(conflict).tolist(),fraction=float(conflict.mean()),neckLeftToeRightToeMedianDots=np.median(dots,axis=0).tolist(),policy='Warn only. Preserve valid source, no resampling, axis flipping or inferred face ground truth.')


def audit_sample(folder,out,repo):
    import torch
    from common.skeleton import Skeleton
    from utils.paramUtil import t2m_raw_offsets,t2m_kinematic_chain
    from utils.motion_process import recover_from_ric,recover_from_rot
    out.mkdir(parents=True,exist_ok=False)
    feature=folder/'features-0.npy';data=torch.tensor(np.load(feature),dtype=torch.float32)
    ric=recover_from_ric(data,22)
    sk=Skeleton(torch.from_numpy(t2m_raw_offsets),t2m_kinematic_chain,'cpu')
    sk.get_offsets_joints(ric[0]);fk=recover_from_rot(data,22,sk)
    # Isolate length mismatch from orientation mismatch, without changing the
    # official channels or declaring either reconstruction ground truth.
    from utils.motion_process import recover_root_rot_pos
    from common.quaternion import quaternion_to_cont6d
    root_q,root_p=recover_root_rot_pos(data)
    params=torch.cat([quaternion_to_cont6d(root_q),data[:,67:193]],dim=-1).reshape(-1,22,6)
    fk_variable=sk.forward_kinematics_cont6d(params,root_p,skel_joints=ric)
    variable_errors=torch.linalg.norm(fk_variable-ric,dim=-1).numpy()
    quats=sk.inverse_kinematics_np(ric.numpy(),[2,1,17,16],smooth_forward=False)
    reconstructed=sk.forward_kinematics_cont6d(quaternion_to_cont6d(torch.tensor(quats,dtype=torch.float32)),ric[:,0],skel_joints=ric)
    ik_error=float(torch.linalg.norm(reconstructed-ric,dim=-1).max())
    relative=(fk-fk[:,0:1])-(ric-ric[:,0:1]);errors=torch.linalg.norm(relative,dim=-1).numpy()
    metadata=json.loads((folder/'motion.json').read_text(encoding='utf-8'))
    save(out/'provenance.json',dict(source=str(folder),featuresSha256=hashlib.sha256(feature.read_bytes()).hexdigest(),motionSha256=hashlib.sha256((folder/'motion.json').read_bytes()).hexdigest(),fkLengths='source first frame, diagnostic only',coordinateVersion='humanml-x-reflection-v1'))
    for label,p in [('ric',ric.numpy()),('fk',fk.numpy())]:
        export_positions(p,out/(label+'.json'),'official-'+label,metadata.get('caption',''),metadata.get('seed',0),features=data.numpy())
    p=ric.numpy().copy();p[:,:,0]*=-1
    exported=np.array([[[v[c] for c in 'xyz'] for v in fr['positions']] for fr in metadata['frames']])
    fk_unity=fk.numpy().copy();fk_unity[:,:,0]*=-1
    lengths=np.linalg.norm(p[:,1:]-p[:,np.array(PARENTS[1:])],axis=-1)
    report=dict(positionRoundtripMax=float(np.abs(exported-p).max()),ricVsFkMedian=float(np.median(errors)),ricVsFkMax=float(errors.max()),perJointMedian=np.median(errors,axis=0).tolist(),boneLengthRange=(lengths.max(axis=0)-lengths.min(axis=0)).tolist(),ricDirection=direction_metrics(p),fkDirection=direction_metrics(fk_unity),diagnosis='FK lengths fitted to RIC first frame. RIC does not enforce constant length. Direction cues do not identify anatomical face or justify a facing flip.')
    report.update(fkWithPerFrameLengthsMedian=float(np.median(variable_errors)),fkWithPerFrameLengthsMax=float(variable_errors.max()),positionIkFkRoundtripMax=ik_error)
    report['facingConflicts']=facing_conflicts(p)
    save(out/'audit.json',report);return report


def fixtures(out):
    # Explicit labelled canonical source pose: +X left, +Z front in HumanML.
    p=np.array([[0,1,0],[.1,1,0],[-.1,1,0],[0,1.12,0],[.1,.55,.025],[-.1,.55,.025],[0,1.24,0],[.1,.1,0],[-.1,.1,0],[0,1.38,0],[.1,.06,.14],[-.1,.06,.14],[0,1.53,0],[.09,1.43,0],[-.09,1.43,0],[0,1.69,.12],[.2,1.43,0],[-.2,1.43,0],[.4,1.43,0],[-.4,1.43,0],[.6,1.43,0],[-.6,1.43,0]],dtype=float)
    calibration=json.loads((Path(__file__).resolve().parents[2]/'VirtualPartner/Assets/VirtualPartner/Resources/TokiFoundationCalibration.json').read_text(encoding='utf-8'))
    neck=np.array([calibration['sourceNeckBinding'][axis] for axis in 'xyz'])
    neck[0]*=-1  # fixtures are authored in the original HumanML frame
    for kind in ['neutral','left_arm','right_arm','forward','yaw_left','lean_forward','root_translation','shallow_squat','neck_reference','neck_nod']:
        frames=[];rotations=[]
        for i in range(80):
            t=i/79;s=np.sin(t*np.pi)**2;v=p.copy();yaw=0
            if kind in ('neck_reference','neck_nod'):
                angle=np.radians(20)*s if kind=='neck_nod' else 0
                rot=np.array([[1,0,0],[0,np.cos(angle),-np.sin(angle)],[0,np.sin(angle),np.cos(angle)]])
                v[15]=v[12]+rot@neck*.2
            if kind in ('left_arm','right_arm','forward'):
                side=1 if kind=='right_arm' else 0
                # Anatomical left/right lift upwards; forward moves BOTH arms
                # to +Z. Distinct fixtures expose side and sign mistakes.
                for side in ([0,1] if kind=='forward' else [side]):
                    a=np.radians(60)*s*(1 if side==0 else -1);c=np.cos(a);sn=np.sin(a)
                    rot=np.array([[c,0,-sn],[0,1,0],[sn,0,c]]) if kind=='forward' else np.array([[c,-sn,0],[sn,c,0],[0,0,1]])
                    for k in [18+side,20+side]:v[k]=v[16+side]+rot@(p[k]-p[16+side])
            if kind=='yaw_left':
                yaw=np.pi*.5*s;c=np.cos(yaw);sn=np.sin(yaw);rot=np.array([[c,0,sn],[0,1,0],[-sn,0,c]]);v=(v-p[0])@rot.T+p[0]
            if kind=='lean_forward':
                a=.2*s;rot=np.array([[1,0,0],[0,np.cos(a),-np.sin(a)],[0,np.sin(a),np.cos(a)]])
                for k in [3,6,9,12,13,14,15,16,17,18,19,20,21]:v[k]=p[0]+rot@(p[k]-p[0])
            if kind=='root_translation':v+=np.array([0,0,.2*s])
            if kind=='shallow_squat':
                # Position-only diagnostic, solve each knee with fixed feet and bone lengths.
                v[[0,1,2,3,6,9,12,13,14,15,16,17,18,19,20,21],1]-=.12*s
                for hip,knee,foot in [(1,4,7),(2,5,8)]:
                    a=v[hip];b=v[foot];l1=np.linalg.norm(p[knee]-p[hip]);l2=np.linalg.norm(p[foot]-p[knee]);d=np.linalg.norm(b-a);along=(l1*l1-l2*l2+d*d)/(2*d)
                    v[knee]=a+(b-a)/d*along+np.array([0,0,np.sqrt(max(0,l1*l1-along*along))])
            frames.append(v);rotations.append(dict(x=0,y=float(-np.sin(yaw/2)),z=0,w=float(np.cos(yaw/2))))
        folder=out/kind;folder.mkdir(parents=True,exist_ok=True)
        export_positions(np.array(frames),folder/'motion.json','fixture',kind,0)
        value=json.loads((folder/'motion.json').read_text());value.update(sourceFormat='canonical-fixture-v1',fixture=True,hasRootOrientation=True)
        for frame,q in zip(value['frames'],rotations):frame['rootRotation']=q
        save(folder/'motion.json',value)
        save(folder/'ready.json',dict(status='GENERATED',operation='motion',parts=['leftArm','rightArm','torso','head','support'],playbackScope='body',intent='CALIBRATION ONLY: '+kind,seconds=4,completion='idle'))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);parser.add_argument('--sample',action='append',default=[]);a=parser.parse_args()
    root=Path(a.output);root.mkdir(parents=True,exist_ok=True)
    repo=Path('F:/Project/MotionExperiments/momask');sys.path.insert(0,str(repo))
    fixtures(root/'fixtures')
    reports={}
    for i,source in enumerate(a.sample):reports[str(source)]=audit_sample(Path(source),root/'decode'/str(i),repo)
    save(root/'decode-summary.json',reports)
    print(json.dumps({k:{a:b for a,b in v.items() if a in ['positionRoundtripMax','ricVsFkMedian','ricVsFkMax']} for k,v in reports.items()},indent=2))
