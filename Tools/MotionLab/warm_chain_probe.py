"""Isolated resident MoMask + real LLM probe, with loopback TTS observation proxy."""
import argparse
import http.client
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import threading
import time
from types import SimpleNamespace
import urllib.request
from lab import save

SYSTEM = '''You are Toki, a calm and friendly virtual companion. This is an isolated latency test with no history or memory. Respond naturally in Chinese to the user's greeting in one short sentence. Also describe one suitable standing body gesture in English for a text-to-motion model. No walking, jumping, fingers or precise contact. Return only JSON with speech (Chinese spoken reply), motion (English motion description), durationSeconds (2, 4, or 8). Choose the gesture yourself. Do not mention the test in the spoken reply.'''


def main():
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--config',required=True);a=p.parse_args()
    root=Path(a.output).resolve();root.mkdir(parents=True,exist_ok=True)
    config_path=Path(a.config).resolve()
    os.environ['HF_HOME']='F:/Project/MotionExperiments/hf-cache'
    from momask_adapter import generate
    args=SimpleNamespace(root='F:/Project/MotionExperiments',device='cuda:0',seed=11,frames=40,repeats=1,postprocess=False)
    for frames in [40,80,160]:
        args.frames=frames;out=root/'warmup'/str(frames);out.mkdir(parents=True,exist_ok=True)
        save(out/'result.json',generate(args,'A person stands calmly with relaxed arms.',out))
    lock=threading.Lock();proxy_events=[]
    def mark(name,**data):
        with lock:
            proxy_events.append(dict(event=name,unix=time.time(),**data));save(root/'tts-proxy.json',proxy_events)
    class Proxy(BaseHTTPRequestHandler):
        def log_message(self,*a):pass
        def do_GET(self):self.forward()
        def do_POST(self):self.forward()
        def forward(self):
            body=self.rfile.read(int(self.headers.get('Content-Length',0)))
            conn=http.client.HTTPConnection('127.0.0.1',8765,timeout=180)
            measured=self.path=='/tts'
            try:
                if measured:mark('tts_http_request',request=json.loads(body))
                conn.request(self.command,self.path,body,{'Content-Type':'application/json'})
                res=conn.getresponse()
                if measured:mark('tts_http_headers',status=res.status)
                self.send_response(res.status)
                for key,value in res.getheaders():
                    if key.lower() not in ('transfer-encoding','connection','content-length'):self.send_header(key,value)
                self.send_header('Connection','close');self.end_headers();self.close_connection=True
                first=True;count=0
                while True:
                    chunk=res.read1(8192)
                    if not chunk:break
                    if first and measured:mark('tts_first_body_bytes')
                    first=False;count+=len(chunk);self.wfile.write(chunk);self.wfile.flush()
                if measured:mark('tts_body_complete',bytes=count)
            except Exception as e:
                mark('proxy_error',error=type(e).__name__);self.close_connection=True
            finally:conn.close()
    server=ThreadingHTTPServer(('127.0.0.1',8766),Proxy)
    threading.Thread(target=server.serve_forever,daemon=True).start()
    save(root/'ready.json',dict(unix=time.time(),warmedFrames=[40,80,160],pid=os.getpid()))
    print('READY: resident MoMask and TTS observation proxy',flush=True)
    while not (root/'request.json').exists():
        if (root/'stop').exists():server.shutdown();return
        time.sleep(.005)
    job=json.loads((root/'request.json').read_text(encoding='utf-8-sig'));out=root/'sample';out.mkdir(exist_ok=True)
    events=[]
    def event(name):events.append(dict(event=name,unix=time.time()));save(root/'worker-timeline.json',events)
    try:
        event('worker_received')
        config=json.loads(config_path.read_text(encoding='utf-8-sig'))
        endpoint=config.get('chatCompletionsUrl') or config['baseUrl'].rstrip('/')+'/v1/chat/completions'
        payload=dict(model=config['model'],stream=True,temperature=.2,response_format={'type':'json_object'},messages=[{'role':'system','content':SYSTEM},{'role':'user','content':job['text']}])
        save(root/'llm-input.json',dict(model=config['model'],messages=payload['messages'],temperature=.2))
        request=urllib.request.Request(endpoint,data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+config['apiKey'],'Content-Type':'application/json'})
        event('llm_request');chunks=[];first=True
        with urllib.request.urlopen(request,timeout=90) as response:
            for line in response:
                if not line.startswith(b'data:'):continue
                data=line[5:].strip()
                if data==b'[DONE]':break
                for choice in json.loads(data).get('choices',[]):
                    chunk=choice.get('delta',{}).get('content') or ''
                    if chunk:
                        if first:event('llm_first_token');first=False
                        chunks.append(chunk)
        event('llm_complete');raw=''.join(chunks);save(root/'llm-output.json',dict(raw=raw))
        plan=json.loads(raw)
        if not isinstance(plan.get('speech'),str) or not plan['speech'].strip() or not isinstance(plan.get('motion'),str) or plan.get('durationSeconds') not in (2,4,8):raise ValueError('Invalid LLM plan')
        save(root/'plan.json',plan);event('plan_published')
        args.frames=int(plan['durationSeconds'])*20
        event('momask_start');result=generate(args,plan['motion'],out);event('momask_complete')
        save(out/'result.json',result);save(root/'worker-complete.json',dict(status='COMPLETE'))
    except Exception as e:
        save(root/'worker-complete.json',dict(status='FAILED',error=type(e).__name__))
    while not (root/'stop').exists():time.sleep(.2)
    server.shutdown()


if __name__=='__main__':main()
