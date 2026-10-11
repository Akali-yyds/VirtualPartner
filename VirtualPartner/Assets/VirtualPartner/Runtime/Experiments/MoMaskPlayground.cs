#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        public bool Ready=>workerState=="READY"&&string.IsNullOrEmpty(pending);
        public bool Busy=>!string.IsNullOrEmpty(pending);
        public float FirstMotion {get;private set;}=-1;
        public double ModelSeconds=>result?.momaskSeconds??-1;
        MotionLabDriver driver;
        Process process;
        string workerState="LOADING",pending="",english="",note="",seedText="11",error="";
        double submitted,pollAt;
        bool waitingForMotion,playingMeasured,randomSeed;
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
            driver.Tts?.StopSpeech();driver.waitForAudio=false;priorRect=driver.viewer.rect;
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
            if(!useLlm){foreach(char c in prompt)if(c>='\u3400'&&c<='\u9fff'){error="中文请选择 LLM 转换模式；直接模式需要英文。";return;}}
            if(seconds!=2&&seconds!=4&&seconds!=8){error="请选择 2、4 或 8 秒。";return;}
            Cancel();error="";english="";note="";result=null;FirstMotion=-1;
            if(randomSeed)seed=UnityEngine.Random.Range(0,int.MaxValue);
            pending=Path.Combine(SessionDirectory,"jobs",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(pending);
            submitted=driver.BeginMeasuredRequest(false);
            var req=new Request{text=prompt,useLlm=useLlm,seconds=seconds,seed=seed};
            string temp=Path.Combine(pending,"request.tmp");File.WriteAllText(temp,JsonUtility.ToJson(req,true),new UTF8Encoding(false));File.Move(temp,Path.Combine(pending,"request.json"));
            Status="已提交，等待生成…";
        }
        public void Cancel()
        {
            if(Busy){File.WriteAllText(Path.Combine(pending,"cancel"),"Cancelled by user");pending="";}
            if(driver!=null){driver.EndMeasuredRequest();driver.ResetPose();}
            waitingForMotion=playingMeasured=false;Status="已停止；迟到结果不会自动播放。";
        }
        public void Replay()
        {
            if(Busy||string.IsNullOrEmpty(LastSample))return;
            driver.EndMeasuredRequest();driver.Load(LastSample);driver.waitForAudio=false;driver.Play();
            Status="重播已生成动作（不重新计入生成延迟）";waitingForMotion=playingMeasured=false;
        }
        void Update()
        {
            if(driver==null)return;
            // Reserve the left third for controls; keep the source/character camera framing.
            float width=Mathf.Min(480,Screen.width*.38f);driver.viewer.rect=new Rect(width/Screen.width,0,1-width/Screen.width,1);
            if(waitingForMotion&&driver.FirstMotionSeconds>=0)
            {
                FirstMotion=driver.FirstMotionSeconds;waitingForMotion=false;SaveTiming();Status="正在演出";
            }
            if(playingMeasured&&!driver.Playing)
            {
                playingMeasured=false;waitingForMotion=false;SaveTiming();driver.EndMeasuredRequest();
                Status=FirstMotion>=0?"演出结束；可修改指令再次生成，或重播对照。":"未检测到相关动作，请查看诊断。";
            }
            if(Time.realtimeSinceStartupAsDouble<pollAt)return;pollAt=Time.realtimeSinceStartupAsDouble+.03;
            try
            {
                if(File.Exists(Path.Combine(SessionDirectory,"worker.json")))
                {var state=JsonUtility.FromJson<Worker>(File.ReadAllText(Path.Combine(SessionDirectory,"worker.json")));workerState=state.status;if(state.status=="FAILED")error=state.error;}
                if(process!=null&&process.HasExited&&workerState!="FAILED"){workerState="STOPPED";Status="模型进程已退出，可重新启动。";}
                if(Busy&&(workerState=="FAILED"||workerState=="STOPPED")){Cancel();error="模型进程退出，请重新启动后再试。";return;}
                if(!Busy){if(workerState=="READY"&&Status.StartsWith("首次"))Status="模型已预热，可以输入并生成。";return;}
                if(Time.realtimeSinceStartupAsDouble-submitted>120){Cancel();error="请求超过 120 秒，已取消自动播放。";return;}
                string path=Path.Combine(pending,"prompt.json");
                if(File.Exists(path)){var value=JsonUtility.FromJson<Prompt>(File.ReadAllText(path));english=value.motion;note=value.note;}
                path=Path.Combine(pending,"progress.json");if(File.Exists(path))Status=JsonUtility.FromJson<Worker>(File.ReadAllText(path)).status=="LLM"?"LLM 正在转换动作描述…":"MoMask 正在生成…";
                path=Path.Combine(pending,"result.json");if(!File.Exists(path))return;
                result=JsonUtility.FromJson<Result>(File.ReadAllText(path));string finished=pending;pending="";
                if(result.status!="GENERATED"){error=result.error??result.status;Status="本次生成未完成";driver.EndMeasuredRequest();return;}
                LastSample=finished;english=result.motion;note=result.note;
                driver.Load(LastSample);driver.waitForAudio=false;driver.Play(submitted);waitingForMotion=playingMeasured=true;Status="已生成，启动演出…";
            }
            catch(Exception ex){error=ex.Message;Status="实验出错";Cancel();}
        }
        void SaveTiming()
        {
            if(string.IsNullOrEmpty(LastSample)||result==null)return;
            File.WriteAllText(Path.Combine(LastSample,"interactive-timing.json"),JsonUtility.ToJson(new Timing{firstMotionSeconds=FirstMotion,llmSeconds=result.llmSeconds,momaskSeconds=result.momaskSeconds,totalWorkerSeconds=result.totalWorkerSeconds},true));
        }
        void OnGUI()
        {
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
            GUILayout.Label(useLlm?"转换耗时单独记录；只发送本次输入。":"直接模式：请输入英文动作描述，不调用 LLM。");
            GUI.SetNextControlName("motion-prompt");prompt=GUILayout.TextArea(prompt,2000,GUILayout.Height(95));
            GUILayout.BeginHorizontal();GUILayout.Label("动作长度",GUILayout.Width(80));
            int choice=GUILayout.SelectionGrid(seconds==2?0:seconds==8?2:1,new[]{"2 秒","4 秒","8 秒"},3);seconds=new[]{2,4,8}[choice];GUILayout.EndHorizontal();
            randomSeed=GUILayout.Toggle(randomSeed,"每次使用新随机种子");
            GUILayout.BeginHorizontal();GUILayout.Label("固定种子",GUILayout.Width(80));seedText=GUILayout.TextField(seedText,10);if(int.TryParse(seedText,out var parsed)&&parsed>=0)seed=parsed;GUILayout.EndHorizontal();
            GUI.enabled=Ready&&int.TryParse(seedText,out var valid)&&valid>=0;
            if(GUILayout.Button("生成并演出"))Submit();GUI.enabled=true;
            GUILayout.BeginHorizontal();if(GUILayout.Button("中断 / 复位"))Cancel();GUI.enabled=!Busy&&!string.IsNullOrEmpty(LastSample);if(GUILayout.Button("重播"))Replay();GUI.enabled=true;GUILayout.EndHorizontal();
            if(workerState=="FAILED"||workerState=="STOPPED")if(GUILayout.Button("重新启动模型"))StartWorker();
            if(Busy)GUILayout.Label("提交后："+(Time.realtimeSinceStartupAsDouble-submitted).ToString("F2")+" 秒");
            GUILayout.Space(8);GUILayout.Label("本次计时（排除首次加载与预热）");
            GUILayout.Label("LLM："+Seconds(result?.llmSeconds??-1)+"    MoMask："+Seconds(result?.momaskSeconds??-1));
            GUILayout.Label("提交 → 首个身体动作："+Seconds(FirstMotion));
            GUILayout.Label("实际 MoMask 输入");GUILayout.TextArea(string.IsNullOrEmpty(english)?"生成后显示；不会用预设替换失败结果。":english,GUILayout.MinHeight(70));
            if(!string.IsNullOrEmpty(note))GUILayout.Label(note);
            if(!string.IsNullOrEmpty(error))GUILayout.Label("错误："+error);
            if(GUILayout.Button("打开结果目录"))UnityEditor.EditorUtility.RevealInFinder(LastSample??SessionDirectory);
            GUILayout.Label("退出 Play Mode 会关闭本次模型进程。长动作顺序、掌心与接地仍需观察确认。",new GUIStyle(skin.label){fontSize=13});
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.skin=previous;
        }
        static string Seconds(double value)=>value<0?"—":value.ToString("F3")+" s";
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
        [Serializable] sealed class Request{public string text;public bool useLlm;public int seconds,seed;}
        [Serializable] sealed class Worker{public string status,error;}
        [Serializable] sealed class Prompt{public string motion,note;}
        [Serializable] sealed class Result{public string status,error,motion,note;public double llmSeconds,momaskSeconds,totalWorkerSeconds;}
        [Serializable] sealed class Timing{public float firstMotionSeconds;public double llmSeconds,momaskSeconds,totalWorkerSeconds;}
    }
}
#endif
