#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
using VirtualPartner.Runtime;
using VirtualPartner.Runtime.Experiments;

namespace VirtualPartner.Editor
{
    // Local, explicit experiment commands. No daily-scene mutation or arbitrary code execution.
    [InitializeOnLoad]
    public static class MotionAdaptationProbe
    {
        static string Folder=>Path.GetFullPath("Library/MotionLab/adaptation");
        static MotionAdaptationProbe(){EditorApplication.update+=Poll;}
        public static void Open(){MoMaskPlaygroundLauncher.Open();}
        static void Poll()
        {
            if(EditorApplication.isCompiling||!File.Exists(Path.Combine(Folder,"command.json")))return;
            try
            {
                var file=Path.Combine(Folder,"command.json");var c=JsonUtility.FromJson<Command>(File.ReadAllText(file));File.Delete(file);
                if(c.action=="open"){Open();return;}
                if(c.action=="stop"){EditorApplication.isPlaying=false;return;}
                if(c.action=="refresh"){AssetDatabase.Refresh();return;}
                var d=UnityEngine.Object.FindFirstObjectByType<MotionLabDriver>();
                var p=UnityEngine.Object.FindFirstObjectByType<MoMaskPlayground>();
                if(c.action=="status")
                {
                    File.WriteAllText(Path.Combine(Folder,"status.json"),JsonUtility.ToJson(new Status{play=EditorApplication.isPlaying,ready=p!=null&&p.Ready,busy=p!=null&&p.Busy,status=p?.Status,session=p?.SessionDirectory,sample=p?.LastSample,holds=d?.Holds,firstMotion=p?.FirstMotion??-1,playing=d!=null&&d.Playing,elapsed=d?.PlaybackElapsed??0},true));return;
                }
                if(d==null||d.Bones==null)throw new InvalidOperationException("MotionLab not initialized");
                if(c.action=="dump")
                {
                    var basis=d.character.transform.rotation*Quaternion.LookRotation(d.rig.bodyForward,d.rig.bodyUp);
                    var inverse=Quaternion.Inverse(basis);
                    var data=new Dump{bones=d.Bones.Select(b=>new Bone{name=b.Key,path=AnimationUtility.CalculateTransformPath(b.Value,d.character.transform),position=inverse*(b.Value.position-d.character.transform.position),localPosition=b.Value.localPosition,rotation=b.Value.localRotation,forward=inverse*b.Value.forward,up=inverse*b.Value.up}).ToArray(),renderers=d.character.GetComponentsInChildren<Renderer>().Select(r=>new BoundsInfo{name=r.name,center=inverse*(r.bounds.center-d.character.transform.position),size=r.bounds.size}).ToArray()};
                    File.WriteAllText(Path.Combine(Folder,"rig.json"),JsonUtility.ToJson(data,true));
                }
                if(c.action=="submit"){p.prompt=c.text;p.useLlm=c.useLlm;p.seconds=c.seconds;p.Submit();}
                if(c.action=="cancel")p.Cancel();
                if(c.action=="replay")p.Replay();
                if(c.action=="review")d.StartCoroutine(Review(d,c.path));
                if(c.action=="foundation-test")d.StartCoroutine(MotionFoundationReview.Check(d,c.path));
                if(c.action=="foundation-review")d.StartCoroutine(MotionFoundationReview.Capture(d,c.path));
                if(c.action=="foundation-replay-test")d.StartCoroutine(MotionFoundationReview.ReplayCheck(d,p,c.path));
                if(c.action=="foundation-benchmark")d.StartCoroutine(MotionFoundationReview.Benchmark(d,c.path));
                if(c.action=="foundation-debug-test")d.StartCoroutine(MotionFoundationReview.DebugCheck(d,c.path));
                if(c.action=="replay-saved")p.ReplaySaved(c.path,false);
                if(c.action=="scope-test")d.StartCoroutine(ScopeTest(d,c.path));
                if(c.action=="idle-test")d.StartCoroutine(IdleTest(d,c.path));
                if(c.action=="seek"){d.Seek(c.time);}
                if(c.action=="mode"){p.adapted=c.adapted;d.FoundationMode=c.foundation;d.UseAdaptation=c.adapted;}
                if(c.action=="load"){d.Load(c.path);d.Play();}
                if(c.action=="capture")ScreenCapture.CaptureScreenshot(c.path);
            }
            catch(Exception ex){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"error.txt"),ex.ToString());}
        }
        static IEnumerator IdleTest(MotionLabDriver d,string source)
        {
            var folder=Path.Combine(Folder,"idle-test-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(folder);
            d.ResetPose();yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();
            var bindings=AnimationUtility.GetCurveBindings(d.idle);
            var bones=d.boneRoot.GetComponentsInChildren<Transform>(true);
            var unkeyed=bones.Where(b=>!bindings.Any(c=>c.path==AnimationUtility.CalculateTransformPath(b,d.character.transform)&&c.propertyName.StartsWith("m_LocalPosition"))).ToArray();
            var positions=unkeyed.Select(b=>b.localPosition).ToArray();
            var rootPosition=d.character.transform.position;var rootRotation=d.character.transform.rotation;
            File.WriteAllText(Path.Combine(folder,"idle-bindings.json"),JsonUtility.ToJson(new BindingReport{bindings=bindings.Select(b=>b.path+":"+b.propertyName).ToArray(),unkeyedPositions=unkeyed.Select(b=>b.name).ToArray()},true));
            foreach(var sample in Directory.GetDirectories(Path.Combine(source,"segments")).OrderBy(s=>s))
            {
                if(!File.Exists(Path.Combine(sample,"motion.json")))continue;
                var output=Path.Combine(folder,Path.GetFileName(sample));Directory.CreateDirectory(output);File.Copy(Path.Combine(sample,"motion.json"),Path.Combine(output,"motion.json"));
                d.CurrentSegment=JsonUtility.FromJson<MotionSegment>(File.ReadAllText(Path.Combine(sample,"ready.json")));
                d.UseAdaptation=true;d.Continuous=true;d.Load(output,"motion.json",true);d.Play();
                while(!d.SegmentFinished&&d.Playing)yield return null;
            }
            d.EndSegment();yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();
            float max=0;for(int i=0;i<unkeyed.Length;i++)max=Mathf.Max(max,Vector3.Distance(positions[i],unkeyed[i].localPosition));
            SaveView(d.viewer,Path.Combine(folder,"after-idle.png"));
            File.WriteAllText(Path.Combine(folder,"complete.json"),JsonUtility.ToJson(new IdleReport{unkeyedPositionCount=unkeyed.Length,maximumUnkeyedDrift=max,rootPositionDrift=Vector3.Distance(rootPosition,d.character.transform.position),rootRotationDrift=Quaternion.Angle(rootRotation,d.character.transform.rotation)},true));
            if(max>.0001f)throw new Exception("Idle unkeyed transform residue: "+max);
        }
        [Serializable] sealed class BindingReport{public string[] bindings,unkeyedPositions;}
        [Serializable] sealed class IdleReport{public int unkeyedPositionCount;public float maximumUnkeyedDrift,rootPositionDrift,rootRotationDrift;}
        static IEnumerator ScopeTest(MotionLabDriver d,string source)
        {
            var root=Path.Combine(Folder,"scope-test-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);
            var segment=new MotionSegment{parts=new[]{"leftArm","rightArm","torso","head"}};
            if(!segment.ResolvePlaybackParts(new string[0]).Contains("support"))throw new Exception("Default must retain generated support");
            if(segment.ResolvePlaybackParts(new[]{"support"}).Contains("support"))throw new Exception("Unrelated support hold must survive");
            segment.parts=new[]{"rightArm"};segment.playbackScope="parts";
            if(segment.ResolvePlaybackParts(new string[0]).Length!=1)throw new Exception("Explicit local mask expanded");
            segment.parts=new[]{"leftArm"};
            if(!segment.ResolvePlaybackParts(new[]{"leftArm"}).Contains("leftArm"))throw new Exception("Explicit hold takeover excluded");
            foreach(string scope in new[]{"parts","body"})
            {
                d.ResetPose();yield return new WaitForSeconds(.5f);
                var folder=Path.Combine(root,scope);Directory.CreateDirectory(folder);
                File.Copy(Path.Combine(source,"motion.json"),Path.Combine(folder,"motion.json"));
                var snapshot=Path.Combine(source,"adaptation-profile.json");if(File.Exists(snapshot))File.Copy(snapshot,Path.Combine(folder,"adaptation-profile.json"));
                d.UseAdaptation=true;d.Continuous=false;d.CurrentSegment=new MotionSegment{parts=new[]{"leftArm","rightArm","torso","head"},playbackScope=scope};
                d.Load(folder);d.Play();bool captured=false;
                while(d.Playing)
                {
                    yield return new WaitForEndOfFrame();
                    if(!captured&&d.PlaybackElapsed>=1.5f){SaveView(d.viewer,Path.Combine(folder,"frame.png"));captured=true;}
                }
            }
            // Real coordinator regression: a held left arm must survive a right
            // arm request; excluded legs must not keep the prior segment's ID.
            d.ResetPose();yield return new WaitForSeconds(.5f);
            string Prepare(string name)
            {
                var folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
                File.Copy(Path.Combine(source,"motion.json"),Path.Combine(folder,"motion.json"));return folder;
            }
            d.CurrentSegment=new MotionSegment{parts=new[]{"leftArm"},playbackScope="parts",completion="infinite"};
            d.Continuous=false;d.Load(Prepare("hold"));d.Play();
            while(d.Playing)yield return null;
            if(!d.Holds.Contains("leftArm"))throw new Exception("Left hold not established");
            d.CurrentSegment=new MotionSegment{parts=new[]{"rightArm"},playbackScope="body"};
            d.Continuous=true;d.Load(Prepare("body-takeover"),"motion.json",true);d.Play();
            yield return new WaitForSeconds(.6f);
            if(!d.Holds.Contains("leftArm"))throw new Exception("Unrelated hold lost during body coordination");
            if(d.Coordinator.GetOwner(d.Bones["Foot:L"])!=BoneOwner.SpatialMotion)throw new Exception("Body motion did not acquire legs");
            d.CurrentSegment=new MotionSegment{parts=new[]{"rightArm"},playbackScope="parts"};
            d.Continuous=true;d.Load(Prepare("local-takeover"),"motion.json",true);d.Play();
            yield return new WaitForSeconds(.6f);
            if(!d.Holds.Contains("leftArm"))throw new Exception("Local takeover released unrelated hold");
            if(d.Coordinator.GetOwner(d.Bones["Foot:L"])==BoneOwner.SpatialMotion||d.Coordinator.GetOwner(d.Bones["Foot:R"])==BoneOwner.SpatialMotion)throw new Exception("Excluded legs retained stale ownership");
            d.ResetPose();
            File.WriteAllText(Path.Combine(root,"complete.json"),"{\"scopeContractAssertions\":4,\"runtimeOwnershipAssertions\":5,\"sameSourceMotion\":true}");
        }
        static IEnumerator Review(MotionLabDriver d,string folder)
        {
            var evidence=Path.Combine(folder,"review-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(evidence);
            var camera=d.viewer;var priorRect=camera.rect;var priorPosition=camera.transform.position;var priorRotation=camera.transform.rotation;
            var priorCapture=Time.captureFramerate;Time.captureFramerate=20;camera.rect=new Rect(0,0,1,1);
            try
            {
                foreach(bool corrected in new[]{false,true})
                {
                    d.ResetPose();yield return new WaitForSeconds(.4f);d.UseLatestCalibration();d.UseAdaptation=corrected;d.CurrentSegment=new MotionSegment();d.Continuous=false;
                    // Existing snapshots stay intact; this explicitly compares the latest calibration.
                    d.Load(folder,"motion.json",false);d.UseLatestCalibration();d.Play();
                    string variant=Path.Combine(evidence,corrected?"corrected":"original");Directory.CreateDirectory(variant);int frame=0;
                    while(d.Playing)
                    {
                        yield return new WaitForEndOfFrame();
                        SaveView(camera,Path.Combine(variant,frame.ToString("D4")+".png"));frame++;
                    }
                    File.Copy(Path.Combine(d.OutputDirectory,"adaptation-"+(corrected?"corrected":"original")+".json"),Path.Combine(variant,"diagnostics.json"));
                }
                File.WriteAllText(Path.Combine(evidence,"complete.json"),"{\"fps\":20,\"captureOverheadExcludedFromPerformance\":true}");
            }
            finally{Time.captureFramerate=priorCapture;camera.rect=priorRect;camera.transform.SetPositionAndRotation(priorPosition,priorRotation);d.ResetPose();}
        }
        public static void SaveView(Camera camera,string path)
        {
            var target=new RenderTexture(960,720,24);var old=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var tex=new Texture2D(960,720,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,960,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{camera.rect=rect;camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(tex);}
        }
        [Serializable] sealed class Command{public string action,text,path;public bool useLlm=true,adapted=true,foundation;public int seconds=4;public float time;}
        [Serializable] sealed class Status{public bool play,ready,busy,playing;public string status,session,sample,holds;public float firstMotion,elapsed;}
        [Serializable] sealed class Dump{public Bone[] bones;public BoundsInfo[] renderers;}
        [Serializable] sealed class Bone{public string name,path;public Vector3 position,localPosition,forward,up;public Quaternion rotation;}
        [Serializable] sealed class BoundsInfo{public string name;public Vector3 center,size;}
    }
}
#endif
