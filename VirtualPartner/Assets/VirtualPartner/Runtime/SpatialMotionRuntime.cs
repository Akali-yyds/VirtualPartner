using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SpatialMotionRuntime : MonoBehaviour
    {
        public SpatialRigProfile profile;
        public string LastDiagnostic { get; private set; }
        public bool HasPersistentPose { get { foreach(var x in active.Values) if(x.holding) return true; return false; } }
        public bool HasSupport => active.ContainsKey("support");
        public bool HasAny => active.Count != 0;
        public event Action<int,string> HeldMotionFailed;
        private ActionCoordinator coordinator;
        private AvatarPoseApplier applier;
        private Transform body;
        private Transform[] transforms;
        private readonly Dictionary<string, BoneMapInstance> bones = new Dictionary<string, BoneMapInstance>();
        private readonly Dictionary<string, Motion> active = new Dictionary<string, Motion>();
        private readonly Dictionary<string, string> failures = new Dictionary<string, string>();
        private readonly SpatialPoseBuffer pose = new SpatialPoseBuffer();
        private readonly Dictionary<string,GeometrySample> geometricResults = new Dictionary<string,GeometrySample>();
        private struct GeometrySample
        {
            public Vector3 target, actual;
            public float relativeError, orientationError;
            public bool oriented;
            public Transform tip;
            public Quaternion wantedOrientation;
            public float chainLength;
            public int frame;
            public override string ToString() => "target="+target.ToString("F4")+" actual="+actual.ToString("F4")+" relativeError="+relativeError.ToString("F5")+" orientationError="+(oriented?orientationError.ToString("F3"):"unconstrained");
        }
        private bool evaluatingLive;
        // Read after FinalizeFrame. Excludes stale/released samples from previous frames.
        public int MeasureFinalGeometry(out float positionError,out float orientationError)
        {
            positionError=orientationError=0;int count=0;
            foreach(var sample in geometricResults.Values)
            {
                if(sample.frame!=Time.frameCount||sample.tip==null||coordinator.GetOwner(sample.tip)!=BoneOwner.SpatialMotion)continue;
                count++;
                positionError=Mathf.Max(positionError,Vector3.Distance(sample.target,sample.tip.position)/Mathf.Max(.001f,sample.chainLength));
                if(sample.oriented)orientationError=Mathf.Max(orientationError,Quaternion.Angle(sample.wantedOrientation,sample.tip.rotation));
            }
            return count;
        }
        public string GeometryReport
        {
            get { var report=new StringBuilder("Last solved samples (may include released groups):\n");foreach(var sample in geometricResults)report.AppendLine(sample.Key+": "+sample.Value);return report.ToString(); }
        }
        private float height;
        private int sequence;
        private int thinkingRequest;
        private sealed class Motion
        {
            public string id, batch, group;
            public SpatialTrackDto track;
            public SpatialCompletionDto finish;
            public List<Transform> owned;
            public Dictionary<Transform,Quaternion> initial = new Dictionary<Transform,Quaternion>();
            public Dictionary<Transform,Vector3> initialPositions = new Dictionary<Transform,Vector3>();
            public Vector3 origin, initialTarget, initialHint, leftFoot, rightFoot;
            public Quaternion frame, leftFootRotation, rightFootRotation, initialTipRotation;
            public float elapsed, duration, remaining, restoreElapsed;
            public int request;
            public int atomicGroup;
            public Dictionary<Transform,Quaternion> restoreRotations;
            public Dictionary<Transform,Vector3> restorePositions;
            public bool holding, thinking;
            public Motion saved;
            public readonly Dictionary<string,Vector3[]> directValues = new Dictionary<string,Vector3[]>();
        }
        public void Configure(BoneMapProfile map, Transform root, Transform character, ActionCoordinator owner, AvatarPoseApplier avatar)
        {
            coordinator=owner; applier=avatar; body=character; transforms=root.GetComponentsInChildren<Transform>(true);
            bones.Clear(); var instances=new List<BoneMapInstance>(); map.BuildControlInstances(root,instances);
            foreach(var b in instances) bones[b.SemanticBone+":"+b.Side]=b;
            height=Mathf.Max(.1f,Vector3.Distance(B("Head"),B("Foot","L")));
            if(profile==null) profile=Resources.Load<SpatialRigProfile>("TokiSpatialRig");
            if(profile==null) { LastDiagnostic="Spatial rig calibration resource missing."; return; }
            pose.Capture(transforms,coordinator,true);
        }
        private Transform T(string bone,string side="None") => bones.TryGetValue(bone+":"+side,out var b)?b.Transform:null;
        private Vector3 B(string bone,string side="None") => T(bone,side).position;
        private List<Transform> GroupBones(string group)
        {
            var list=new List<Transform>();
            if(group=="leftArm" || group=="rightArm") { string s=group=="leftArm"?"L":"R"; foreach(var n in new[]{"Clavicle","UpperArm","Forearm","Hand"}) list.Add(T(n,s)); }
            else if(group=="support") { list.Add(T("Pelvis")); foreach(var s in new[]{"L","R"}) foreach(var n in new[]{"Thigh","Calf","Foot","Toe"}) list.Add(T(n,s)); }
            else foreach(var n in group=="head"?new[]{"Neck","Head"}:new[]{"Spine","Chest"}) list.Add(T(n));
            return list;
        }
        public bool TryStart(StagePlanActionDto action,string batch,out string error,bool thinking=false,int requestId=0)
        {
            error=null;
            if(!SpatialMotionContract.Validate(action,out error))return false;
            // Parent motion couples its children; independent limb tracks may succeed separately.
            bool coupled=Array.Exists(action.tracks,t=>t.group=="support"||t.group=="torso") ||
                (Array.Exists(action.tracks,t=>t.group=="head") && Array.Exists(action.tracks,t=>t.anchor=="head"));
            if(coupled || action.tracks.Length==1)return TryStartAtomic(action,batch,out error,thinking,requestId);
            bool accepted=false;var rejected=new List<string>();
            foreach(var track in action.tracks)
            {
                var part=new StagePlanActionDto{type=action.type,sync=action.sync,completion=action.completion,tracks=new[]{track}};
                if(TryStartAtomic(part,batch,out var reason,thinking,requestId))accepted=true;
                else rejected.Add(track.group+": "+reason);
            }
            if(rejected.Count>0){error=string.Join("; ",rejected);LastDiagnostic=error;failures[batch]=error;}
            return accepted;
        }
        private bool TryStartAtomic(StagePlanActionDto action,string batch,out string error,bool thinking,int requestId)
        {
            error=null;
            if(profile==null || transforms==null) { error="Spatial rig is not calibrated."; return false; }
            if(!SpatialMotionContract.Validate(action,out error))return false;
            var candidates=new List<Motion>();
            int atomicGroup=++sequence;
            pose.Capture(transforms,coordinator,true);
            foreach(var sourceTrack in action.tracks)
            {
                if (thinking && active.TryGetValue(sourceTrack.group, out var reserved) && !reserved.thinking)
                { error = sourceTrack.group + " is reserved by a formal motion."; return false; }
                var track=JsonUtility.FromJson<SpatialTrackDto>(JsonUtility.ToJson(sourceTrack));
                if (track.keys[0].time == 0) foreach(var key in track.keys) key.time += profile.returnSeconds;
                var owned=GroupBones(track.group);
                if(owned.Contains(null) || !coordinator.CanAcquireSpatial(owned,thinking,out error))return false;
                var m=new Motion { id=batch+":"+track.group+":"+(++sequence),batch=batch,group=track.group,track=track,
                    finish=SpatialMotionContract.ResolveCompletion(track,action.completion),owned=owned,origin=body.position,
                    frame=body.rotation*Quaternion.LookRotation(profile.bodyForward,profile.bodyUp),duration=track.keys[track.keys.Length-1].time,thinking=thinking };
                m.request=requestId;
                m.atomicGroup=atomicGroup;
                foreach(var t in transforms) { m.initial[t]=coordinator.ReadVisibleRotation(t); m.initialPositions[t]=coordinator.ReadVisiblePosition(t); }
                if(track.group=="leftArm"||track.group=="rightArm")
                {
                    var tip=T("Hand",track.group=="leftArm"?"L":"R");
                    m.initialTarget=Quaternion.Inverse(m.frame)*(pose.Position(tip)-Anchor(m))/height;
                    m.initialTipRotation=pose.Rotation(tip);
                    m.initialHint=Quaternion.Inverse(m.frame)*(pose.Position(T("Forearm",track.group=="leftArm"?"L":"R"))-Anchor(m))/height;
                }
                if(track.group=="support") {m.leftFoot=pose.Position(T("Foot","L"));m.rightFoot=pose.Position(T("Foot","R"));m.leftFootRotation=pose.Rotation(T("Foot","L"));m.rightFootRotation=pose.Rotation(T("Foot","R"));}
                candidates.Add(m);
            }
            // Validate the whole path at 60 Hz, including joint interpolation and IK.
            float duration=0;foreach(var m in candidates)duration=Mathf.Max(duration,m.duration);
            var preview=new List<Motion>(candidates);
            foreach(var existing in active.Values)
                if(!existing.thinking && !candidates.Exists(c=>c.group==existing.group))preview.Add(existing);
            preview.Sort((a,b)=>Order(a.group).CompareTo(Order(b.group)));
            int samples=Mathf.CeilToInt(duration*60);
            for(int i=0;i<=samples;i++)
            {
                pose.Capture(transforms,coordinator,true);
                foreach(var m in preview)
                {
                    float time=candidates.Contains(m)?i/60f:m.elapsed+(m.holding?0:i/60f);
                    if(!Evaluate(m,Mathf.Min(m.duration,time),out error)) { LastDiagnostic=error+" at t="+(i/60f).ToString("F3")+" group="+m.group; error=LastDiagnostic; return false; }
                }
            }
            foreach(var m in candidates)
            {
                if(active.TryGetValue(m.group,out var old))
                {
                    if(m.finish.mode=="restore" && old.holding) m.saved=old;
                    coordinator.ReleaseSpatial(old.id,0);
                }
                active[m.group]=m;
            }
            LastDiagnostic="Accepted "+batch; failures.Remove(batch); return true;
        }
        private static int Order(string group)=>group=="support"?0:group=="torso"?1:group=="head"?2:3;
        private Vector3 Anchor(Motion m)
        {
            if(m.track.anchor=="head")return pose.Position(T("Head"));
            if(m.track.anchor=="chest")return pose.Position(T("Chest"));
            if(m.track.anchor=="shoulder")return pose.Position(T("UpperArm",m.group=="leftArm"?"L":"R"));
            return m.origin;
        }
        private Vector3 Target(Motion m,float time,out SpatialKeyDto key,out float blend)
        {
            var keys=m.track.keys; int index=0;while(index<keys.Length-1 && time>keys[index].time)index++;
            key=keys[index]; float before=index==0?0:keys[index-1].time, span=key.time-before;
            blend=Mathf.Clamp01((time-before)/span);
            Vector3 from=index==0?m.initialTarget:V(keys[index-1].position),to=V(key.position);
            Vector3 v0=index==0||keys[index-1].stop?Vector3.zero:(to-(index==1?m.initialTarget:V(keys[index-2].position)))/(key.time-(index==1?0:keys[index-2].time));
            Vector3 v1=index==keys.Length-1||key.stop?Vector3.zero:(V(keys[index+1].position)-from)/(keys[index+1].time-before);
            return SpatialPoseBuffer.Hermite(from,to,v0,v1,blend,span);
        }
        private static Vector3 V(StagePlanRotationDto v)=>v==null?Vector3.zero:v.ToVector3();
        private SpatialLimbCalibration Limb(string group)=>Array.Find(profile.limbs,x=>x.group==group);
        private bool Evaluate(Motion m,float time,out string error)
        {
            error=null;
            foreach(var t in m.owned)pose.SetLocal(t,m.initial[t],m.initialPositions[t]);
            var target=Target(m,time,out var key,out var blend);
            float total=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/m.duration));
            if(m.group=="leftArm"||m.group=="rightArm")
            {
                string side=m.group=="leftArm"?"L":"R"; var calibration=Limb(m.group);
                if(calibration==null){error="Missing limb calibration: "+m.group;return false;}
                Vector3 goal=Anchor(m)+m.frame*target*height;
                Vector3 wantedHint=V(key.hint).sqrMagnitude>.000001f ? V(key.hint) : m.initialHint;
                int hintIndex=Array.IndexOf(m.track.keys,key);var previousHint=hintIndex==0?m.initialHint:V(m.track.keys[hintIndex-1].hint);
                if(previousHint.sqrMagnitude<.000001f)previousHint=m.initialHint;
                Vector3 hint=Anchor(m)+m.frame*Vector3.Lerp(previousHint,wantedHint,Mathf.SmoothStep(0,1,blend))*height;
                Quaternion? orientation=null;
                if(V(key.palm).sqrMagnitude>.000001f)
                {
                    var local=Quaternion.LookRotation(calibration.palmLocal,calibration.fingersLocal);
                    var wanted=m.frame*Quaternion.LookRotation(V(key.palm),V(key.fingers))*Quaternion.Inverse(local);
                    int i=Array.IndexOf(m.track.keys,key);
                    Quaternion from=m.initialTipRotation;
                    if(i>0 && V(m.track.keys[i-1].palm).sqrMagnitude>.000001f) from=m.frame*Quaternion.LookRotation(V(m.track.keys[i-1].palm),V(m.track.keys[i-1].fingers))*Quaternion.Inverse(local);
                    orientation=Quaternion.Slerp(from,wanted,Mathf.SmoothStep(0,1,blend));
                }
                if(!pose.SolveLimb(T("UpperArm",side),T("Forearm",side),T("Hand",side),goal,hint,orientation,calibration,profile,out var residual,out error))return false;
                if(evaluatingLive)geometricResults[m.group]=new GeometrySample{target=goal,actual=pose.Position(T("Hand",side)),relativeError=residual,oriented=orientation.HasValue,orientationError=orientation.HasValue?Quaternion.Angle(pose.Rotation(T("Hand",side)),orientation.Value):0,tip=T("Hand",side),wantedOrientation=orientation??Quaternion.identity,chainLength=calibration.upperLength+calibration.lowerLength,frame=Time.frameCount};
                // Torso capsule is intentionally conservative; detailed hand/cloth contact is out of scope.
                Vector3 low=pose.Position(T("Pelvis")),high=pose.Position(T("Chest"));
                var elbow=pose.Position(T("Forearm",side));var hand=pose.Position(T("Hand",side));
                for(int i=0;i<=8;i++)
                {
                    var point=Vector3.Lerp(elbow,hand,i/8f);
                    if(SpatialPoseBuffer.SegmentDistance(point,low,high)<height*profile.torsoClearance)
                    {error="Forearm/hand intersects torso clearance.";return false;}
                    if(Vector3.Distance(point,pose.Position(T("Head")))<height*profile.headClearance)
                    {error="Forearm/hand intersects head clearance.";return false;}
                }
            }
            else if(m.group=="support")
            {
                if(target.magnitude>profile.maxPelvisOffset){error="Pelvis offset exceeds shallow-pose range.";return false;}
                pose.SetPosition(T("Pelvis"),pose.Position(T("Pelvis"))+m.frame*target*height);
                if(!ApplyDirect(m,key,time,out error))return false;
                if(!SolveSupport(m,out error))return false;
            }
            else if(!ApplyDirect(m,key,time,out error))return false;
            return true;
        }
        private bool SolveSupport(Motion m,out string error)
        {
            error=null;
                foreach(var side in new[]{"L","R"})
                {
                    var calibration=Limb(side=="L"?"leftLeg":"rightLeg");
                    if(calibration==null){error="Missing leg calibration.";return false;}
                    var goal=side=="L"?m.leftFoot:m.rightFoot;
                    var rotation=side=="L"?m.leftFootRotation:m.rightFootRotation;
                    if(!pose.SolveLimb(T("Thigh",side),T("Calf",side),T("Foot",side),goal,pose.Position(T("Thigh",side))+m.frame*Vector3.forward*height,rotation,calibration,profile,out var residual,out error))return false;
                    if(residual>profile.footTolerance){error="Foot support drift exceeds tolerance.";return false;}
                    if(evaluatingLive)geometricResults["foot"+side]=new GeometrySample{target=goal,actual=pose.Position(T("Foot",side)),relativeError=residual,tip=T("Foot",side),chainLength=calibration.upperLength+calibration.lowerLength,frame=Time.frameCount};
                }
            return true;
        }
        private bool ApplyDirect(Motion m,SpatialKeyDto key,float time,out string error)
        {
            error=null;
            // Interpolate each joint across the full track. Omitted later joints retain their
            // preceding value rather than snapping back to the action's initial pose.
            foreach(var calibration in profile.joints)
            {
                if(calibration.side!="None" || !SpatialMotionContract.AllowsBone(m.group,calibration.bone))continue;
                var joint=T(calibration.bone);
                if(!m.directValues.TryGetValue(calibration.bone,out var values))
                {
                    bool specified=false;
                    foreach(var k in m.track.keys)if(k.bones!=null && Array.Exists(k.bones,b=>b.bone==calibration.bone))specified=true;
                    if(!specified){m.directValues[calibration.bone]=Array.Empty<Vector3>();continue;}
                    Vector3 initial=(Quaternion.Inverse(calibration.restRotation)*m.initial[joint]).eulerAngles;
                    initial=new Vector3(Mathf.DeltaAngle(0,initial.x),Mathf.DeltaAngle(0,initial.y),Mathf.DeltaAngle(0,initial.z));
                    values=new Vector3[m.track.keys.Length+1];values[0]=initial;
                    for(int i=0;i<m.track.keys.Length;i++)
                    {
                        var entry=m.track.keys[i].bones==null?null:Array.Find(m.track.keys[i].bones,b=>b.bone==calibration.bone);
                        values[i+1]=entry==null?values[i]:V(entry.rotation);
                    }
                    m.directValues[calibration.bone]=values;
                }
                if(values.Length==0)continue;
                int index=Array.IndexOf(m.track.keys,key),end=index+1;
                float previous=index==0?0:m.track.keys[index-1].time,span=key.time-previous;
                var v0=index==0||m.track.keys[index-1].stop?Vector3.zero:(values[end]-values[end-2])/(key.time-(index==1?0:m.track.keys[index-2].time));
                var v1=end==m.track.keys.Length||key.stop?Vector3.zero:(values[end+1]-values[end-1])/(m.track.keys[index+1].time-previous);
                var desired=SpatialPoseBuffer.Hermite(values[end-1],values[end],v0,v1,Mathf.Clamp01((time-previous)/span),span);
                var limited=new Vector3(Mathf.Clamp(desired.x,calibration.minimum.x,calibration.maximum.x),Mathf.Clamp(desired.y,calibration.minimum.y,calibration.maximum.y),Mathf.Clamp(desired.z,calibration.minimum.z,calibration.maximum.z));
                float correction=Quaternion.Angle(Quaternion.Euler(desired),Quaternion.Euler(limited));
                if(correction>profile.maximumOrientationCorrection || correction>profile.orientationTolerance){error="Joint rotation exceeds calibrated range: "+calibration.bone;return false;}
                pose.SetLocal(joint,calibration.restRotation*Quaternion.Euler(limited),pose.LocalPosition(joint));
            }
            return true;
        }
        public void Tick(float dt)
        {
            if(profile==null||transforms==null)return;
            evaluatingLive=true;
            pose.Capture(transforms,coordinator,false);
            var list=new List<Motion>(active.Values); list.Sort((a,b)=>Order(a.group).CompareTo(Order(b.group)));
            foreach(var m in list)
            {
                if(!active.TryGetValue(m.group,out var current)||current!=m)continue;
                if(!coordinator.CanAcquireSpatial(m.owned,m.thinking,out var reason)) { Fail(m,reason);continue; }
                if(m.holding)
                {
                    if(m.restoreRotations==null && m.finish.mode=="timed") {m.remaining-=dt;if(m.remaining<=0){Finish(m);continue;}}
                }
                else m.elapsed=Mathf.Min(m.duration,m.elapsed+dt);
                if(!Evaluate(m,m.elapsed,out reason)){Fail(m,reason);continue;}
                if(m.restoreRotations!=null)
                {
                    m.restoreElapsed+=dt;float blend=Mathf.SmoothStep(0,1,m.restoreElapsed/profile.returnSeconds);
                    foreach(var bone in m.owned)pose.SetLocal(bone,Quaternion.Slerp(m.restoreRotations[bone],pose.LocalRotation(bone),blend),Vector3.Lerp(m.restorePositions[bone],pose.LocalPosition(bone),blend));
                    if(m.group=="support" && !SolveSupport(m,out reason)){Fail(m,reason);continue;}
                    if(m.restoreElapsed>=profile.returnSeconds){m.restoreRotations=null;m.restorePositions=null;}
                }
                bool changed = false;
                foreach(var bone in m.owned)
                {
                    changed |= Quaternion.Angle(coordinator.ReadVisibleRotation(bone),pose.LocalRotation(bone)) > .01f || Vector3.Distance(coordinator.ReadVisiblePosition(bone),pose.LocalPosition(bone)) > .00001f;
                    coordinator.WriteSpatial(m.id,bone,pose.LocalRotation(bone),pose.LocalPosition(bone),m.thinking);
                }
                if(changed && m.request!=0) GetComponent<PerformanceLatency>()?.Mark(m.request,m.thinking ? "firstThinkingWrite" : "firstSpatialWrite");
                if(!m.holding && m.elapsed>=m.duration)
                {
                    if(m.finish.mode=="hold"||m.finish.mode=="timed"){m.holding=true;m.remaining=m.finish.seconds;}
                    else Finish(m);
                }
            }
            evaluatingLive=false;
        }
        private void Fail(Motion m,string reason)
        {
            LastDiagnostic=m.group+": "+reason;failures[m.batch]=LastDiagnostic;
            if(m.holding && !m.thinking && m.request>0) HeldMotionFailed?.Invoke(m.request,LastDiagnostic);
            foreach(var affected in new List<Motion>(active.Values))
                if(affected==m || affected.atomicGroup==m.atomicGroup)
                { affected.saved=null;Finish(affected); }
        }
        private void Finish(Motion m)
        {
            coordinator.ReleaseSpatial(m.id,profile.returnSeconds);active.Remove(m.group);
            if(m.saved!=null)
            {
                var old=m.saved; m.saved=null;
                old.id="restore:"+(++sequence);old.restoreElapsed=0;
                old.restoreRotations=new Dictionary<Transform,Quaternion>();old.restorePositions=new Dictionary<Transform,Vector3>();
                foreach(var t in old.owned){old.restoreRotations[t]=coordinator.ReadVisibleRotation(t);old.restorePositions[t]=coordinator.ReadVisiblePosition(t);}
                active[old.group]=old;
            }
        }
        public bool IsTransitioning(string batch) { foreach(var m in active.Values)if(m.batch==batch&&(!m.holding||m.restoreRotations!=null))return true;return false; }
        public string Failure(string batch)=>failures.TryGetValue(batch,out var reason)?reason:null;
        public void CancelMoving()
        { foreach(var m in new List<Motion>(active.Values))if(!m.holding&&!m.thinking){m.saved=null;Finish(m);} }
        public void ResetPose(string group="all")
        {foreach(var m in new List<Motion>(active.Values))if(group=="all"||m.group==group){m.saved=null;Finish(m);} }
        public void YieldBones(IReadOnlyList<Transform> requested)
        {foreach(var m in new List<Motion>(active.Values))foreach(var t in requested)if(m.owned.Contains(t)){m.saved=null;Finish(m);break;} }
        public string Describe()
        {
            var s=new StringBuilder("Current physical pose state (runtime truth):\n");
            bool formal=false;
            foreach(var m in active.Values)if(!m.thinking)
            {
                formal=true;
                s.AppendLine(m.group+": "+(m.holding?m.finish.mode:"moving")+", remaining="+m.remaining.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"; anchor="+m.track.anchor+"; finalTarget="+JsonUtility.ToJson(m.track.keys[m.track.keys.Length-1])+"; savedHold="+(m.saved!=null));
            }
            if(!formal)s.AppendLine("No formal spatial holds.");
            if(profile!=null && transforms!=null)
            {
                s.AppendLine("Spatial reach capabilities (distances normalized by head-to-foot height):");
                foreach(var limb in profile.limbs) s.AppendLine(limb.group+" total reach="+((limb.upperLength+limb.lowerLength)/height).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                var frame=body.rotation*Quaternion.LookRotation(profile.bodyForward,profile.bodyUp);
                foreach(var name in new[]{"Head","Chest","Pelvis"})s.AppendLine(name+" body position="+(Quaternion.Inverse(frame)*(T(name).position-body.position)/height).ToString("F3"));
                foreach(var side in new[]{"L","R"})s.AppendLine("Shoulder "+side+" body position="+(Quaternion.Inverse(frame)*(T("UpperArm",side).position-body.position)/height).ToString("F3"));
                if(Camera.main!=null)s.AppendLine("Conversation camera body position="+(Quaternion.Inverse(frame)*(Camera.main.transform.position-body.position)/height).ToString("F3"));
                foreach(var group in SpatialMotionContract.Groups)
                    if(!coordinator.CanAcquireSpatial(GroupBones(group),false,out var reason))s.AppendLine(group+" unavailable: "+reason);
            }
            return s.ToString();
        }
        public void BeginThinking(int request)
        {
            EndThinking(thinkingRequest);thinkingRequest=request;
            if(profile==null)return;
            foreach(var track in profile.thinkingTracks)
                TryStart(new StagePlanActionDto{type="spatialPose",tracks=new[]{track},completion=new SpatialCompletionDto{mode="hold"}},"thinking:"+request,out _,true,request);
        }
        public void EndThinking(int request)
        {if(request!=thinkingRequest)return;foreach(var m in new List<Motion>(active.Values))if(m.thinking){m.saved=null;Finish(m);}thinkingRequest=0;}
        private void OnDisable(){if(coordinator!=null&&profile!=null)ResetPose();}
    }
}
