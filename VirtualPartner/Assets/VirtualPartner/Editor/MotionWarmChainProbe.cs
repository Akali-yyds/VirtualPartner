#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.Experiments;

namespace VirtualPartner.Editor
{
    // Explicit file-triggered isolated probe. Never starts without a local command.
    [InitializeOnLoad]
    public static class MotionWarmChainProbe
    {
        static string Command=>Path.GetFullPath("Library/MotionLab/warm-chain-command.json");
        static bool active;
        static MotionWarmChainProbe(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(active||EditorApplication.isCompiling||!File.Exists(Command))return;
            var job=JsonUtility.FromJson<CommandDto>(File.ReadAllText(Command));
            if(!File.Exists(Path.Combine(job.root,"ready.json")))return;
            if(!EditorApplication.isPlaying)
            {
                if(EditorSceneManager.GetActiveScene().isDirty)return;
                if(!SessionState.GetBool("WarmChain.Pending",false))
                {
                    SessionState.SetString("WarmChain.OriginalScene",EditorSceneManager.GetActiveScene().path);
                    SessionState.SetBool("WarmChain.Pending",true);
                    EditorSceneManager.OpenScene("Assets/Scenes/MotionLab.unity");EditorApplication.isPlaying=true;
                }
                return;
            }
            var driver=UnityEngine.Object.FindFirstObjectByType<MotionLabDriver>();
            if(driver==null||driver.Tts==null||!driver.Tts.Initialized)return;
            active=true;File.Move(Command,Command+".started-"+DateTime.UtcNow.Ticks);
            driver.StartCoroutine(Run(driver,job.root));
        }
        static double Unix()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0;
        static IEnumerator Run(MotionLabDriver d,string root)
        {
            var report=new Report();var events=new List<Event>();var frames=new List<float>();
            Action<string> mark=name=>{events.Add(new Event{name=name,unix=Unix()});File.WriteAllText(Path.Combine(root,"unity-timeline.json"),JsonUtility.ToJson(new Events{events=events.ToArray()},true));};
            var settings=new SerializedObject(d.Tts);string oldUrl=settings.FindProperty("serviceUrl").stringValue;
            var oldProfile=settings.FindProperty("characterProfile").objectReferenceValue;
            var profile=UnityEngine.Object.Instantiate(d.characterProfile);
            var voice=UnityEngine.Object.Instantiate(profile.VoiceProfile);
            var vs=new SerializedObject(voice);vs.FindProperty("characterId").stringValue="warm-greeting-"+Guid.NewGuid().ToString("N");vs.ApplyModifiedPropertiesWithoutUndo();
            var ps=new SerializedObject(profile);ps.FindProperty("characterId").stringValue="warm-greeting-"+Guid.NewGuid().ToString("N");ps.ApplyModifiedPropertiesWithoutUndo();
            ps.FindProperty("voiceProfile").objectReferenceValue=voice;ps.ApplyModifiedPropertiesWithoutUndo();
            settings.FindProperty("characterProfile").objectReferenceValue=profile;
            settings.FindProperty("serviceUrl").stringValue="http://127.0.0.1:8766";
            settings.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                d.ResetPose();d.Tts.ReleaseSpeech();yield return new WaitForSeconds(.5f);
                var submitted=d.BeginMeasuredRequest(false);mark("submit");
                File.WriteAllText(Path.Combine(root,"request.json"),"{\"text\":\"早上好呀\"}");
                bool tts=false,loaded=false,audio=false,motion=false;
                double ttsStart=-1,loadStart=-1;
                while(Time.realtimeSinceStartupAsDouble-submitted<180)
                {
                    frames.Add(Time.unscaledDeltaTime*1000);
                    if(!tts&&File.Exists(Path.Combine(root,"plan.json")))
                    {
                        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(root,"plan.json")));
                        ttsStart=Time.realtimeSinceStartupAsDouble;mark("tts_start");
                        d.Tts.StartSpeech(new StagePlanActionDto{type="speech",text=plan.speech},10,out var failure);
                        if(!string.IsNullOrEmpty(failure)){report.error=failure;break;}tts=true;
                    }
                    if(!loaded&&File.Exists(Path.Combine(root,"sample/motion.json")))
                    {
                        loadStart=Time.realtimeSinceStartupAsDouble;mark("unity_load_start");
                        d.Load(Path.Combine(root,"sample"));mark("unity_load_complete");d.Play(submitted);loaded=true;
                    }
                    if(!motion&&d.FirstMotionSeconds>=0){mark("first_body_motion_observed");motion=true;}
                    if(!audio&&d.AudioStartSeconds>=0){mark("audio_playback_observed");audio=true;}
                    if(tts&&!string.IsNullOrEmpty(d.Tts.LatestError)){report.error=d.Tts.LatestError;break;}
                    if(File.Exists(Path.Combine(root,"worker-complete.json")))
                    {
                        var worker=JsonUtility.FromJson<Worker>(File.ReadAllText(Path.Combine(root,"worker-complete.json")));
                        if(worker.status=="FAILED"){report.error=worker.error;break;}
                        if(loaded&&tts&&!d.Playing&&!d.Tts.Active){report.completed=true;break;}
                    }
                    yield return null;
                }
                report.firstMotion=d.FirstMotionSeconds;report.firstAudio=d.AudioStartSeconds;
                report.ttsStart=ttsStart<0?-1:ttsStart-submitted;report.unityLoadStart=loadStart<0?-1:loadStart-submitted;
                report.cached=d.Tts.Cached;report.provider=d.Tts.CurrentProvider;report.streaming=d.Tts.StreamingPlayback;
                report.underruns=d.Tts.StreamingUnderrunCount;report.total=Time.realtimeSinceStartupAsDouble-submitted;
                report.realAudio=report.firstAudio>=0&&!report.cached&&string.IsNullOrEmpty(d.Tts.LatestError);
                if(!report.completed&&report.error==null)report.error="Timed out";
                frames.Sort();report.frameMsP50=frames[frames.Count/2];report.frameMsP95=frames[Math.Min(frames.Count-1,(int)(frames.Count*.95))];report.frameMsMax=frames[frames.Count-1];
                mark("complete");d.EndMeasuredRequest();
                File.WriteAllText(Path.Combine(root,"unity-report.json"),JsonUtility.ToJson(report,true));
                MotionLabReview.SaveCamera(d.viewer,Path.Combine(root,"after-timing.png"));
            }
            finally
            {
                d.Tts.StopSpeech();d.ResetPose();settings.Update();settings.FindProperty("serviceUrl").stringValue=oldUrl;
                settings.FindProperty("characterProfile").objectReferenceValue=oldProfile;settings.ApplyModifiedPropertiesWithoutUndo();UnityEngine.Object.Destroy(profile);UnityEngine.Object.Destroy(voice);
                File.WriteAllText(Path.Combine(root,"stop"),"Probe finished");
                SessionState.SetBool("WarmChain.Pending",false);active=false;
            }
        }
        [Serializable] sealed class CommandDto{public string root;}
        [Serializable] sealed class Plan{public string speech;}
        [Serializable] sealed class Worker{public string status,error;}
        [Serializable] sealed class Event{public string name;public double unix;}
        [Serializable] sealed class Events{public Event[] events;}
        [Serializable] sealed class Report{public bool completed,realAudio,cached,streaming;public string error,provider;public double firstMotion,firstAudio,ttsStart,unityLoadStart,total;public int underruns;public float frameMsP50,frameMsP95,frameMsMax;}
    }
}
#endif
