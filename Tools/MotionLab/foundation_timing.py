"""Warm CPU decode/coordinate/export measurements. Never counts these as LLM latency."""
import argparse
import json
from pathlib import Path
import sys
import tempfile
import time
import numpy as np
from lab import export_positions,save
from motion_coordinates import root_quaternions

def run(sample,output):
    sys.path.insert(0,'F:/Project/MotionExperiments/momask')
    import torch
    from utils.motion_process import recover_from_ric
    data=np.load(sample/'features-0.npy');tensor=torch.tensor(data,dtype=torch.float32)
    values=[]
    with tempfile.TemporaryDirectory() as folder:
        for repeat in range(31):
            start=time.perf_counter();p=recover_from_ric(tensor,22).numpy();decoded=time.perf_counter()
            root_quaternions(data);v=p.copy();v[:,:,0]*=-1;converted=time.perf_counter()
            export_positions(p,Path(folder)/'motion.json','timing','fixed saved sample',0,features=data);exported=time.perf_counter()
            if repeat:values.append(dict(decodeMs=(decoded-start)*1000,coordinateMs=(converted-decoded)*1000,exportWithDiskMs=(exported-converted)*1000))
    stats={k:dict(n=len(values),p50=float(np.median([r[k] for r in values])),p95=float(np.percentile([r[k] for r in values],95)),maximum=max(r[k] for r in values)) for k in values[0]}
    save(output,dict(source=str(sample),warmupExcluded=1,generatedFrames=len(data),generationIncluded=False,stats=stats,measurements=values))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--sample',required=True);p.add_argument('--output',required=True);a=p.parse_args();run(Path(a.sample),Path(a.output))
