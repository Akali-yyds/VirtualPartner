from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
import json,time
class Handler(BaseHTTPRequestHandler):
 def log_message(self,*args): pass
 def do_POST(self):
  request=json.loads(self.rfile.read(int(self.headers.get('Content-Length',0))))
  time.sleep(2)
  plan=json.dumps({'schemaVersion':'2.0','type':'stagePlan','metadata':{'intent':'phone_integration_test','mood':'neutral'},'stages':[{'actions':[{'type':'speech','text':'接口联调测试回复。老师，我在这里。','emotion':'neutral','speed':1.0}]}]},ensure_ascii=False)
  self.send_response(200)
  self.send_header('Content-Type','text/event-stream' if request.get('stream') else 'application/json')
  self.end_headers()
  try:
   if request.get('stream'):
    payload={'choices':[{'delta':{'content':plan},'finish_reason':None}]}
    self.wfile.write(('data: '+json.dumps(payload,ensure_ascii=False)+'\n\ndata: [DONE]\n\n').encode('utf-8'))
   else:self.wfile.write(json.dumps({'choices':[{'message':{'content':plan}}]},ensure_ascii=False).encode('utf-8'))
  except (ConnectionError,OSError):pass
ThreadingHTTPServer(('127.0.0.1',18767),Handler).serve_forever()
