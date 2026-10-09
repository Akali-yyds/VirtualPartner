"""Retain all fixed cases. A single process separates model load from warm samples."""
import argparse
import json
import os
from pathlib import Path
import time
from types import SimpleNamespace
from lab import save,HERE
from resources import ResourceMonitor


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--route',choices=['momask','dip'],required=True)
    parser.add_argument('--output',required=True)
    parser.add_argument('--device',default='cuda:0')
    parser.add_argument('--prefix')
    options=parser.parse_args()
    output=Path(options.output).resolve();output.mkdir(parents=True,exist_ok=True)
    cases=json.loads((HERE/'cases.json').read_text(encoding='utf8'))
    if options.route=='momask':
        from momask_adapter import generate
        jobs=[(case,seed,10) for case in cases for seed in [11,22,33]]
    else:
        from dip_adapter import generate
        jobs=[(cases[index],11,steps) for index in [0,5,8,11] for steps in [10,5]]
    records=[]
    for case,seed,steps in jobs:
        directory=output/(case['id']+'-'+str(seed)+(('-steps'+str(steps)) if options.route=='dip' else ''))
        directory.mkdir(parents=True,exist_ok=True)
        record=dict(case=case,seed=seed,steps=steps,route=options.route,status='RUNNING')
        save(directory/'result.json',record)
        tick=time.perf_counter()
        monitor=None
        try:
            args=SimpleNamespace(root='F:/Project/MotionExperiments',device=options.device,seed=seed,steps=steps,prefix=options.prefix)
            with ResourceMonitor() as monitor:
                record.update(generate(args,case['english'],directory))
        except Exception as error:
            record.update(status='FAILED',error=str(error),errorType=type(error).__name__)
        finally:
            if monitor is not None:save(directory/'resources.json',monitor.samples)
        record['totalSeconds']=time.perf_counter()-tick
        save(directory/'result.json',record);records.append(record)
        save(output/'index.json',records)
        print(case['id'],seed,steps,record['status'],round(record['totalSeconds'],3),flush=True)


if __name__=='__main__':main()
