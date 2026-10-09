"""Copy all evidence, including failures, outside Unity Library and hash it."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def package(source,destination):
    source=source.resolve();destination=destination.resolve()
    if source==destination or source in destination.parents:
        raise ValueError('Evidence destination must be outside source')
    destination.mkdir(parents=True,exist_ok=True)
    manifest=[]
    for path in sorted(source.rglob('*')):
        if not path.is_file():continue
        relative=path.relative_to(source)
        # Keep timestamped frames too: interrupted runs may have no encodable video.
        target=destination/relative;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(path,target)
        manifest.append(dict(path=relative.as_posix(),bytes=target.stat().st_size,sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    (destination/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    print('Preserved',len(manifest),'files at',destination)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('destination');a=p.parse_args();package(Path(a.source),Path(a.destination))
