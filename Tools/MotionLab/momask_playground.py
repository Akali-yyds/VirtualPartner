"""Resident MoMask worker. Streaming planning and serial GPU generation overlap."""
import argparse
import json
import os
from pathlib import Path
import queue
import threading
import time
from types import SimpleNamespace
from lab import save
from motion_planner import SYSTEM, stream_plan, validate_segment


def validate(job):
    if not isinstance(job.get('text'),str) or not job['text'].strip():raise ValueError('请输入动作指令。')
    if len(job['text'])>2000:raise ValueError('指令请限制在 2000 字符以内。')
    if job.get('fixedDuration',not job.get('useLlm')) and job.get('seconds') not in (2,4,8):raise ValueError('固定对照时长必须是 2、4 或 8 秒。')
    if type(job.get('seed')) is not int or not 0<=job['seed']<=2147483647:raise ValueError('种子须为 0–2147483647 的整数。')
    if not job.get('useLlm') and any('\u3400'<=c<='\u9fff' for c in job['text']):raise ValueError('中文指令请选择 LLM 模式。')


def process_job(job,out,config,args,generate,cancelled):
    started=time.perf_counter();tasks=queue.Queue();done=threading.Event();failure=[]
    timing=dict(firstTokenSeconds=-1,llmSeconds=-1,firstSegmentSeconds=-1,momaskSeconds=0)
    count=[0];completed=[];planned_seconds=[0.0];generated_seconds=0.0
    def progress():
        save(out/'progress.json',dict(plannedMotionSeconds=planned_seconds[0],generatedMotionSeconds=generated_seconds,acceptedSegments=count[0],generatedSegments=sum(x['status']=='GENERATED' and x.get('operation')=='motion' for x in completed)))
    save(out/'planner-input.json',dict(system=SYSTEM,input=job))
    def segment(value,elapsed):
        if cancelled():raise InterruptedError()
        if count[0]>=64:raise ValueError('实验最多 64 段，剩余规划未接受')
        i=count[0];count[0]+=1
        if timing['firstSegmentSeconds']<0:timing['firstSegmentSeconds']=elapsed
        folder=out/'segments'/f'{i:03d}';folder.mkdir(parents=True,exist_ok=True)
        save(folder/'plan.json',dict(value,index=i,acceptedSeconds=elapsed))
        if value.get('operation')=='motion':planned_seconds[0]+=value['seconds']
        tasks.put((i,value,folder))
    def token(raw,first,elapsed):
        timing.update(firstTokenSeconds=first,llmSeconds=elapsed)
        save(out/'llm.json',dict(raw=raw,**timing))
    def planner():
        try:
            if job['useLlm']:stream_plan(job,config,cancelled,segment,token)
            else:segment(validate_segment(dict(motion=job['text'],seconds=job.get('seconds',4),parts=['leftArm','rightArm','torso','head','support'])),0)
        except InterruptedError:pass
        except Exception as exc:failure.append(str(exc) if isinstance(exc,ValueError) else type(exc).__name__)
        finally:done.set()
    thread=threading.Thread(target=planner,daemon=True);thread.start()
    while not done.is_set() or not tasks.empty():
        if cancelled():break
        try:i,plan,folder=tasks.get(timeout=.03)
        except queue.Empty:continue
        if plan['operation']=='unsupported':
            failure.append(plan['intent']);save(folder/'ready.json',dict(status='FAILED',error=plan['intent'],index=i));continue
        item=dict(plan,index=i,status='GENERATED',momaskSeconds=0,firstSegmentSeconds=timing['firstSegmentSeconds'])
        try:
            if plan['operation']=='motion':
                args.frames=plan['frames'];args.seed=(job['seed']+i)%2147483648;tick=time.perf_counter()
                save(folder/'generation.json',generate(args,plan['motion'],folder))
                item['momaskSeconds']=time.perf_counter()-tick;timing['momaskSeconds']+=item['momaskSeconds']
            item.update(generatedSeconds=time.perf_counter()-started,llmSeconds=timing['llmSeconds'])
            if cancelled():item['status']='CANCELLED'
            save(folder/'ready.json',item);completed.append(item)
            if item['status']=='GENERATED' and plan['operation']=='motion':generated_seconds+=plan['seconds']
            progress()
        except Exception as exc:
            item.update(status='FAILED',error=type(exc).__name__);save(folder/'ready.json',item);failure.append('segment '+str(i)+': '+type(exc).__name__)
    # Late planner output cannot enqueue playable results for a cancelled request.
    status='CANCELLED' if cancelled() else 'FAILED' if failure else 'GENERATED'
    if not completed and status=='GENERATED':status='FAILED';failure.append('No executable segment returned')
    record=dict(status=status,error='; '.join(failure),segments=len(completed),plannedMotionSeconds=planned_seconds[0],generatedMotionSeconds=generated_seconds,totalWorkerSeconds=time.perf_counter()-started,**timing)
    save(out/'result.json',record)
    return record


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--session',required=True);parser.add_argument('--config',required=True);a=parser.parse_args()
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
    except Exception as exc:save(state,dict(status='FAILED',error=type(exc).__name__));return
    while not (root/'stop').exists():
        for request in sorted(root.glob('jobs/*/request.json')):
            out=request.parent
            if (out/'result.json').exists():continue
            cancelled=lambda out=out:(out/'cancel').exists() or (root/'stop').exists()
            try:
                job=json.loads(request.read_text(encoding='utf-8-sig'));validate(job)
                if cancelled():save(out/'result.json',dict(status='CANCELLED'));continue
                status('BUSY');process_job(job,out,config,args,generate,cancelled)
            except Exception as exc:save(out/'result.json',dict(status='FAILED',error=str(exc) if isinstance(exc,ValueError) and not isinstance(exc,json.JSONDecodeError) else type(exc).__name__))
            status('READY')
        time.sleep(.01)
    status('STOPPED')


if __name__=='__main__':main()
