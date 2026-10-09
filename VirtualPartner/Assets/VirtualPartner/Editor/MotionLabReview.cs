#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VirtualPartner.Runtime.Experiments;
using Object=UnityEngine.Object;

namespace VirtualPartner.Editor
{
    public static class MotionLabReview
    {
        static bool running;
        public static bool Running {get=>running&&Application.isPlaying;private set=>running=value;}
        public static string Status {get;private set;}="Not started";
        public static string StartFinalReview()
        {
            if(Running)return "Already running";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();driver.StartCoroutine(FinalReview(driver));return "Final review: lifecycle, semantic, learned, audio comparisons";
        }
        static IEnumerator FinalReview(MotionLabDriver driver)
        {
            Checks();yield return Lifecycle(driver);
            foreach(var suite in new[]{"semantic-suite","momask-suite","dip-suite-v2","dip-character-prefix-v2"})
                yield return Run(driver,Path.Combine(MotionLabWindow.ResultsRoot,suite));
            foreach(var route in new[]{"semantic","momask","dip"})yield return AudioComparison(driver,route);
            Status="FINAL_REVIEW_COMPLETE";
            File.WriteAllText(Path.Combine(MotionLabWindow.ResultsRoot,"review-complete.txt"),DateTime.UtcNow.ToString("O"));
        }
        static IEnumerator Lifecycle(MotionLabDriver driver)
        {
            var notes=new List<string>();int failures=0;
            Action<bool,string> check=(ok,label)=>{notes.Add((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;};
            var folder=Path.Combine(MotionLabWindow.ResultsRoot,"semantic-suite");
            driver.Load(Path.Combine(folder,"10_hold-11"));driver.Play();while(driver.Playing)yield return null;
            check(driver.Motion.HasPersistentPose,"Semantic persistent hold is established");
            var right=driver.Bones["Hand:R"];var held=right.position;
            driver.Load(Path.Combine(folder,"11_partial-11"));driver.Play();while(driver.Playing)yield return null;
            check(driver.Motion.HasPersistentPose,"Partial takeover preserves prior hold");
            check(Vector3.Distance(held,right.position)<.04f,"Held right hand remains near its prior target");
            driver.ResetPose();yield return new WaitForSeconds(.5f);check(!driver.Motion.HasAny,"Reset clears experimental holds");
            var fixture=Path.Combine(MotionLabWindow.ResultsRoot,"controlled-timed-hold");Directory.CreateDirectory(fixture);
            var timed=JsonUtility.FromJson<MotionLabSemantic>(File.ReadAllText(Path.Combine(folder,"10_hold-11/semantic.json")));
            timed.steps=new[]{timed.steps[0]};timed.steps[0].duration=.5f;timed.steps[0].completion="timed";timed.steps[0].holdSeconds=.8f;
            File.WriteAllText(Path.Combine(fixture,"semantic.json"),JsonUtility.ToJson(timed));
            File.WriteAllText(Path.Combine(fixture,"fixture-note.txt"),"Controlled scheduler test, not a model-generated or quality-accepted sample");
            driver.Load(fixture);driver.Play();yield return new WaitForSeconds(.95f);
            check(driver.Playing&&driver.Motion.HasPersistentPose,"Timed hold remains active after travel until hold interval expires");
            yield return new WaitForSeconds(.7f);
            check(!driver.Playing&&!driver.Motion.HasPersistentPose,"Timed hold releases after its own interval");
            driver.ResetPose();yield return new WaitForSeconds(.5f);
            var instances=new List<Runtime.BoneMapInstance>();driver.boneMap.BuildControlInstances(driver.boneRoot,instances);
            var hand=instances.Find(x=>x.SemanticBone==Runtime.SemanticBone.Hand&&x.Side==Runtime.BoneSide.R);
            driver.Coordinator.RequestDebug(hand,Vector3.zero);
            check(!driver.Coordinator.CanAcquireSpatial(new[]{right},false,out _),"Experimental motion cannot acquire Debug-owned hand");
            driver.ResetPose();check(driver.Coordinator.GetOwner(right)==Runtime.BoneOwner.Debug,"Body reset preserves Debug ownership");
            driver.Coordinator.ReleaseDebug(hand);yield return new WaitForSeconds(.5f);
            driver.Load(Path.Combine(folder,"12_interrupt-11"));driver.Play();yield return new WaitForSeconds(.5f);driver.Stop();yield return new WaitForSeconds(.5f);
            check(!driver.Playing,"Interruption stops experimental playback");
            driver.ResetPose();File.WriteAllText(Path.Combine(MotionLabWindow.ResultsRoot,"lifecycle-checks.txt"),"Failures="+failures+"\n"+string.Join("\n",notes));
        }
        public static string Start(string folder)
        {
            if(Running)return "Already running";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();
            if(driver==null||!Application.isPlaying)return "Play isolated experiment scene first.";
            driver.StartCoroutine(Run(driver,folder));return "Batch playback started; all samples retained.";
        }
        public static string StartSemanticValidation()
        {
            if(Running)return "Already running";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();
            driver.StartCoroutine(SemanticValidation(driver));return "Lifecycle and final-transform semantic validation started";
        }
        static IEnumerator SemanticValidation(MotionLabDriver driver)
        {
            Running=true;Checks();yield return Lifecycle(driver);
            yield return Run(driver,Path.Combine(MotionLabWindow.ResultsRoot,"semantic-suite"));
            Status="SEMANTIC_VALIDATION_COMPLETE";Running=false;
        }
        static IEnumerator Run(MotionLabDriver driver,string folder)
        {
            Running=true;var summary=new List<string>();var directories=Directory.GetDirectories(folder);Array.Sort(directories,StringComparer.Ordinal);
            try
            {
                foreach(var directory in directories)
                {
                    if(!File.Exists(Path.Combine(directory,"semantic.json"))&&!File.Exists(Path.Combine(directory,"motion.json")))continue;
                    Status=Path.GetFileName(directory);
                    driver.ResetPose();yield return new WaitForSeconds(.6f);
                    // Partial takeover explicitly begins with the previously generated persistent hold.
                    if(Status.StartsWith("11_partial"))
                    {
                        var setup=Path.Combine(folder,"10_hold-"+Status.Substring(Status.LastIndexOf('-')+1));
                        if(Directory.Exists(setup))
                        {driver.Load(setup);driver.Play();while(driver.Playing)yield return null;}
                    }
                    string error=null;
                    try{driver.Load(directory);driver.waitForAudio=false;driver.Play(interrupt:Status.StartsWith("12_interrupt"));}
                    catch(Exception ex){error=ex.Message;}
                    if(error!=null){summary.Add(Status+" LOAD_FAILED "+error);File.WriteAllLines(Path.Combine(folder,"playback-index.txt"),summary);continue;}
                    int frame=0;double next=0,start=Time.realtimeSinceStartupAsDouble;
                    var video=Path.Combine(directory,"frames-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(video);
                    // Render at real wall-clock intervals; timestamp each frame instead of claiming fixed simulation FPS.
                    var timestamps=new List<string>();
                    while(driver.Playing&&Time.realtimeSinceStartupAsDouble-start<35)
                    {
                        yield return new WaitForEndOfFrame();
                        double elapsed=Time.realtimeSinceStartupAsDouble-start;
                        if(elapsed>=next)
                        {SaveCamera(driver.viewer,Path.Combine(video,frame.ToString("D5")+".png"));timestamps.Add(elapsed.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));frame++;next=elapsed+.1;}
                    }
                    yield return new WaitForSeconds(.5f);
                    SaveCamera(driver.viewer,Path.Combine(video,"../final-"+DateTime.UtcNow.ToString("HHmmss")+".png"));
                    File.WriteAllLines(Path.Combine(video,"timestamps.txt"),timestamps);
                    summary.Add(Status+" "+driver.Status+" firstMovementPlaybackOnly="+driver.FirstMotionSeconds.ToString("F3"));
                    File.WriteAllLines(Path.Combine(folder,"playback-index.txt"),summary);
                }
                Status="Batch complete: "+summary.Count+" samples; human quality unreviewed.";
            }
            finally{driver.ResetPose();Running=false;}
        }
        public static void SaveCamera(Camera camera,string path)
        {
            var old=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);}
        }
        public static string CapturePrefix()
        {
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();
            driver.ResetPose();driver.StartCoroutine(Prefix(driver));return "Capturing 21 measured Idle frames at 20 FPS";
        }
        public static string StartAudioComparison(string route)
        {
            if(Running)return "Already running";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();driver.StartCoroutine(AudioComparison(driver,route));return "Real generation + TTS A/B started";
        }
        public static string StartRemainingAudioReview()
        {
            if(Running)return "Already running";
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();
            if(driver==null||!Application.isPlaying)return "Play isolated experiment scene first.";
            driver.StartCoroutine(RemainingAudio(driver));return "Resume learned audio recordings, then measure all routes without capture";
        }
        static IEnumerator RemainingAudio(MotionLabDriver driver)
        {
            foreach(var route in new[]{"momask","dip"})yield return AudioComparison(driver,route);
            foreach(var route in new[]{"semantic","momask","dip"})yield return AudioComparison(driver,route,false);
            Status="AUDIO_REVIEW_COMPLETE";
            File.WriteAllText(Path.Combine(MotionLabWindow.ResultsRoot,"audio-review-complete.txt"),DateTime.UtcNow.ToString("O"));
        }
        static IEnumerator AudioComparison(MotionLabDriver driver,string route,bool capture=true)
        {
            Running=true;System.Diagnostics.Process process=null;
            var ttsSettings=new UnityEditor.SerializedObject(driver.Tts);bool oldCache=ttsSettings.FindProperty("cacheCompletedStream").boolValue;
            ttsSettings.FindProperty("cacheCompletedStream").boolValue=false;ttsSettings.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                foreach(bool sync in new[]{false,true})
                {
                    driver.ResetPose();driver.Tts.ReleaseSpeech();yield return new WaitForSeconds(.5f);
                    string folder=Path.Combine(MotionLabWindow.ResultsRoot,(capture?"concurrent-":"no-capture-")+route+"-"+(sync?"audio":"action")+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder,"review-state.txt"),"RUNNING; absence of a final report means interrupted, not passed");
                    string interpreter="F:/Project/MotionExperiments/"+(route=="dip"?"dip-env":"momask-env")+"/Scripts/python.exe";
                    var start=new System.Diagnostics.ProcessStartInfo(interpreter){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
                        Arguments="\""+Path.Combine(MotionLabWindow.ProjectRoot,"Tools/MotionLab/lab.py")+"\" --route "+route+" --case 0 --device cuda:0 --seed 11 --output \""+folder+"\" --config \""+Path.GetFullPath(Path.Combine(Application.dataPath,"../UserSettings/VirtualPartnerLlmConfig.json"))+"\""};
                    start.EnvironmentVariables["PYTHONUTF8"]="1";start.EnvironmentVariables["HF_HOME"]="F:/Project/MotionExperiments/hf-cache";
                    start.EnvironmentVariables["HF_HUB_DISABLE_XET"]="1";
                    double submit=driver.BeginMeasuredRequest(sync);Status="Concurrent "+route+" "+(sync?"audio":"action");
                    process=System.Diagnostics.Process.Start(start);
                    driver.Tts.StartSpeech(new Runtime.StagePlanActionDto{type="speech",text="这是一条独立动作与语音同步测试。我会站在原地，用右手向你挥手，然后自然地放下手臂。我们正在记录动作开始和真实声音播放的时间。"},10,out var ttsFailure);
                    bool loaded=false,completed=false;string error=null;int frame=0;double next=0;
                    string video=Path.Combine(folder,"frames");Directory.CreateDirectory(video);var stamps=new List<string>();
                    while(Time.realtimeSinceStartupAsDouble-submit<120)
                    {
                        bool motionReady=File.Exists(Path.Combine(folder,route=="semantic"?"semantic.json":"motion.json"));
                        if(!loaded&&motionReady)
                        {
                            try{driver.Load(folder);driver.Play(submit);loaded=true;}catch(Exception ex){error=ex.Message;break;}
                        }
                        if(process.HasExited&&process.ExitCode!=0)
                        {
                            error="Generation/postprocessing failed; see result.json";break;
                        }
                        yield return new WaitForEndOfFrame();
                        double elapsed=Time.realtimeSinceStartupAsDouble-submit;
                        if(capture&&elapsed>=next){SaveCamera(driver.viewer,Path.Combine(video,frame.ToString("D5")+".png"));stamps.Add(elapsed.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));frame++;next=elapsed+.1;}
                        if(loaded&&process.HasExited&&!driver.Playing&&!driver.Tts.Active){completed=true;break;}
                    }
                    if(!completed&&error==null)error="Timed out before generation, playback and TTS completed";
                    if(completed&&driver.AudioStartSeconds<0)error="No real audio playback observed";
                    if(completed&&driver.FirstMotionSeconds<0)error=(error==null?"":error+"; ")+"No related body motion observed";
                    if(!process.HasExited)process.Kill();process.Dispose();process=null;
                    driver.EndMeasuredRequest();File.WriteAllLines(Path.Combine(video,"timestamps.txt"),stamps);
                    File.WriteAllText(Path.Combine(folder,"audio-comparison.json"),JsonUtility.ToJson(new AudioReport{route=route,sync=sync,capture=capture,completed=completed,firstMotion=driver.FirstMotionSeconds,firstBytes=driver.AudioFirstBytesSeconds,firstAudio=driver.AudioStartSeconds,error=error??ttsFailure,ttsError=driver.Tts.LatestError,underruns=driver.Tts.StreamingUnderrunCount,cached=driver.Tts.Cached,realAudio=driver.AudioStartSeconds>=0&&string.IsNullOrEmpty(driver.Tts.LatestError)},true));
                    File.WriteAllText(Path.Combine(folder,"review-state.txt"),completed?"COMPLETED; semantic quality requires review":"FAILED: "+error);
                }
                Status="Audio comparison complete: "+route;
            }
            finally{if(process!=null){if(!process.HasExited)process.Kill();process.Dispose();}driver.Tts.StopSpeech();driver.ResetPose();ttsSettings.Update();ttsSettings.FindProperty("cacheCompletedStream").boolValue=oldCache;ttsSettings.ApplyModifiedPropertiesWithoutUndo();Running=false;}
        }
        [Serializable] sealed class AudioReport {public string route,error,ttsError;public bool sync,realAudio,cached,capture,completed;public double firstAudio,firstBytes;public float firstMotion;public int underruns;}
        static IEnumerator Prefix(MotionLabDriver driver)
        {
            yield return new WaitForSeconds(.6f);
            var frames=new List<MotionLabFrame>();
            var origin=driver.character.transform.position;
            var inverse=Quaternion.Inverse(driver.character.transform.rotation*Quaternion.LookRotation(driver.rig.bodyForward,driver.rig.bodyUp));
            for(int f=0;f<21;f++)
            {
                yield return new WaitForEndOfFrame();
                var points=new Vector3[22];
                for(int j=0;j<22;j++)if(j!=6)points[j]=inverse*(driver.Bones[MotionLabSkeleton.Bones[j]].position-origin);
                points[6]=(points[3]+points[9])*.5f;frames.Add(new MotionLabFrame{positions=points});
                yield return new WaitForSecondsRealtime(.05f);
            }
            var clip=new MotionLabClip{version=1,route="measured-character-prefix",coordinates="body-right/up/forward; meters; HumanML22",fps=20,parents=MotionLabSkeleton.Parents,frames=frames.ToArray()};
            File.WriteAllText(Path.Combine(MotionLabWindow.ResultsRoot,"character-prefix.json"),JsonUtility.ToJson(clip));
        }
        public static string Checks()
        {
            int count=0;var notes=new List<string>();
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL "+name);count++;notes.Add("PASS "+name);};
            var driver=Object.FindFirstObjectByType<MotionLabDriver>();if(driver==null)throw new Exception("Missing experiment scene");
            check(Object.FindFirstObjectByType<Runtime.MomotalkConversationController>()==null,"No conversation/history component in experiment scene");
            check(Object.FindFirstObjectByType<Runtime.LlmRelay>()==null,"No automatic daily LLM requests");
            check(driver.Bones["Hand:L"]!=driver.Bones["Hand:R"],"Distinct left/right mapping");
            var bad=new MotionLabClip{version=1,fps=20,frames=new[]{new MotionLabFrame()}};bool rejected=false;try{bad.Validate();}catch(ArgumentException){rejected=true;}check(rejected,"Reject incomplete clip before playback");
            var step=new MotionLabStep{duration=1,completion="hold",goals=new[]{new MotionLabGoal{part="leftArm",target="shoulder",direction="outward",motion="reach",extent=.3f}}};
            var left=MotionLabSemanticPlanner.Build(step,driver.Bones,driver.character.transform,driver.rig,driver.viewer.transform);
            step.goals[0].part="rightArm";var right=MotionLabSemanticPlanner.Build(step,driver.Bones,driver.character.transform,driver.rig,driver.viewer.transform);
            check(left.tracks[0].keys[0].position.x<right.tracks[0].keys[0].position.x,"Character-relative left/right targets");
            step.goals[0].extent=float.NaN;rejected=false;try{MotionLabSemanticPlanner.Build(step,driver.Bones,driver.character.transform,driver.rig,driver.viewer.transform);}catch(ArgumentException){rejected=true;}check(rejected,"Non-finite extent rejected");
            string result=count+" passed\n"+string.Join("\n",notes);Directory.CreateDirectory(MotionLabWindow.ResultsRoot);File.WriteAllText(Path.Combine(MotionLabWindow.ResultsRoot,"checks.txt"),result);return result;
        }
    }
}
#endif
