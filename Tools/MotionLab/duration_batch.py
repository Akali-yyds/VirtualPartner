"""Nine duration/complexity cases per route in one warm resident process."""
import argparse
import json
import os
from pathlib import Path
import time
from types import SimpleNamespace
from lab import HERE,save
from resources import ResourceMonitor


def main():
    p=argparse.ArgumentParser();p.add_argument('--route',choices=['momask','dip'],required=True);p.add_argument('--output',required=True);p.add_argument('--prefix',required=True);a=p.parse_args()
    root=Path(a.output).resolve();root.mkdir(parents=True,exist_ok=True)
    os.environ['HF_HOME']='F:/Project/MotionExperiments/hf-cache';os.environ['HF_HUB_DISABLE_XET']='1'
    if a.route=='momask':from momask_adapter import generate
    else:from dip_adapter import generate
    cases=json.loads((HERE/'duration_cases.json').read_text(encoding='utf8'))
    args=SimpleNamespace(root='F:/Project/MotionExperiments',device='cuda:0',seed=11,steps=10,prefix=str(Path(a.prefix).resolve()),frames=40,repeats=1,postprocess=False)
    # Warm all three tensor shapes before measuring any reviewed case. Keep outputs;
    # these are labeled warmups and never presented as the requested samples.
    for frames in [40,80,160]:
        args.frames=frames;folder=root/'warmup'/a.route/str(frames);folder.mkdir(parents=True,exist_ok=True)
        start=time.perf_counter();record=generate(args,'A person stands calmly in place with relaxed arms.',folder)
        save(folder/'warmup.json',dict(**record,excludedFromBenchmark=True,wallSeconds=time.perf_counter()-start))
        print('WARMED',a.route,frames,flush=True)
    index=[]
    for case in cases:
        args.frames=case['seconds']*20;folder=root/a.route/case['id'];folder.mkdir(parents=True,exist_ok=True)
        record=dict(route=a.route,case=case,input=case['english'],seed=11,status='RUNNING',coldStartExcluded=True,
                    mode='resident process; one accepted input, one generated output; no reroll',steps=10 if a.route=='dip' else 18)
        save(folder/'result.json',record)
        monitor=ResourceMonitor()
        try:
            with monitor:
                start=time.perf_counter();record.update(generate(args,case['english'],folder));record['warmRequestSeconds']=time.perf_counter()-start
        except Exception as error:record.update(status='FAILED',error=str(error),errorType=type(error).__name__)
        save(folder/'resources.json',monitor.samples);save(folder/'result.json',record);index.append(record);save(root/(a.route+'-index.json'),index)
        print(case['id'],record['status'],round(record.get('warmRequestSeconds',-1),3),flush=True)


if __name__=='__main__':main()
