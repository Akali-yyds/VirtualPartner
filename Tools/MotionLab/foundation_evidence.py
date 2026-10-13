"""Freeze original regressions. No private chat, service config or regenerated motion."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from lab import save

CASES={
    'backbend-last-groove':'20261012-014939-833/jobs/20261012-015041-117-a6d6d8',
    'leg-mask':'20261012-012040-951/jobs/20261012-012407-506-a9ed93',
    'height-accumulation':'20261012-013348-727/jobs/20261012-013500-007-581068',
    'missing-last-segment':'20261012-013348-727/jobs/20261012-014008-932-cab1c9',
    'raised-arms':'20261011-145349-467/jobs/20261011-145500-123-23213f',
}
ALLOWED={'motion.json','ready.json','features-0.npy','raw-0.npy','channels.json',
         'adaptation-profile.json','request.json','result.json','progress.json',
         'planner.json','planner-raw.txt','unity-events.json','interactive-timing.json'}

def freeze(root,output):
    output.mkdir(parents=True,exist_ok=False)
    manifest=[]
    for name,relative in CASES.items():
        source=root/relative
        if not source.exists():
            manifest.append(dict(case=name,source=str(source),status='MISSING'));continue
        files=[]
        for f in source.rglob('*'):
            rel=f.relative_to(source)
            if not f.is_file() or f.name not in ALLOWED or any(p.startswith(('review','inspections')) for p in rel.parts):continue
            dest=output/name/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
            digest=hashlib.sha256(f.read_bytes()).hexdigest()
            assert digest==hashlib.sha256(dest.read_bytes()).hexdigest()
            files.append(dict(file=str(rel).replace('\\','/'),sha256=digest,bytes=f.stat().st_size))
        manifest.append(dict(case=name,source=str(source),status='FROZEN',files=files))
    save(output/'index.json',dict(version=1,policy='Frozen originals. Inspections must use new directories.',cases=manifest))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--root',default='F:/Project/MotionExperiments/Evidence/Playground');a=p.parse_args()
    freeze(Path(a.root),Path(a.output))
