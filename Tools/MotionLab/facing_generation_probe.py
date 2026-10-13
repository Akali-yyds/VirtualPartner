"""Controlled source-generation diagnosis through an existing MotionLab worker.

No Unity playback, LLM call, source overwrite or acceptance of a replacement.
All cases, including failures, stay in separate diagnostic job directories.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time

import numpy as np
from foundation_facing import describe
from lab import save


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def run(session, source, output):
    state=read(session/'worker.json')
    if state['status']!='READY':
        raise RuntimeError('Resident worker must be READY before a diagnostic batch')
    output.mkdir(parents=True,exist_ok=False)
    fourth=read(source/'segments/003/motion.json')
    last=read(source/'segments/004/motion.json')
    cases=[('last-original-a',last['caption'],15,5,'004'),
           ('fourth-original',fourth['caption'],14,5,'003'),
           ('last-original-b',last['caption'],15,5,'004'),
           ('fourth-seed15',fourth['caption'],15,5,None),
           ('last-seed14',last['caption'],14,5,None),
           ('last-seed11',last['caption'],11,5,None),
           ('last-4seconds',last['caption'],15,4,None),
           ('last-6seconds',last['caption'],15,6,None),
           ('last-standalone-wording',last['caption'].replace('continues the light rhythmic groove','stands in place and performs a light rhythmic groove'),15,5,None)]
    rows=[]
    stamp=time.strftime('%Y%m%d-%H%M%S')
    for name,caption,seed,seconds,original in cases:
        folder=session/'jobs'/('diagnostic-facing-'+stamp+'-'+name)
        folder.mkdir(parents=True,exist_ok=False)
        request=dict(text=caption,useLlm=False,fixedDuration=False,seconds=seconds,seed=seed,context=[],runtime='isolated source-facing diagnostic; do not play or replace original')
        save(folder/'request.json',request)
        start=time.monotonic()
        while not (folder/'result.json').exists():
            if time.monotonic()-start>120:
                (folder/'cancel').touch()
                raise TimeoutError('Diagnostic job did not finish: '+str(folder))
            time.sleep(.1)
        result=read(folder/'result.json')
        row=dict(case=name,request=request,directory=str(folder),result=result)
        if result['status']=='GENERATED':
            segment=folder/'segments/000'
            motion=read(segment/'motion.json')
            positions=np.array([[[v[c] for c in 'xyz'] for v in f['positions']] for f in motion['frames']])
            features=np.load(segment/'features-0.npy')
            row.update(describe(positions))
            row['featureSha256']=hashlib.sha256((segment/'features-0.npy').read_bytes()).hexdigest()
            if original:
                reference=source/'segments'/original/'features-0.npy'
                row['originalFeatureSha256']=hashlib.sha256(reference.read_bytes()).hexdigest()
                row['featureDifferenceMax']=float(np.max(np.abs(features-np.load(reference))))
        rows.append(row)
        save(output/'results.json',dict(source=str(source),session=str(session),policy='Diagnostic counterfactuals only; originals preserved, no automatic reroll or replacement',cases=rows))
        print(json.dumps({k:v for k,v in row.items() if k in ('case','fraction','featureDifferenceMax','neckLeftToeRightToeMedianDots')},ensure_ascii=False),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--session',type=Path,required=True)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    a=parser.parse_args()
    run(a.session,a.source,a.output)
