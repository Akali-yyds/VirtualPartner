"""Measure raw vs corrected source clips without changing any generated motion."""
import argparse
import json
from pathlib import Path
import numpy as np
from lab import save


def positions(path):
    clip=json.loads(path.read_text(encoding='utf8'))
    return np.asarray([[[p['x'],p['y'],p['z']] for p in f['positions']] for f in clip['frames']]),clip['fps']


def analyze(root):
    rows=[]
    for path in sorted(root.rglob('motion.json')):
        if not (path.parent/'result.json').exists():continue
        xyz,fps=positions(path)
        leg=np.linalg.norm(xyz[0,1]-xyz[0,4])+np.linalg.norm(xyz[0,4]-xyz[0,7])
        row=dict(sample=str(path.parent.relative_to(root)),frames=len(xyz),seconds=(len(xyz)-1)/fps,legLength=float(leg))
        feet=xyz[:,[7,8]]
        row['maxFootTravelLegLengths']=float(np.linalg.norm(feet-feet[:1],axis=-1).max()/leg)
        speeds=np.linalg.norm(np.diff(xyz,axis=0),axis=-1)*fps/leg
        row['maxJointSpeedLegLengthsPerSecond']=float(speeds.max())
        # DiP exports two 40-frame predictions. Report the join, not a subjective smoothness pass.
        if len(xyz)==80:
            row['joinDisplacementLegLengths']=float(np.linalg.norm(xyz[40]-xyz[39],axis=-1).max()/leg)
            row['joinVelocityChangeLegLengthsPerSecond']=float(np.linalg.norm((xyz[41]-xyz[40])-(xyz[40]-xyz[39]),axis=-1).max()*fps/leg)
        for variant in ['converted','foot-ik']:
            other=path.with_name(variant+'.json')
            if not other.exists():continue
            corrected,_=positions(other)
            difference=np.linalg.norm(corrected-xyz,axis=-1)/leg
            row[variant]=dict(maxCorrectionLegLengths=float(difference.max()),meanCorrectionLegLengths=float(difference.mean()),
                maxFootTravelLegLengths=float(np.linalg.norm(corrected[:,[7,8]]-corrected[:1,[7,8]],axis=-1).max()/leg))
        rows.append(row)
    save(root/'source-diagnostics.json',rows)
    print('Measured',len(rows),'clips; no source was modified')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();analyze(Path(a.root))
