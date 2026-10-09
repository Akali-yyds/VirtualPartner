#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using VirtualPartner.Runtime;
using Object = UnityEngine.Object;

namespace VirtualPartner.Editor
{
    public static class SpatialMotionReview
    {
        public const string Output="Library/MCPForUnity/SpatialMotionReview";
        public static StagePlanRotationDto V(float x,float y,float z)=>new StagePlanRotationDto{x=x,y=y,z=z};
        public static StagePlanActionDto Thinking()
        {
            return new StagePlanActionDto{type="spatialPose",completion=new SpatialCompletionDto{mode="hold"},tracks=new[]{
                new SpatialTrackDto{group="rightArm",anchor="head",keys=new[]{new SpatialKeyDto{time=.8f,position=V(.035f,-.17f,.14f),palm=V(-1,0,0),fingers=V(0,1,0),hint=V(.25f,-.35f,.2f)}}},
                new SpatialTrackDto{group="leftArm",anchor="chest",keys=new[]{new SpatialKeyDto{time=.95f,position=V(.015f,-.03f,.10f),palm=V(0,1,0),fingers=V(1,0,0),hint=V(-.2f,-.15f,.15f)}}},
                new SpatialTrackDto{group="head",anchor="body",keys=new[]{new SpatialKeyDto{time=.9f,bones=new[]{new StagePlanBonePoseDto{bone="Head",side="None",rotation=V(0,0,5)}}}}}
            }};
        }
        public static string PreviewThinking()
        {
            var runtime=Object.FindFirstObjectByType<SpatialMotionRuntime>();
            Object.FindFirstObjectByType<AutonomousBehaviorScheduler>().EnterUserInteraction();
            var action=Thinking();var result=new System.Text.StringBuilder();
            foreach(var track in action.tracks)
                result.AppendLine(track.group+": "+runtime.TryStart(new StagePlanActionDto{type="spatialPose",tracks=new[]{track},completion=action.completion},"visual:"+track.group,out var error)+" "+error);
            return result.ToString();
        }
        public static string SaveThinking()
        {
            var p=AssetDatabase.LoadAssetAtPath<SpatialRigProfile>("Assets/VirtualPartner/Resources/TokiSpatialRig.asset");
            p.thinkingTracks=Thinking().tracks;EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();return "Saved character waiting pose.";
        }
        public static string StartChecks()
        {
            var runtime=Object.FindFirstObjectByType<SpatialMotionRuntime>();
            if(runtime==null)return "Enter Play Mode first.";
            runtime.StartCoroutine(CheckRoutine(runtime));return "Checks started.";
        }
        private static IEnumerator CheckRoutine(SpatialMotionRuntime runtime)
        {
            Directory.CreateDirectory(Output);var lines=new List<string>();int passed=0,failed=0;
            Action<bool,string> check=(ok,name)=>{if(ok)passed++;else failed++;lines.Add((ok?"PASS ":"FAIL ")+name);};
            Object.FindFirstObjectByType<AutonomousBehaviorScheduler>().EnterUserInteraction();
            runtime.ResetPose();yield return new WaitForSeconds(.5f);
            var parsed=JsonUtility.FromJson<StagePlanActionDto>(JsonUtility.ToJson(Thinking()));
            check(SpatialMotionContract.ResolveCompletion(parsed.tracks[0],parsed.completion).mode=="hold","JSON missing track completion inherits action hold");
            parsed.tracks[0].completion=new SpatialCompletionDto{mode="idle"};
            check(SpatialMotionContract.ResolveCompletion(parsed.tracks[0],parsed.completion).mode=="idle","Explicit track idle overrides action hold");
            parsed.tracks[0].keys[0].time=0;
            check(SpatialMotionContract.Validate(parsed,out _),"Zero-time first key accepted for smooth entry");
            var invalid=Thinking();invalid.tracks[0].keys[0].time=float.NaN;
            check(!SpatialMotionContract.Validate(invalid,out _),"Non-finite time rejected");
            var conflict=Thinking();conflict.tracks[0].keys[0].bones=new[]{new StagePlanBonePoseDto{bone="UpperArm",side="R",rotation=V(0,0,0)}};
            check(!SpatialMotionContract.Validate(conflict,out _),"Mixed arm IK/direct control rejected");
            var move=Thinking();move.tracks=new[]{move.tracks[0]};
            bool accepted=runtime.TryStart(move,"test-hold",out var error);check(accepted,"Reachable arm: "+error);
            yield return new WaitForSeconds(1.2f);
            check(runtime.HasPersistentPose,"Infinite hold survives transition completion");
            runtime.CancelMoving();check(runtime.HasPersistentPose,"Request cancellation preserves established hold");
            var head=Thinking();head.tracks=new[]{head.tracks[2]};
            check(runtime.TryStart(head,"head-hold",out error),"Independent head hold accepted: "+error);
            yield return new WaitForSeconds(1.1f);
            runtime.ResetPose("head");
            check(runtime.HasPersistentPose && runtime.Describe().Contains("rightArm:") && !runtime.Describe().Contains("head:"),"Partial reset preserves unrelated arm hold");
            var unreachable=Thinking();unreachable.tracks=new[]{unreachable.tracks[0]};unreachable.tracks[0].keys[0].position=V(20,20,20);
            check(!runtime.TryStart(unreachable,"bad",out _),"Unreachable target rejected");check(runtime.HasPersistentPose,"Rejected takeover preserves old hold");
            move=Thinking();move.tracks=new[]{move.tracks[0]};move.tracks[0].keys[0].position=V(.09f,-.2f,.18f);move.completion=new SpatialCompletionDto{mode="restore"};
            check(runtime.TryStart(move,"test-restore",out error),"Temporary takeover: "+error);
            yield return new WaitForSeconds(1.4f);check(runtime.HasPersistentPose,"Explicit restore resumes old hold");
            runtime.ResetPose();yield return new WaitForSeconds(.5f);check(!runtime.HasAny,"Reset clears all spatial state");
            var partial=Thinking();partial.tracks=new[]{partial.tracks[0],partial.tracks[2]};partial.tracks[0].anchor="chest";partial.tracks[0].keys[0].position=V(20,20,20);
            check(runtime.TryStart(partial,"partial",out error),"Valid independent head track survives unreachable arm");
            yield return new WaitForSeconds(1.2f);
            check(runtime.HasPersistentPose && runtime.Failure("partial")!=null && runtime.Describe().Contains("head:"),"Partial execution retains explicit failure and successful hold");
            runtime.ResetPose();yield return new WaitForSeconds(.5f);
            move=Thinking();move.tracks=new[]{move.tracks[0]};move.completion=new SpatialCompletionDto{mode="timed",seconds=.5f};
            check(runtime.TryStart(move,"timed",out error),"Timed hold accepted: "+error);
            yield return new WaitForSeconds(1f);check(runtime.HasPersistentPose,"Timer begins after arrival");
            yield return new WaitForSeconds(.7f);check(!runtime.HasAny,"Timed hold expires");
            var crouch=new StagePlanActionDto{type="spatialPose",completion=new SpatialCompletionDto{mode="hold"},tracks=new[]{new SpatialTrackDto{group="support",anchor="body",keys=new[]{new SpatialKeyDto{time=1,position=V(0,-.08f,0)}}}}};
            check(runtime.TryStart(crouch,"crouch",out error),"Shallow crouch with planted feet: "+error);
            yield return new WaitForSeconds(1.3f);check(runtime.HasSupport,"Support remains acquired");
            var lookup=typeof(SpatialMotionRuntime).GetMethod("T",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var leftFoot=(Transform)lookup.Invoke(runtime,new object[]{"Foot","L"});
            var rightFoot=(Transform)lookup.Invoke(runtime,new object[]{"Foot","R"});
            yield return new WaitForEndOfFrame();var leftStart=leftFoot.position;var rightStart=rightFoot.position;
            var temporary=JsonUtility.FromJson<StagePlanActionDto>(JsonUtility.ToJson(crouch));temporary.completion.mode="restore";
            temporary.tracks[0].keys[0].position=V(0,.03f,0);temporary.tracks[0].keys[0].time=.5f;
            check(runtime.TryStart(temporary,"support-restore",out error),"Support temporary override accepted: "+error);
            float maxDrift=0,until=Time.time+1.3f;
            while(Time.time<until)
            {
                yield return new WaitForEndOfFrame();
                maxDrift=Mathf.Max(maxDrift,Vector3.Distance(leftStart,leftFoot.position),Vector3.Distance(rightStart,rightFoot.position));
            }
            var leg=Array.Find(runtime.profile.limbs,x=>x.group=="leftLeg");
            check(runtime.HasSupport && maxDrift/(leg.upperLength+leg.lowerLength)<=runtime.profile.footTolerance,"Support restore keeps feet planted; relative max drift="+(maxDrift/(leg.upperLength+leg.lowerLength)));
            runtime.ResetPose();yield return new WaitForSeconds(.5f);
            int failedRequest=0;Action<int,string> onFailure=(id,reason)=>failedRequest=id;runtime.HeldMotionFailed+=onFailure;
            check(runtime.TryStart(crouch,"held-failure",out error,requestId:420),"Request-scoped support hold accepted");
            yield return new WaitForSeconds(1.3f);
            float previousLimit=runtime.profile.maxPelvisOffset;
            try {runtime.profile.maxPelvisOffset=.001f;yield return null;yield return null;}
            finally {runtime.profile.maxPelvisOffset=previousLimit;runtime.HeldMotionFailed-=onFailure;}
            check(failedRequest==420 && !runtime.HasSupport,"Late hold failure retains originating request and releases support");
            yield return new WaitForEndOfFrame();SaveFrame("crouch.png");runtime.ResetPose();yield return new WaitForSeconds(.5f);
            runtime.BeginThinking(1001);yield return new WaitForSeconds(1.3f);check(runtime.HasAny,"Waiting pose starts");
            runtime.EndThinking(1000);check(runtime.HasAny,"Stale request cannot release waiting pose");
            runtime.EndThinking(1001);check(!runtime.HasAny,"Matching request releases waiting pose");
            yield return new WaitForSeconds(.5f);
            var direct=new StagePlanActionDto{type="spatialPose",completion=new SpatialCompletionDto{mode="hold"},tracks=new[]{new SpatialTrackDto{group="head",keys=new[]{
                new SpatialKeyDto{time=.4f,bones=new[]{new StagePlanBonePoseDto{bone="Head",side="None",rotation=V(0,0,10)}}},
                new SpatialKeyDto{time=.9f,bones=new[]{new StagePlanBonePoseDto{bone="Neck",side="None",rotation=V(0,0,1)}}}
            }}}};
            check(runtime.TryStart(direct,"omitted-joint",out error),"Sparse direct trajectory accepted: "+error);
            yield return new WaitForSeconds(1.2f);yield return new WaitForEndOfFrame();
            var headBone=(Transform)lookup.Invoke(runtime,new object[]{"Head","None"});
            var headCalibration=Array.Find(runtime.profile.joints,j=>j.bone=="Head"&&j.side=="None");
            check(Quaternion.Angle(headBone.localRotation,headCalibration.restRotation*Quaternion.Euler(0,0,10))<.2f,"Omitted later head key retains preceding target");
            runtime.ResetPose();yield return new WaitForSeconds(.5f);
            for(int phase=0;phase<4;phase++)
            {
                yield return new WaitForSeconds(.37f+phase*.21f);
                runtime.BeginThinking(1100+phase);yield return new WaitForSeconds(1.5f);
                var activeField=typeof(SpatialMotionRuntime).GetField("active",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var active=(System.Collections.IDictionary)activeField.GetValue(runtime);
                check(active.Contains("rightArm")&&active.Contains("leftArm")&&active.Contains("head"),"Thinking retains all available groups from Idle phase "+phase+": "+runtime.LastDiagnostic);
                runtime.EndThinking(1100+phase);
            }
            lines.Insert(0,$"passed={passed}, failed={failed}");File.WriteAllLines(Output+"/checks.txt",lines);Debug.Log("Spatial review: "+lines[0]);
        }
        public static void SaveFrame(string name)
        {Directory.CreateDirectory(Output);var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());Object.Destroy(image);}
        public static string RecordThinking()
        {var runtime=Object.FindFirstObjectByType<SpatialMotionRuntime>();runtime.StartCoroutine(Record(runtime));return "Recording started.";}
        public static string CaptureFrontThinking()
        {
            var runtime=Object.FindFirstObjectByType<SpatialMotionRuntime>();
            runtime.StartCoroutine(FrontThinking(runtime));return "Front review scheduled after final bone writes.";
        }
        private static IEnumerator FrontThinking(SpatialMotionRuntime runtime)
        {
            runtime.ResetPose();yield return new WaitForSeconds(.5f);runtime.BeginThinking(9999);
            yield return new WaitForSeconds(1.5f);yield return new WaitForEndOfFrame();
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var root=(Transform)typeof(SpatialMotionRuntime).GetField("body",flags).GetValue(runtime);
            var lookup=typeof(SpatialMotionRuntime).GetMethod("T",flags);
            var head=(Transform)lookup.Invoke(runtime,new object[]{"Head","None"});
            var foot=(Transform)lookup.Invoke(runtime,new object[]{"Foot","L"});
            float h=Vector3.Distance(head.position,foot.position);var center=(head.position+foot.position)*.5f+Vector3.up*h*.12f;
            var obj=new GameObject("Temporary pose review camera");var cam=obj.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.enabled=false;cam.fieldOfView=40;cam.nearClipPlane=.01f;
            var target=new RenderTexture(960,1080,24);var previous=RenderTexture.active;var tex=new Texture2D(960,1080,TextureFormat.RGB24,false);
            try
            {
                cam.targetTexture=target;obj.transform.position=center+root.TransformDirection(runtime.profile.bodyForward).normalized*h*2.7f;obj.transform.LookAt(center);cam.Render();
                RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,960,1080),0,0);tex.Apply();
                File.WriteAllBytes(Output+"/thinking-front.png",tex.EncodeToPNG());File.WriteAllText(Output+"/thinking-front-status.txt",runtime.LastDiagnostic+"\n"+runtime.Describe());
            }
            finally {RenderTexture.active=previous;cam.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(tex);Object.Destroy(obj);runtime.EndThinking(9999);}
        }
        private static IEnumerator Record(SpatialMotionRuntime runtime)
        {
            Directory.CreateDirectory(Output+"/frames");runtime.ResetPose();yield return new WaitForSeconds(.5f);
            runtime.BeginThinking(999);float started=Time.unscaledTime;int frame=0;var times=new List<string>();
            while(Time.unscaledTime-started<4)
            {
                if(Time.unscaledTime-started>2.5f)runtime.EndThinking(999);
                yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Output+"/frames/"+(frame++).ToString("D5")+".jpg",image.EncodeToJPG(85));Object.Destroy(image);
                times.Add((Time.unscaledTime-started).ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
            }
            File.WriteAllLines(Output+"/frames/times.txt",times);
        }
    }
}
#endif
