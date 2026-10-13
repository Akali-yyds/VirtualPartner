#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualPartner.Runtime.Experiments;
using VirtualPartner.Runtime;

namespace VirtualPartner.Editor
{
    // Read-only source evidence. Captures and reports are new, timestamped runs.
    public static class MotionFoundationReview
    {
        static string NewRun(string name)
        {
            var path=Path.GetFullPath("Library/MotionLab/foundation/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+name);
            Directory.CreateDirectory(path);return path;
        }
        public static IEnumerator Check(MotionLabDriver d,string fixtures)
        {
            var folder=NewRun("checks");var rows=new List<Row>();
            d.ResetInspectionPose();File.WriteAllText(Path.Combine(folder,"rig-binding-snapshot.json"),d.RigSnapshot());
            var original=d.Bones.Values.ToArray();var idlePositions=original.Select(b=>b.localPosition).ToArray();var idleRotations=original.Select(b=>b.localRotation).ToArray();
            Quaternion? referenceHead=null,noddedHead=null;
            foreach(var sample in Directory.GetDirectories(fixtures).OrderBy(x=>x))
            {
                d.FoundationMode=true;d.UseAdaptation=false;d.CurrentSegment=new MotionSegment();d.Continuous=false;
                d.Load(sample);d.Seek(2);yield return new WaitForEndOfFrame();
                var bones=d.Bones.Values.ToArray();var positions=bones.Select(b=>b.position).ToArray();var rotations=bones.Select(b=>b.rotation).ToArray();
                var row=new Row{sample=Path.GetFileName(sample),directionError=d.DirectionError,boneLengthChange=d.BoneLengthChange};
                if(row.sample=="neck_reference")referenceHead=d.Bones["Head"].rotation;
                if(row.sample=="neck_nod")noddedHead=d.Bones["Head"].rotation;
                row.neckRotationError=d.AdaptationDiagnostic.neckRotationError;
                foreach(float time in new[]{.5f,1f,1.5f,2.5f,3f,3.5f}){d.Seek(time);yield return new WaitForEndOfFrame();row.directionError=Mathf.Max(row.directionError,d.DirectionError);row.boneLengthChange=Mathf.Max(row.boneLengthChange,d.BoneLengthChange);}
                d.Seek(3);yield return new WaitForEndOfFrame();d.Seek(2);yield return new WaitForEndOfFrame();
                for(int i=0;i<bones.Length;i++){row.repeatPosition=Mathf.Max(row.repeatPosition,Vector3.Distance(positions[i],bones[i].position));row.repeatAngle=Mathf.Max(row.repeatAngle,Quaternion.Angle(rotations[i],bones[i].rotation));}
                row.passed=row.directionError<.1f&&row.boneLengthChange<.00001f&&row.repeatPosition<.00001f&&row.repeatAngle<.1f&&row.neckRotationError<.1f;
                rows.Add(row);MotionAdaptationProbe.SaveView(d.viewer,Path.Combine(folder,row.sample+".png"));
            }
            d.ResetInspectionPose();
            float resetPosition=0,resetAngle=0;
            for(int i=0;i<original.Length;i++){resetPosition=Mathf.Max(resetPosition,Vector3.Distance(idlePositions[i],original[i].localPosition));resetAngle=Mathf.Max(resetAngle,Quaternion.Angle(idleRotations[i],original[i].localRotation));}
            float nodDelta=referenceHead.HasValue&&noddedHead.HasValue?Quaternion.Angle(referenceHead.Value,noddedHead.Value):-1;
            bool nodPassed=nodDelta>=0&&Mathf.Abs(nodDelta-20)<.1f;
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(new CheckReport{passed=rows.All(r=>r.passed)&&resetPosition<.00001f&&resetAngle<.1f&&nodPassed,headNodDelta=nodDelta,resetPosition=resetPosition,resetAngle=resetAngle,rows=rows.ToArray()},true));
        }
        public static IEnumerator ReplayCheck(MotionLabDriver d,MoMaskPlayground p,string source)
        {
            var folder=NewRun("replay");var rows=new List<ReplayRow>();
            p.inspectionLayer=MotionInspectionLayer.Foundation;d.ResetInspectionPose();
            var bones=d.Bones.Values.ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
            for(int repeat=0;repeat<2;repeat++)
            {
                double start=Time.realtimeSinceStartupAsDouble;p.ReplaySaved(source,false);
                while(p.ReplayInProgress&&Time.realtimeSinceStartupAsDouble-start<90)yield return null;
                var row=new ReplayRow{repeat=repeat,segments=p.ReplayedSegments,seconds=p.ReplayedSeconds,span=Time.realtimeSinceStartupAsDouble-start,status=p.Status};
                d.ResetInspectionPose();
                for(int i=0;i<bones.Length;i++){row.resetPosition=Mathf.Max(row.resetPosition,Vector3.Distance(positions[i],bones[i].localPosition));row.resetAngle=Mathf.Max(row.resetAngle,Quaternion.Angle(rotations[i],bones[i].localRotation));}
                row.passed=row.segments==5&&Mathf.Abs(row.seconds-30)<.01f&&row.resetPosition<.00001f&&row.resetAngle<.1f;
                rows.Add(row);
            }
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(new ReplayReport{passed=rows.All(r=>r.passed),rows=rows.ToArray()},true));
        }
        public static IEnumerator Benchmark(MotionLabDriver d,string sample)
        {
            var folder=NewRun("warm-performance");bool diagnostics=d.CaptureDiagnostics;d.CaptureDiagnostics=false;
            try
            {
                foreach(var layer in new[]{MotionInspectionLayer.Foundation,MotionInspectionLayer.LegacyRaw,MotionInspectionLayer.LegacyCorrected})
                for(int repeat=0;repeat<3;repeat++)
                {
                    d.ResetInspectionPose();d.FoundationMode=layer==MotionInspectionLayer.Foundation;d.UseAdaptation=layer==MotionInspectionLayer.LegacyCorrected;d.CurrentSegment=new MotionSegment();d.Continuous=false;
                    d.Load(sample);d.Play();while(d.Playing)yield return null;
                    var report=Directory.GetFiles(d.OutputDirectory,"playback-*.json").OrderBy(x=>x).Last();
                    File.Copy(report,Path.Combine(folder,layer+"-"+repeat+".json"));
                }
                File.WriteAllText(Path.Combine(folder,"complete.json"),"{\"repeatsPerLayer\":3,\"recording\":false,\"generationIncluded\":false,\"captureDiagnostics\":false}");
            }
            finally{d.CaptureDiagnostics=diagnostics;d.ResetInspectionPose();}
        }
        public static IEnumerator DebugCheck(MotionLabDriver d,string sample)
        {
            var folder=NewRun("debug-priority");var instances=new List<BoneMapInstance>();d.boneMap.BuildControlInstances(d.boneRoot,instances);
            var head=instances.First(x=>x.SemanticBone==SemanticBone.Head);
            if(d.Coordinator.GetOwner(head.Transform)==BoneOwner.Debug)throw new InvalidOperationException("External head Debug fixed; preserve it and postpone injected test.");
            bool acquired=false;
            try
            {
                d.ResetInspectionPose();acquired=d.Coordinator.RequestDebug(head,Vector3.zero);if(!acquired)throw new Exception("Debug acquisition failed");
                yield return new WaitForSeconds(.5f);
                d.FoundationMode=true;d.UseAdaptation=false;d.Load(sample);d.Seek(1);yield return null;
                bool rejected=!d.Playing&&d.Status.Contains("owned by Debug");d.ResetInspectionPose();
                bool retained=d.Coordinator.GetOwner(head.Transform)==BoneOwner.Debug;
                File.WriteAllText(Path.Combine(folder,"result.json"),"{\"spatialRejected\":"+rejected.ToString().ToLowerInvariant()+",\"debugPreservedByReset\":"+retained.ToString().ToLowerInvariant()+"}");
            }
            finally{if(acquired)d.Coordinator.ReleaseDebug(head);d.ResetInspectionPose();}
        }
        public static IEnumerator Capture(MotionLabDriver d,string sample)
        {
            var folder=NewRun("capture");var camera=d.viewer;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var body=d.InspectionBodyFrame;
            var target=d.character.transform.position+body*new Vector3(0,.4f,0);
            var frames=new List<Captured>();var priorSpeed=d.PlaybackSpeed;
            bool priorSeparation=d.SeparateSourceInView;d.SeparateSourceInView=true;
            var p=UnityEngine.Object.FindFirstObjectByType<MoMaskPlayground>();if(p!=null&&p.Busy)throw new InvalidOperationException("Finish or cancel the current experiment before capture.");if(p!=null)p.InspectionBusy=true;
            try
            {
                foreach(var layer in new[]{MotionInspectionLayer.Foundation,MotionInspectionLayer.LegacyRaw,MotionInspectionLayer.LegacyCorrected})
                {
                    d.FoundationMode=layer==MotionInspectionLayer.Foundation;d.UseAdaptation=layer==MotionInspectionLayer.LegacyCorrected;
                    d.LatestCalibration=false;d.CurrentSegment=new MotionSegment();d.Continuous=false;d.ResetInspectionPose();d.Load(sample);
                    var output=Path.Combine(folder,layer.ToString());Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output,"rig.json"),d.RigSnapshot());
                    for(int frame=0;frame<d.CurrentClip.frames.Length;frame++)
                    {
                        d.Seek(frame/d.CurrentClip.fps);yield return new WaitForEndOfFrame();
                        foreach(var view in new[]{"front","side","back"})
                        {
                            var delta=view=="front"?new Vector3(0,.1f,2.6f):view=="back"?new Vector3(0,.1f,-2.6f):new Vector3(2.6f,.1f,0);
                            camera.transform.position=target+body*delta;camera.transform.LookAt(target);
                            camera.transform.position-=camera.transform.right*.375f;d.RefreshSourceView();
                            var path=Path.Combine(output,view);Directory.CreateDirectory(path);
                            MotionAdaptationProbe.SaveView(camera,Path.Combine(path,frame.ToString("D4")+".png"));
                        }
                        frames.Add(new Captured{layer=layer.ToString(),frame=frame,directionError=d.DirectionError,pose=d.AdaptationDiagnostic});
                    }
                }
                File.WriteAllText(Path.Combine(folder,"frames.json"),JsonUtility.ToJson(new CaptureReport{source=sample,frames=frames.ToArray()},true));
            }
            finally{d.ResetPose();d.Paused=false;d.PlaybackSpeed=priorSpeed;d.SeparateSourceInView=priorSeparation;camera.transform.SetPositionAndRotation(position,rotation);if(p!=null)p.InspectionBusy=false;}
        }
        [Serializable] sealed class Row{public string sample;public bool passed;public float directionError,boneLengthChange,repeatPosition,repeatAngle,neckRotationError;}
        [Serializable] sealed class ReplayRow{public int repeat,segments;public float seconds,resetPosition,resetAngle;public double span;public bool passed;public string status;}
        [Serializable] sealed class ReplayReport{public bool passed;public ReplayRow[] rows;}
        [Serializable] sealed class CheckReport{public bool passed;public float resetPosition,resetAngle,headNodDelta;public Row[] rows;}
        [Serializable] sealed class Captured{public string layer;public int frame;public float directionError;public MotionFrameDiagnostic pose;}
        [Serializable] sealed class CaptureReport{public string source;public int fps=20;public bool captureOverheadExcluded=true;public Captured[] frames;}
    }
}
#endif
