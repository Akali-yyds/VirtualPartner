"""Build a navigable, unfiltered evidence index; generation != quality acceptance."""
import argparse
import html
import json
from pathlib import Path
import statistics
import math


def read(path):return json.loads(path.read_text(encoding='utf8'))
def quantiles(values):
    values=sorted(values)
    if not values:return {}
    return dict(n=len(values),median=statistics.median(values),p95=values[math.ceil(len(values)*.95)-1],maximum=max(values))


def summarize(root):
    rows=[]
    for result in sorted(root.rglob('result.json')):
        record=read(result);folder=result.parent
        plays=sorted(folder.glob('playback-*.json'))
        playback=read(plays[-1]) if plays else {}
        videos=sorted(folder.glob('*.mp4'))
        pictures=sorted(folder.glob('final*.png'))
        comparison=read(folder/'audio-comparison.json') if (folder/'audio-comparison.json').exists() else None
        outcome='NOT_AN_AUDIO_COMPARISON'
        if folder.name.startswith(('concurrent-','no-capture-')):
            outcome='INTERRUPTED_OR_INCOMPLETE' if comparison is None else ('AUDIO_AND_MOTION_OBSERVED' if comparison.get('realAudio') and comparison.get('firstMotion',-1)>=0 and not comparison.get('error') and not comparison.get('ttsError') else 'FAILED_OR_BLOCKED')
        rows.append(dict(sample=str(folder.relative_to(root)),route=record.get('route'),status=record.get('status'),audioOutcome=outcome,
            input=record.get('input') or (record.get('case') or {}).get('text',''),error=record.get('error',''),
            playback=playback,videos=[str(v.relative_to(root)).replace('\\','/') for v in videos],
            pictures=[str(p.relative_to(root)).replace('\\','/') for p in pictures],
            result=str(result.relative_to(root)).replace('\\','/')))
    aggregate={}
    for name in ['semantic-suite','momask-suite','dip-suite-v2','dip-character-prefix-v2']:
        directory=root/name
        records=[read(p) for p in directory.glob('*/result.json')]
        item=dict(samples=len(records),generated=sum(r.get('status')=='GENERATED' for r in records),
                  failures=[dict(case=r.get('case',{}).get('id'),error=r.get('error')) for r in records if r.get('status')!='GENERATED'])
        if name=='semantic-suite':item['llmSeconds']=quantiles([r['timings']['semanticComplete'] for r in records if 'timings' in r])
        if name=='momask-suite':
            item['warmInferenceSeconds']=quantiles([r['timings']['inference'][1]['total'] for r in records if 'timings' in r])
            item['bvhTotalSeconds']=quantiles([r['timings']['bvhNoFootIkSeconds']+r['timings']['bvhFootIkSeconds'] for r in records if 'timings' in r])
        if name.startswith('dip'):
            for steps in [5,10]:item[str(steps)+'stepsSeconds']=quantiles([s['generationSeconds']+s['textSeconds'] for r in records if r.get('steps')==steps and 'timings' in r for s in r['timings']['segments']])
        matching=[r['playback'] for r in rows if r['sample'].startswith(name+'/') or r['sample'].startswith(name+'\\')]
        for metric in ['directionError','boneLengthChange','footTravelRelative','sourceFootTravelRelative','frameMsP50','frameMsP95','finalPositionErrorRelative','finalOrientationErrorDegrees']:
            item[metric]=quantiles([p[metric] for p in matching if metric in p and p[metric]>=0 and (not metric.startswith('final') or p.get('finalGeometrySamples',0)>0)])
        item['playbackDiagnostics']=[dict(sample=r['sample'],diagnostics=r['playback'].get('diagnostics')) for r in rows if r['sample'].startswith(name+'/') or r['sample'].startswith(name+'\\') if r['playback'].get('diagnostics')]
        resources=[s for p in directory.glob('*/resources.json') for s in read(p)]
        for metric in ['rssBytes','gpuTotalUsedMiB']:
            item['resourceSamples_'+metric]=quantiles([s[metric] for s in resources if metric in s])
        item['minimumAvailableRamBytes']=min((s['availableRamBytes'] for s in resources),default=None)
        aggregate[name]=item
    aggregate['audioComparisons']=[dict(sample=str(p.parent.relative_to(root)),**read(p)) for p in sorted(root.rglob('audio-comparison.json'))]
    (root/'summary.json').write_text(json.dumps(aggregate,ensure_ascii=False,indent=2),encoding='utf8')
    (root/'samples.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
    parts=['<!doctype html><meta charset="utf-8"><title>Motion Lab evidence</title><style>body{font:16px system-ui;background:#161a23;color:#e5e9f0;max-width:1250px;margin:30px auto}a{color:#9cd9ff}article{border-top:1px solid #485063;padding:18px 0}video{width:480px;max-width:100%}details{margin:10px 0}pre{white-space:pre-wrap}</style><h1>Motion Lab — all samples</h1><p>Generated / execution complete do not mean semantic accuracy or naturalness accepted. Earlier failed and interrupted runs remain listed. Recordings preserve measured frame timestamps. Audio is measured separately; camera recordings are silent.</p>']
    for row in rows:
        parts.append('<article><h2>'+html.escape(row['sample'])+'</h2><p>'+html.escape(str(row['status']))+' — '+html.escape(row['input'])+'</p><p>'+row['audioOutcome']+'</p>')
        parts.append('<a href="'+html.escape(row['result'],quote=True)+'">Raw result / prompt / timings</a>')
        if row['error']:parts.append('<pre>'+html.escape(row['error'])+'</pre>')
        for video in row['videos']:parts.append('<details><summary>'+html.escape(video.split('/')[-1])+'</summary><video controls preload="none" src="'+html.escape(video,quote=True)+'"></video></details>')
        parts.append('<details><summary>Latest playback measurements</summary><pre>'+html.escape(json.dumps(row['playback'],ensure_ascii=False,indent=2))+'</pre></details></article>')
    (root/'index.html').write_text('\n'.join(parts),encoding='utf8')
    print(json.dumps(aggregate,ensure_ascii=False,indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();summarize(Path(a.root))
