"""Build a three-route player from the latest completed main-suite recordings."""
import argparse
import json
from pathlib import Path


def build(root):
    cases=json.loads((Path(__file__).parent/'cases.json').read_text(encoding='utf8'))
    samples={}
    suites=['semantic-suite','momask-suite','dip-character-prefix-v2','dip-suite-v2']
    for suite in suites:
        for folder in sorted((root/suite).iterdir()):
            if not folder.is_dir() or not (folder/'result.json').exists():continue
            videos=sorted(p for p in folder.glob('frames-*.mp4') if p.stat().st_size>0)
            if not videos:continue
            plays=sorted(folder.glob('playback-*.json'))
            report=json.loads(plays[-1].read_text(encoding='utf8')) if plays else {}
            samples[suite+'/'+folder.name]=dict(video=videos[-1].relative_to(root).as_posix(),
                result=(folder/'result.json').relative_to(root).as_posix(),diagnostics=report.get('diagnostics',[]))
    template=Path(__file__).with_suffix('.html').read_text(encoding='utf8')
    data=json.dumps(dict(cases=cases,samples=samples),ensure_ascii=False).replace('<','\\u003c')
    target=root/'review.html';target.write_text(template.replace('__REVIEW_DATA__',data),encoding='utf8')
    print(f'{target}: {len(samples)} main samples with video')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();build(Path(a.root))
