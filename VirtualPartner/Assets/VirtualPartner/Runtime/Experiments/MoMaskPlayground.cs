#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    // Temporary Game View UI. Only attached to MotionLab in Editor Play Mode.
    public sealed class MoMaskPlayground : MonoBehaviour
    {
        public string prompt="站在原地，右手抬到肩膀旁挥两下，然后自然放下。";
        public bool useLlm=true;
        public int seconds=4,seed=11;
        public string Status {get;private set;}="准备启动 MoMask…";
        public string SessionDirectory {get;private set;}
        public string LastSample {get;private set;}
        public string LastPerformance {get;private set;}
        public MotionInspectionLayer inspectionLayer=MotionInspectionLayer.LegacyCorrected;
        public bool latestCalibration;
        public bool InspectionBusy;
        readonly Queue<string> replayQueue=new Queue<string>();
        bool replaying;
        int replayCount,replayIndex;
        string replayIssues="";
        double replayHold;
        public bool ReplayInProgress=>replaying;
        public int ReplayedSegments {get;private set;}
        public float ReplayedSeconds {get;private set;}
        public bool Ready=>(workerState=="READY"||workerState=="BUSY");
        public bool Busy=>!string.IsNullOrEmpty(pending);
        public float FirstMotion {get;private set;}=-1;
        public double ModelSeconds=>result?.momaskSeconds??-1;
        MotionLabDriver driver;
        Process process;
        string workerState="LOADING",pending="",english="",note="",seedText="11",error="";
        double submitted,pollAt;
        double lastClock,activePlaybackSeconds,firstPlayAt=-1,lastMotionAt=-1,totalElapsed=-1;
        double playedClipSeconds;
        int completedSegments;
        Progress progress;
        public double ActualPlaybackSeconds=>activePlaybackSeconds;
        public double GeneratedMotionSeconds=>result?.generatedMotionSeconds??progress?.generatedMotionSeconds??0;
        bool waitingForMotion,playingMeasured,randomSeed;
        public bool fixedDuration,adapted=true;
        int nextSegment;
        bool planningDone;
        double finishedAt=-1;
        string activeRequest="",currentFolder="";
        readonly List<ContextTurn> context=new List<ContextTurn>();
        readonly List<string> played=new List<string>();
        readonly List<EventRecord> events=new List<EventRecord>();
        void Record(string name,string detail="")
        {
            events.Add(new EventRecord{name=name,detail=detail,seconds=Time.realtimeSinceStartupAsDouble-submitted});
            if(!string.IsNullOrEmpty(pending))File.WriteAllText(Path.Combine(pending,"unity-events.json"),JsonUtility.ToJson(new EventList{events=events.ToArray()},true));
        }
        Result result;
        Rect priorRect;
        Font font;
        GUISkin skin;
        Vector2 scroll;
        readonly object logLock=new object();
        string logPath;
        void Start()
        {
            driver=GetComponent<MotionLabDriver>();
            if(driver==null){enabled=false;return;}
            driver.Tts?.StopSpeech();driver.waitForAudio=false;driver.UseAdaptation=true;priorRect=driver.viewer.rect;
            font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);
            StartWorker();
        }
        public void StartWorker()
        {
            if(process!=null&&!process.HasExited)return;
            DisposeWorker(false);
            SessionDirectory=Path.Combine("F:/Project/MotionExperiments/Evidence/Playground",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(SessionDirectory);logPath=Path.Combine(SessionDirectory,"worker.log");
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            var start=new ProcessStartInfo("F:/Project/MotionExperiments/momask-env/Scripts/python.exe")
            {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,
             Arguments=Quote(Path.Combine(project,"Tools/MotionLab/momask_playground.py"))+" --session "+Quote(SessionDirectory)+" --config "+Quote(Path.GetFullPath(Path.Combine(Application.dataPath,"../UserSettings/VirtualPartnerLlmConfig.json")))};
            start.EnvironmentVariables["PYTHONUTF8"]="1";
            process=new Process{StartInfo=start};process.OutputDataReceived+=Log;process.ErrorDataReceived+=Log;
            try{process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();workerState="LOADING";error="";Status="首次加载与预热中；完成后可连续生成，不计入提交耗时。";}
            catch(Exception ex){error=ex.Message;workerState="FAILED";Status="模型启动失败";process.Dispose();process=null;}
        }
        void Log(object sender,DataReceivedEventArgs e)
        {
            if(e.Data==null)return;
            lock(logLock)
            {
                if(!ReferenceEquals(sender,process))return;
                try{File.AppendAllText(logPath,e.Data+Environment.NewLine);}catch(IOException){}
            }
        }
        void DisposeWorker(bool terminate)
        {
            lock(logLock)
            {
                if(process==null)return;
                process.OutputDataReceived-=Log;process.ErrorDataReceived-=Log;
                try
                {
                    if(terminate&&!process.HasExited)
                    {
                        // Windows venv Python is a launcher with a child interpreter.
                        // Stop this owned tree, not just the launcher PID.
                        using(var stop=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+process.Id+" /T /F")
                        {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true}))
                            stop?.WaitForExit(1000);
                    }
                }
                catch(InvalidOperationException){}
                catch(System.ComponentModel.Win32Exception){if(!process.HasExited)process.Kill();}
                finally{process.Dispose();process=null;}
            }
        }
        public void Submit()
        {
            if(!Ready)return;
            if(string.IsNullOrWhiteSpace(prompt)){error="请输入动作指令。";return;}
            if(prompt.Trim()=="停止"||prompt.Trim()=="复位"||prompt.Trim().Equals("stop",StringComparison.OrdinalIgnoreCase)){Cancel();return;}
            if(!useLlm){foreach(char c in prompt)if(c>='\u3400'&&c<='\u9fff'){error="中文请选择 LLM 模式。";return;}}
            if(Busy){File.WriteAllText(Path.Combine(pending,"cancel"),"Superseded");Record("superseded");}
            replaying=false;replayQueue.Clear();driver.Paused=false;
            error="";english="";note="";result=null;FirstMotion=-1;events.Clear();played.Clear();nextSegment=0;planningDone=false;
            progress=null;activePlaybackSeconds=playedClipSeconds=0;firstPlayAt=lastMotionAt=totalElapsed=-1;completedSegments=0;lastClock=Time.realtimeSinceStartupAsDouble;
            if(randomSeed)seed=UnityEngine.Random.Range(0,int.MaxValue);
            pending=Path.Combine(SessionDirectory,"jobs",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(pending);
            submitted=Time.realtimeSinceStartupAsDouble;
            var req=new Request{text=prompt,useLlm=useLlm,seconds=seconds,seed=seed,fixedDuration=fixedDuration||!useLlm,context=context.ToArray(),runtime=driver.Status+"; holds="+driver.Holds};
            string temp=Path.Combine(pending,"request.tmp");File.WriteAllText(temp,JsonUtility.ToJson(req,true),new UTF8Encoding(false));File.Move(temp,Path.Combine(pending,"request.json"));
            Record("submit",prompt);Status="流式规划中；当前动作继续到新动作就绪。";
        }
        public void Cancel()
        {
            replaying=false;replayQueue.Clear();
            if(Busy){totalElapsed=Time.realtimeSinceStartupAsDouble-submitted;SaveTiming();Record("cancel");File.WriteAllText(Path.Combine(pending,"cancel"),"Cancelled by user");pending="";}
            if(driver!=null){driver.EndMeasuredRequest();driver.ResetPose();}
            waitingForMotion=playingMeasured=false;finishedAt=-1;activeRequest="";Status="已停止；保持及待播段已清除。";
        }
        public void Replay()
        {
            ReplaySaved(LastPerformance,false);
        }
        public void ReplayCurrent()=>ReplaySaved(LastSample,true);
        public void ReplaySaved(string path,bool single)
        {
            if(Busy||string.IsNullOrEmpty(path))return;
            driver.EndMeasuredRequest();driver.ResetInspectionPose();replayQueue.Clear();replayIssues="";
            if(single)replayQueue.Enqueue(path);
            else
            {
                LastPerformance=path;
                var root=Path.Combine(path,"segments");
                if(!Directory.Exists(root)){Status="缺少整场段落目录；可选择重播当前段。";return;}
                var folders=Directory.GetDirectories(root);Array.Sort(folders,StringComparer.Ordinal);
                int expected=0;
                foreach(var folder in folders)
                {
                    if(!int.TryParse(Path.GetFileName(folder),out var index))continue;
                    if(index!=expected)replayIssues+="; 缺少段 "+expected;
                    expected=index+1;
                    if(File.Exists(Path.Combine(folder,"ready.json")))replayQueue.Enqueue(folder);else replayIssues+="; 段 "+index+" 未就绪";
                }
                var outcome=Path.Combine(path,"result.json");
                if(File.Exists(outcome)){var r=JsonUtility.FromJson<Result>(File.ReadAllText(outcome));if(r.status!="GENERATED")replayIssues=r.error??r.status;}
                else replayIssues="原演出没有完成记录；仅重播已接受段";
            }
            replayCount=replayQueue.Count;replayIndex=0;ReplayedSegments=0;ReplayedSeconds=0;replaying=true;replayHold=0;
            waitingForMotion=playingMeasured=false;finishedAt=-1;AdvanceReplay();
        }
        void AdvanceReplay()
        {
            while(replayQueue.Count>0)
            {
                var folder=replayQueue.Dequeue();replayIndex++;
                var ready=Path.Combine(folder,"ready.json");
                var segment=File.Exists(ready)?JsonUtility.FromJson<MotionSegment>(File.ReadAllText(ready)):new MotionSegment();
                if(!string.IsNullOrEmpty(segment.status)&&segment.status!="GENERATED"){replayIssues+="; 段 "+replayIndex+": "+segment.error;continue;}
                if(segment.operation=="reset"){driver.ResetPose();continue;}
                if(segment.operation=="release"){driver.ReleaseParts(segment.parts);continue;}
                if(!File.Exists(Path.Combine(folder,"motion.json"))){replayIssues+="; 段 "+replayIndex+" 动作文件缺失";continue;}
                driver.FoundationMode=inspectionLayer==MotionInspectionLayer.Foundation;
                driver.UseAdaptation=inspectionLayer==MotionInspectionLayer.LegacyCorrected;
                driver.LatestCalibration=latestCalibration;
                driver.CurrentSegment=segment;driver.Continuous=replayIndex>1;
                driver.Load(folder,"motion.json",driver.Continuous);driver.waitForAudio=false;driver.Play();driver.Continuous=true;
                LastSample=folder;english=segment.motion;note=segment.intent;replayHold=0;
                Status="重播 "+replayIndex+" / "+replayCount+" · "+driver.Duration.ToString("F2")+"s · "+inspectionLayer+" · "+segment.intent;
                return;
            }
            replaying=false;driver.EndSegment();Status="整场重播结束；视觉质量待确认。"+(replayIssues==""?"":" 不完整："+replayIssues);
        }
        void Update()
        {
            if(InspectionBusy)return;
            if(driver==null){Start();if(driver==null)return;}
            var now=Time.realtimeSinceStartupAsDouble;
            if(Busy&&activeRequest==pending&&driver.Playing&&!driver.SegmentFinished&&!driver.Paused)activePlaybackSeconds+=Math.Max(0,now-lastClock);
            lastClock=now;
            float width=Mathf.Min(480,Screen.width*.38f);driver.viewer.rect=new Rect(width/Screen.width,0,1-width/Screen.width,1);
            if(replaying)
            {
                if(driver.Paused)return;
                if(driver.SegmentFinished)
                {
                    if(replayHold==0){replayHold=now;ReplayedSegments++;ReplayedSeconds+=driver.Duration;}
                    if(driver.CurrentSegment.completion!="hold"||now-replayHold>=driver.CurrentSegment.holdSeconds)AdvanceReplay();
                }
                else if(!driver.Playing){replaying=false;Status="重播中止："+driver.Status;}
                return;
            }
            if(waitingForMotion&&activeRequest==pending&&driver.FirstMotionSeconds>=0)
            {if(FirstMotion<0)FirstMotion=driver.FirstMotionSeconds;waitingForMotion=false;Record("first_motion");SaveTiming();}
            if(driver.SegmentFinished&&finishedAt<0)
            {finishedAt=Time.realtimeSinceStartupAsDouble;if(Busy&&activeRequest==pending){lastMotionAt=finishedAt;completedSegments++;playedClipSeconds+=driver.Duration;}Record("segment_finished",currentFolder);}
            if(driver.SegmentFinished&&finishedAt>=0&&!driver.Paused&&Time.realtimeSinceStartupAsDouble-finishedAt>1)
                driver.EndSegment(); // explicit holds have independent ownership
            if(Time.realtimeSinceStartupAsDouble<pollAt)return;pollAt=Time.realtimeSinceStartupAsDouble+.03;
            try
            {
                var statePath=Path.Combine(SessionDirectory,"worker.json");
                if(File.Exists(statePath)){var state=JsonUtility.FromJson<Worker>(File.ReadAllText(statePath));workerState=state.status;if(state.status=="FAILED")error=state.error;}
                if(process!=null&&process.HasExited&&workerState!="FAILED"){workerState="STOPPED";Status="模型退出，可重新启动。";}
                if(!Busy){if(workerState=="READY"&&Status.StartsWith("首次"))Status="模型已预热，可以生成。";return;}
                if(workerState=="FAILED"||workerState=="STOPPED"){error="模型退出，已接受段保留";planningDone=true;}
                string donePath=Path.Combine(pending,"result.json");
                string progressPath=Path.Combine(pending,"progress.json");if(File.Exists(progressPath))progress=JsonUtility.FromJson<Progress>(File.ReadAllText(progressPath));
                if(File.Exists(donePath))
                {result=JsonUtility.FromJson<Result>(File.ReadAllText(donePath));planningDone=true;if(result.status!="GENERATED")error=result.error??result.status;}
                bool takeover=activeRequest!=pending;
                bool canAdvance=takeover||!driver.Playing||driver.SegmentFinished;
                if(!takeover&&driver.CurrentSegment.completion=="hold"&&finishedAt>=0&&Time.realtimeSinceStartupAsDouble-finishedAt<driver.CurrentSegment.holdSeconds)canAdvance=false;
                var folder=Path.Combine(pending,"segments",nextSegment.ToString("D3"));var ready=Path.Combine(folder,"ready.json");
                if(canAdvance&&!driver.Paused&&File.Exists(ready))
                {
                    var segment=JsonUtility.FromJson<MotionSegment>(File.ReadAllText(ready));nextSegment++;
                    if(segment.status!="GENERATED"){error=segment.error??"Segment failed";Record("segment_failed",error);return;}
                    if(segment.operation=="reset"){driver.ResetPose();Record("reset");return;}
                    if(segment.operation=="release"){driver.ReleaseParts(segment.parts);Record("release",string.Join(",",segment.parts));return;}
                    bool continuous=driver.Playing||driver.HasHolds;
                    if(takeover){driver.BeginMeasuredRequest(false);driver.SetMeasuredSubmission(submitted);activeRequest=pending;}
                    driver.FoundationMode=false;driver.UseAdaptation=adapted;driver.CurrentSegment=segment;driver.Continuous=continuous;
                    Record("load_start",folder);driver.Load(folder,"motion.json",continuous);driver.Play(submitted);driver.Continuous=true;
                    Record("play_start",segment.intent);played.Add(segment.intent);english=segment.motion;note=segment.intent;
                    if(firstPlayAt<0)firstPlayAt=Time.realtimeSinceStartupAsDouble;
                    LastPerformance=pending;LastSample=currentFolder=folder;finishedAt=-1;waitingForMotion=playingMeasured=true;Status="第 "+nextSegment+" 段演出中；后续段可继续生成。";
                }
                else if(planningDone&&!File.Exists(ready)&&!driver.Playing)
                {
                    totalElapsed=Time.realtimeSinceStartupAsDouble-submitted;SaveTiming();driver.EndMeasuredRequest();
                    context.Add(new ContextTurn{instruction=prompt,result=string.Join("; ",played)+(string.IsNullOrEmpty(error)?"; playback completed, quality unreviewed":"; failure: "+error)});
                    if(context.Count>8)context.RemoveAt(0);
                    Record("request_finished",error);pending="";Status=(string.IsNullOrEmpty(error)?"演出结束":"仅完成部分演出："+error)+"；保持："+driver.Holds;
                }
                if(Busy&&Time.realtimeSinceStartupAsDouble-submitted>180&&!planningDone)
                {Record("timeout");File.WriteAllText(Path.Combine(pending,"cancel"),"Planner timeout");planningDone=true;error="规划超过 180 秒；未完成部分已记录。";}
            }
            catch(Exception ex){error=ex.Message;Record("exception",ex.GetType().Name);Cancel();}
        }
        void SaveTiming()
        {
            if(string.IsNullOrEmpty(pending))return;
            File.WriteAllText(Path.Combine(pending,"interactive-timing.json"),JsonUtility.ToJson(new Timing{firstMotionSeconds=FirstMotion,llmSeconds=result?.llmSeconds??-1,momaskSeconds=result?.momaskSeconds??-1,totalWorkerSeconds=result?.totalWorkerSeconds??-1,generatedMotionSeconds=GeneratedMotionSeconds,completedClipSeconds=playedClipSeconds,activePlaybackSeconds=activePlaybackSeconds,performanceSpanSeconds=firstPlayAt<0?-1:Math.Max(0,(lastMotionAt<0?Time.realtimeSinceStartupAsDouble:lastMotionAt)-firstPlayAt),totalElapsedSeconds=totalElapsed,completedSegments=completedSegments},true));
        }
        void OnGUI()
        {
            if(InspectionBusy)return;
            if(skin==null)
            {
                skin=Instantiate(GUI.skin);skin.font=font;
                foreach(var s in new[]{skin.label,skin.button,skin.textArea,skin.textField,skin.toggle}){s.fontSize=16;s.wordWrap=true;}
                skin.button.fixedHeight=36;skin.label.padding=new RectOffset(0,0,3,3);
            }
            var previous=GUI.skin;GUI.skin=skin;
            float width=Mathf.Min(480,Screen.width*.38f);
            // The camera only clears its own viewport. An opaque fill is required
            // here or previous IMGUI frames remain visible underneath the controls.
            var tint=GUI.color;GUI.color=new Color(.08f,.09f,.11f,1);
            GUI.DrawTexture(new Rect(0,0,width,Screen.height),Texture2D.whiteTexture);GUI.color=tint;
            GUILayout.BeginArea(new Rect(16,16,width-32,Screen.height-32));scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("MoMask 动作试验",new GUIStyle(skin.label){fontSize=24,fontStyle=FontStyle.Bold});
            GUILayout.Label("临时场景 · 无 TTS · 模型常驻");
            GUILayout.Label("服务："+workerState+"   "+Status);
            useLlm=GUILayout.Toggle(useLlm,"中文 / 自然语言 → LLM 动作描述");
            GUILayout.Label(useLlm?"流式分段；使用本次实验上下文，支持追问与保持。":"英文单段对照，不调用 LLM。");
            GUI.SetNextControlName("motion-prompt");prompt=GUILayout.TextArea(prompt,2000,GUILayout.Height(95));
            fixedDuration=GUILayout.Toggle(fixedDuration,"高级：固定时长对照（默认由 LLM 决定）");
            if(fixedDuration||!useLlm){GUILayout.BeginHorizontal();GUILayout.Label("动作长度",GUILayout.Width(80));
            int choice=GUILayout.SelectionGrid(seconds==2?0:seconds==8?2:1,new[]{"2 秒","4 秒","8 秒"},3);seconds=new[]{2,4,8}[choice];GUILayout.EndHorizontal();}
            adapted=GUILayout.Toggle(adapted,"生成后使用旧 Toki 修正（基础检查见下方重播层）");
            GUILayout.Label("检查重播层（不会重新生成）");
            inspectionLayer=(MotionInspectionLayer)GUILayout.SelectionGrid((int)inspectionLayer,new[]{"基础传递","旧原始","旧修正"},3);
            latestCalibration=GUILayout.Toggle(latestCalibration,"显式使用最新配置比较（默认保留样本快照）");
            randomSeed=GUILayout.Toggle(randomSeed,"每次使用新随机种子");
            GUILayout.BeginHorizontal();GUILayout.Label("固定种子",GUILayout.Width(80));seedText=GUILayout.TextField(seedText,10);if(int.TryParse(seedText,out var parsed)&&parsed>=0)seed=parsed;GUILayout.EndHorizontal();
            GUI.enabled=Ready&&int.TryParse(seedText,out var valid)&&valid>=0;
            if(GUILayout.Button("生成并演出"))Submit();GUI.enabled=true;
            GUILayout.BeginHorizontal();if(GUILayout.Button("中断 / 复位"))Cancel();GUI.enabled=!Busy&&!string.IsNullOrEmpty(LastPerformance);if(GUILayout.Button("重播整场"))Replay();GUI.enabled=true;GUILayout.EndHorizontal();
            GUI.enabled=!Busy&&!string.IsNullOrEmpty(LastSample);if(GUILayout.Button("重播当前段"))ReplayCurrent();GUI.enabled=true;
            GUI.enabled=!Busy;if(GUILayout.Button("打开已保存的整场演出")){var folder=UnityEditor.EditorUtility.OpenFolderPanel("选择含 segments 的演出目录",SessionDirectory,"");if(!string.IsNullOrEmpty(folder))ReplaySaved(folder,false);}GUI.enabled=true;
            if(replaying||ReplayedSegments>0)GUILayout.Label("重播已完成 "+ReplayedSegments+" 段 · "+ReplayedSeconds.ToString("F2")+"s；不计入原始生成耗时");
            GUILayout.BeginHorizontal();if(GUILayout.Button(driver.Paused?"继续":"暂停"))driver.Paused=!driver.Paused;
            GUI.enabled=!Busy;if(GUILayout.Button("下一帧"))InspectTime(driver.PlaybackElapsed+.05f);GUI.enabled=true;
            if(GUILayout.Button("0.5× / 1×"))driver.PlaybackSpeed=driver.PlaybackSpeed==1?.5f:1;GUILayout.EndHorizontal();
            GUI.enabled=!Busy;if(driver.Duration>0){var seek=GUILayout.HorizontalSlider(driver.PlaybackElapsed,0,driver.Duration);if(Mathf.Abs(seek-driver.PlaybackElapsed)>.08f)InspectTime(seek);}GUI.enabled=true;
            GUILayout.Label("保持："+driver.Holds);
            var diag=driver.AdaptationDiagnostic;if(diag!=null)GUILayout.Label("修正 "+diag.correction.ToString("F3")+"m · 残余穿插 "+diag.residualPenetration.ToString("F3")+"m · 头倾 "+diag.headTilt.ToString("F1")+"°");
            if(diag!=null&&diag.sourceFacingConflict)GUILayout.Label("未通过方向检查：源颈头和双脚方向与髋部参考冲突。保留播放用于诊断，不自动翻转。",new GUIStyle(skin.label){normal={textColor=new Color(1,.6f,.25f)}});
            if(workerState=="FAILED"||workerState=="STOPPED")if(GUILayout.Button("重新启动模型"))StartWorker();
            if(Busy)GUILayout.Label("提交后："+(Time.realtimeSinceStartupAsDouble-submitted).ToString("F2")+" 秒");
            GUILayout.Space(8);GUILayout.Label("本次计时（排除首次加载与预热）");
            GUILayout.Label("LLM："+Seconds(result?.llmSeconds??-1)+"    MoMask："+Seconds(result?.momaskSeconds??-1));
            GUILayout.Label("提交 → 首个身体动作："+Seconds(FirstMotion));
            GUILayout.Label("本次全部动作："+Seconds(GeneratedMotionSeconds)+(planningDone?"":"（生成中，累计）")+" · 已完成 "+completedSegments+" 段");
            GUILayout.Label("实际运动累计："+Seconds(activePlaybackSeconds)+" · 速度 "+driver.PlaybackSpeed.ToString("F1")+"×");
            GUILayout.Label("整场跨度（含暂停／段间等待）："+Seconds(firstPlayAt<0?-1:(Busy?Time.realtimeSinceStartupAsDouble:Math.Max(firstPlayAt,lastMotionAt))-firstPlayAt));
            GUILayout.Label("提交至结束："+Seconds(Busy?Time.realtimeSinceStartupAsDouble-submitted:totalElapsed));
            if(!string.IsNullOrEmpty(error))GUILayout.Label("未完整完成："+error);
            GUILayout.Label("实际 MoMask 输入");GUILayout.TextArea(string.IsNullOrEmpty(english)?"生成后显示；不会用预设替换失败结果。":english,GUILayout.MinHeight(70));
            if(!string.IsNullOrEmpty(note))GUILayout.Label(note);
            if(!string.IsNullOrEmpty(error))GUILayout.Label("错误："+error);
            if(GUILayout.Button("打开结果目录"))UnityEditor.EditorUtility.RevealInFinder(LastSample??SessionDirectory);
            GUILayout.Label("退出 Play Mode 会关闭本次模型进程。长动作顺序、掌心与接地仍需观察确认。",new GUIStyle(skin.label){fontSize=13});
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.skin=previous;
        }
        static string Seconds(double value)=>value<0?"—":value.ToString("F3")+" s";
        void InspectTime(float time){replaying=false;replayQueue.Clear();driver.Seek(time);Status="当前段检查：从干净状态重建；整场接续请重新点击重播整场。";}
        static string Quote(string value)=>"\""+value+"\"";
        void OnDisable()
        {
            if(!string.IsNullOrEmpty(SessionDirectory))File.WriteAllText(Path.Combine(SessionDirectory,"stop"),"Exit playground");
            if(driver!=null){Cancel();if(driver.viewer!=null)driver.viewer.rect=priorRect;}
            // This panel owns the worker; release its GPU allocation even if a
            // network read or inference is still running when Play Mode exits.
            DisposeWorker(true);
            if(font!=null)Destroy(font);if(skin!=null)Destroy(skin);
        }
        [Serializable] sealed class Request{public string text,runtime;public bool useLlm,fixedDuration;public int seconds,seed;public ContextTurn[] context;}
        [Serializable] sealed class ContextTurn{public string instruction,result;}
        [Serializable] sealed class EventRecord{public string name,detail;public double seconds;}
        [Serializable] sealed class EventList{public EventRecord[] events;}
        [Serializable] sealed class Worker{public string status,error;}
        [Serializable] sealed class Prompt{public string motion,note;}
        [Serializable] sealed class Progress{public double plannedMotionSeconds,generatedMotionSeconds;public int acceptedSegments,generatedSegments;}
        [Serializable] sealed class Result{public string status,error,motion,note;public double llmSeconds,momaskSeconds,totalWorkerSeconds,plannedMotionSeconds,generatedMotionSeconds;}
        [Serializable] sealed class Timing{public float firstMotionSeconds;public double llmSeconds,momaskSeconds,totalWorkerSeconds,generatedMotionSeconds,completedClipSeconds,activePlaybackSeconds,performanceSpanSeconds,totalElapsedSeconds;public int completedSegments;}
    }
}
#endif
