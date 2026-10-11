"""File-queue resident MoMask worker for the isolated Unity playground; no TTS."""
import argparse
import json
import os
from pathlib import Path
import time
from types import SimpleNamespace
import urllib.request
from lab import save

SYSTEM = '''Convert the user's instruction into one English text-to-motion description for a standing character. Preserve specified left/right, direction, height, counts and sequence. Do not add extra actions. If the input is conversational rather than an action instruction, choose a simple suitable body response. No dialogue output. Do not pretend to support fingers or exact contact: mention limitations in a short Chinese note if requested. Return JSON with motion (English description) and note (Chinese; empty when no limitation). This is an isolated motion experiment with no history or memory.'''


def validate(job):
    if not isinstance(job.get('text'),str) or not job['text'].strip():raise ValueError('请输入动作指令。')
    if len(job['text'])>2000:raise ValueError('指令请限制在 2000 字符以内。')
    if job.get('seconds') not in (2,4,8):raise ValueError('时长必须是 2、4 或 8 秒。')
    if type(job.get('seed')) is not int or not 0<=job['seed']<=2147483647:raise ValueError('种子须为 0–2147483647 的整数。')
    if not job.get('useLlm') and any('\u3400'<=c<='\u9fff' for c in job['text']):raise ValueError('MoMask 使用英文编码器；中文指令请选择 LLM 模式。')


def translate(text,config_path,cancelled):
    config=json.loads(config_path.read_text(encoding='utf-8-sig'))
    endpoint=config.get('chatCompletionsUrl') or config['baseUrl'].rstrip('/')+'/v1/chat/completions'
    payload=dict(model=config['model'],temperature=.2,stream=True,response_format={'type':'json_object'},messages=[{'role':'system','content':SYSTEM},{'role':'user','content':text}])
    req=urllib.request.Request(endpoint,data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+config['apiKey'],'Content-Type':'application/json'})
    start=time.perf_counter();first=-1;chunks=[]
    with urllib.request.urlopen(req,timeout=60) as response:
        for line in response:
            if cancelled():raise InterruptedError('Cancelled')
            if not line.startswith(b'data:'):continue
            value=line[5:].strip()
            if value==b'[DONE]':break
            for choice in json.loads(value).get('choices',[]):
                chunk=choice.get('delta',{}).get('content') or ''
                if chunk:
                    if first<0:first=time.perf_counter()-start
                    chunks.append(chunk)
    elapsed=time.perf_counter()-start;raw=''.join(chunks);plan=json.loads(raw)
    if not isinstance(plan.get('motion'),str) or not plan['motion'].strip():raise ValueError('LLM 未返回有效动作描述。')
    return plan,dict(model=config['model'],system=SYSTEM,raw=raw,firstTokenSeconds=first,llmSeconds=elapsed)


def main():
    p=argparse.ArgumentParser();p.add_argument('--session',required=True);p.add_argument('--config',required=True);a=p.parse_args()
    root=Path(a.session).resolve();config=Path(a.config).resolve();root.mkdir(parents=True,exist_ok=True)
    os.environ['HF_HOME']='F:/Project/MotionExperiments/hf-cache'
    state=root/'worker.json'
    def status(value):save(state,dict(status=value,pid=os.getpid()))
    try:
        status('LOADING')
        from momask_adapter import generate
        args=SimpleNamespace(root='F:/Project/MotionExperiments',device='cuda:0',seed=11,frames=80,repeats=1,postprocess=False)
        for frames in (40,80,160):
            if (root/'stop').exists():return
            args.frames=frames;out=root/'warmup'/str(frames);out.mkdir(parents=True,exist_ok=True)
            save(out/'result.json',generate(args,'A person stands calmly with relaxed arms.',out))
        status('READY')
    except Exception as error:
        save(state,dict(status='FAILED',error=type(error).__name__));return
    while not (root/'stop').exists():
        requests=sorted(root.glob('jobs/*/request.json'))
        for request in requests:
            out=request.parent
            if (out/'result.json').exists():continue
            cancelled=lambda:(out/'cancel').exists() or (root/'stop').exists()
            record=dict(status='RUNNING',llmSeconds=-1,firstTokenSeconds=-1,momaskSeconds=-1,totalWorkerSeconds=-1)
            started=time.perf_counter()
            try:
                job=json.loads(request.read_text(encoding='utf-8-sig'));validate(job)
                if cancelled():raise InterruptedError()
                status('BUSY');save(out/'progress.json',dict(status='LLM' if job['useLlm'] else 'MOMASK'))
                motion=job['text'].strip();note=''
                if job['useLlm']:
                    plan,llm=translate(motion,config,cancelled);save(out/'llm.json',llm)
                    motion=plan['motion'];note=plan.get('note','');record.update(llmSeconds=llm['llmSeconds'],firstTokenSeconds=llm['firstTokenSeconds'])
                save(out/'prompt.json',dict(motion=motion,note=note));record.update(motion=motion,note=note)
                if cancelled():raise InterruptedError()
                save(out/'progress.json',dict(status='MOMASK'))
                args.frames=job['seconds']*20;args.seed=job['seed'];tick=time.perf_counter()
                generated=generate(args,motion,out);record['momaskSeconds']=time.perf_counter()-tick
                save(out/'generation.json',generated)
                if cancelled():raise InterruptedError()
                record.update(status='GENERATED',seconds=job['seconds'],seed=job['seed'])
            except InterruptedError:record.update(status='CANCELLED')
            except Exception as error:
                # Avoid server response/config reprs, which can contain credentials.
                record.update(status='FAILED',error=str(error) if isinstance(error,ValueError) and not isinstance(error,json.JSONDecodeError) else type(error).__name__)
            record['totalWorkerSeconds']=time.perf_counter()-started;save(out/'result.json',record);status('READY')
        time.sleep(.01)
    status('STOPPED')


if __name__=='__main__':main()
