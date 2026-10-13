using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    // Position-only diagnostic retargeter. Wrist twist is unobserved and is deliberately not scored as correct.
    public sealed class MotionLabRetargeter
    {
        readonly Transform[] rig;
        readonly Transform[] mapped = new Transform[22];
        readonly Quaternion[] restRotations;
        readonly Vector3[] restPositions;
        readonly SpatialPoseBuffer pose = new SpatialPoseBuffer();
        readonly ActionCoordinator owner;
        readonly Quaternion bodyFrame;
        readonly Quaternion bindingBodyFrame;
        public Quaternion BindingBodyFrame=>bindingBodyFrame;
        public Quaternion ReferenceBodyFrame=>Foundation&&!HistoricalFoundationBasis?bindingBodyFrame:bodyFrame;
        readonly Vector3 pelvisOrigin;
        readonly float legLength;
        readonly Quaternion pelvisRest;
        readonly Dictionary<Transform,int> index=new Dictionary<Transform,int>();
        readonly Dictionary<Transform,Quaternion> entryRotations=new Dictionary<Transform,Quaternion>();
        readonly Dictionary<Transform,Vector3> entryPositions=new Dictionary<Transform,Vector3>();
        readonly Dictionary<int,int> aimChild=new Dictionary<int,int>{{1,4},{2,5},{3,9},{4,7},{5,8},{7,10},{8,11},{9,12},{12,15},{13,16},{14,17},{16,18},{17,19},{18,20},{19,21}};
        float sourceScale;
        public float SourceScale=>sourceScale;
        Vector3 sourceOrigin;
        public Vector3[] LastSource {get;private set;}
        public Quaternion LastRootRotation {get;private set;}=Quaternion.identity;
        public float MaxDirectionError {get;private set;}
        public float MaxSettledDirectionError {get;private set;}
        public float MaxBoneLengthChange {get;private set;}
        public float MaxFootTravel {get;private set;}
        public float MaxSourceFootTravel {get;private set;}
        public int TorsoIntersectionSamples {get;private set;}
        Vector3 sourceLeftFoot,sourceRightFoot;
        Vector3 leftFootStart,rightFootStart;
        bool measured;
        public bool Adapted;
        public bool Foundation;
        public int FoundationVersion=3;
        public Vector3 SourceNeckBinding;
        public bool HistoricalFoundationBasis=>FoundationVersion==1;
        public string ProcessingLayer=>Foundation?("foundation-v"+FoundationVersion+(FoundationVersion>=3?"-relative-neck":FoundationVersion==2?"-binding-basis":"-historical")):Adapted?"legacy-corrected":"legacy-raw";
        bool RelativeNeck=>Foundation&&FoundationVersion>=3;
        Quaternion expectedNeckRotation;
        public string RootReference {get;private set;}="unavailable";
        public Quaternion SourcePlacement=>Foundation||Adapted?segmentFrame:bodyFrame;
        public MotionAdaptationProfile Calibration=MotionAdaptationProfile.Load();
        public MotionSegment Segment=new MotionSegment();
        readonly HashSet<string> playbackParts=new HashSet<string>();
        bool pendingTakeover;
        public string[] PlaybackParts=>new List<string>(playbackParts).ToArray();
        bool Plays(string group)=>playbackParts.Contains(group);
        public MotionFrameDiagnostic Diagnostic {get;private set;}=new MotionFrameDiagnostic();
        readonly Quaternion headRest;
        readonly SpatialRigProfile solveLimits;
        readonly SpatialLimbCalibration limbLimits=new SpatialLimbCalibration{minBend=2,maxBend=155};
        readonly Dictionary<string,Held> holds=new Dictionary<string,Held>();
        Vector3 segmentOrigin;
        Quaternion segmentFrame;
        float correction,penetration,footCorrection;
        readonly Vector3[] footAnchors=new Vector3[2];
        readonly bool[] footContact=new bool[2];
        readonly Dictionary<Transform,Quaternion> previousOutput=new Dictionary<Transform,Quaternion>();
        readonly Dictionary<Transform,Vector3> angularVelocity=new Dictionary<Transform,Vector3>();
        readonly Dictionary<Transform,Vector3> entryVelocity=new Dictionary<Transform,Vector3>();
        double lastOutputTime;
        public bool HasHolds=>holds.Count>0;
        public string DescribeHolds()=>string.Join("; ",System.Linq.Enumerable.Select(holds,h=>h.Key+": "+h.Value.intent+" ("+(h.Value.remaining<0?"infinite":h.Value.remaining.ToString("F1")+"s")+")"));
        sealed class Held{public string id,intent;public float remaining;public Vector3 target,hint;public Dictionary<int,Quaternion> rotations=new Dictionary<int,Quaternion>();public Dictionary<int,Vector3> positions=new Dictionary<int,Vector3>();}

        public MotionLabRetargeter(Dictionary<string,Transform> bones,Transform root,Transform character,SpatialRigProfile profile,ActionCoordinator coordinator)
        {
            owner=coordinator;rig=root.GetComponentsInChildren<Transform>(true);
            restRotations=new Quaternion[rig.Length];restPositions=new Vector3[rig.Length];
            for(int i=0;i<rig.Length;i++){restRotations[i]=rig[i].localRotation;restPositions[i]=rig[i].localPosition;index[rig[i]]=i;}
            for(int i=0;i<22;i++) if(MotionLabSkeleton.Bones[i]!="") mapped[i]=bones[MotionLabSkeleton.Bones[i]];
            bodyFrame=character.rotation*Quaternion.LookRotation(profile.bodyForward,profile.bodyUp);
            // Capture once from the model binding, before Idle or any playback.
            // The target's anatomical R-L axis fixes the sign; never use a
            // generated head/toe frame to decide a whole-clip flip.
            var bindRight=Vector3.ProjectOnPlane(mapped[2].position-mapped[1].position,character.up);
            if(bindRight.sqrMagnitude<.000001f)throw new ArgumentException("Target binding has no reliable bilateral hip axis");
            bindingBodyFrame=Quaternion.LookRotation(Vector3.Cross(bindRight.normalized,character.up),character.up);
            pelvisOrigin=mapped[0].position;pelvisRest=mapped[0].rotation;
            legLength=Vector3.Distance(mapped[1].position,mapped[4].position)+Vector3.Distance(mapped[4].position,mapped[7].position);
            headRest=mapped[15].rotation;segmentOrigin=pelvisOrigin;segmentFrame=bodyFrame;
            solveLimits=ScriptableObject.CreateInstance<SpatialRigProfile>();solveLimits.maximumPositionCorrection=.5f;solveLimits.positionTolerance=.05f;
        }
        public void Begin(MotionLabClip clip,bool continuous=false)
        {
            clip.Validate();var p=clip.frames[0].positions;
            playbackParts.Clear();foreach(var group in Segment.ResolvePlaybackParts(holds.Keys))playbackParts.Add(group);
            pendingTakeover=Adapted;
            float length=Vector3.Distance(p[1],p[4])+Vector3.Distance(p[4],p[7]);
            if(length<.01f)throw new ArgumentException("Degenerate source legs.");
            sourceScale=legLength/length;sourceOrigin=p[0];
            MaxSettledDirectionError=MaxDirectionError=MaxBoneLengthChange=MaxFootTravel=MaxSourceFootTravel=0;TorsoIntersectionSamples=0;measured=false;
            sourceLeftFoot=p[7];sourceRightFoot=p[8];
            entryRotations.Clear();entryPositions.Clear();foreach(var t in mapped)if(t!=null){entryRotations[t]=owner.ReadVisibleRotation(t);entryPositions[t]=owner.ReadVisiblePosition(t);}
            entryVelocity.Clear();foreach(var t in mapped)if(t!=null)entryVelocity[t]=continuous&&Time.realtimeSinceStartupAsDouble-lastOutputTime<.15&&angularVelocity.TryGetValue(t,out var velocity)?velocity:Vector3.zero;
            if(Adapted)
            {
                segmentOrigin=continuous?mapped[0].position:pelvisOrigin;
                // Independent MoMask clips use their own vertical origin. Carry
                // horizontal placement/heading, but do not add each clip's
                // final crouch/bounce offset to the next clip's standing height.
                segmentOrigin.y=pelvisOrigin.y;
                // Relative source heading starts at the actual current target heading.
                var pRight=p[2]-p[1];var pUp=p[9]-p[0];var sourceHeading=Quaternion.LookRotation(Vector3.Cross(pRight,pUp),pUp);
                if(Foundation)sourceHeading=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.Cross(pRight,pUp),Vector3.up),Vector3.up);
                if(Foundation&&clip.hasRootOrientation)sourceHeading=clip.frames[0].rootRotation;
                var currentForward=mapped[0].rotation*Quaternion.Inverse(pelvisRest)*(ReferenceBodyFrame*Vector3.forward);
                var targetHeading=Quaternion.LookRotation(Vector3.ProjectOnPlane(currentForward,Vector3.up).normalized,Vector3.up);
                segmentFrame=targetHeading*Quaternion.Inverse(sourceHeading);
            }
            Array.Clear(footContact,0,2);
        }
        void NormalizeLocal(Transform bone)=>pose.SetLocal(bone,pose.LocalRotation(bone).normalized,pose.LocalPosition(bone));
        public bool Apply(MotionLabClip clip,float seconds,string id,out string reason)
        {
            var owned=new List<Transform>();for(int i=0;i<mapped.Length;i++)if(mapped[i]!=null&&(!Adapted||Plays(MotionSegment.Group(i))))owned.Add(mapped[i]);
            if(!owner.CanAcquireSpatial(owned,false,out reason))return false;
            float frame=Mathf.Clamp(seconds*clip.fps,0,clip.frames.Length-1);int a=(int)frame,b=Mathf.Min(a+1,clip.frames.Length-1);
            LastSource=new Vector3[22];for(int i=0;i<22;i++)LastSource[i]=Vector3.Lerp(clip.frames[a].positions[i],clip.frames[b].positions[i],frame-a);
            correction=penetration=footCorrection=0;
            // Unmapped ancestors (notably Bip001) continue to be sampled by Idle. The FK
            // preview must use their actual current transform, not an unapplied rest pose.
            pose.Capture(rig,owner,false);
            // Normalize accumulated products before FK uses Unity TRS's unit tolerance.
            foreach(var t in rig)NormalizeLocal(t);
            foreach(var t in owned){int i=index[t];pose.SetLocal(t,restRotations[i].normalized,restPositions[i]);}
            Vector3 right=LastSource[2]-LastSource[1],up=LastSource[9]-LastSource[0];
            Vector3 forward=Vector3.Cross(right,up);
            if(forward.sqrMagnitude<.000001f){reason="Degenerate torso basis.";return false;}
            var sourceFrame=Quaternion.LookRotation(forward,up);
            var rootFrame=Foundation?(clip.hasRootOrientation?Quaternion.Slerp(clip.frames[a].rootRotation,clip.frames[b].rootRotation,frame-a):Quaternion.LookRotation(Vector3.ProjectOnPlane(forward,Vector3.up),Vector3.up)):sourceFrame;
            LastRootRotation=rootFrame;
            RootReference=Foundation&&clip.hasRootOrientation?(clip.fixture?"fixture root":"decoded yaw"):"hip reference";
            var frameRotation=Adapted?segmentFrame:bodyFrame;
            if(!Adapted||Plays("support")){
            pose.SetRotation(mapped[0],frameRotation*rootFrame*Quaternion.Inverse(ReferenceBodyFrame)*pelvisRest);
            NormalizeLocal(mapped[0]);
            pose.SetPosition(mapped[0],(Adapted?segmentOrigin:pelvisOrigin)+frameRotation*((LastSource[0]-sourceOrigin)*sourceScale));}
            foreach(var pair in aimChild)
            {
                var joint=mapped[pair.Key];var child=mapped[pair.Value];
                if(Adapted&&!Plays(MotionSegment.Group(pair.Key)))continue;
                if(RelativeNeck&&pair.Key==12)
                {
                    // HumanML Head is an anterior skull landmark; Toki Head is
                    // a pivot above its neck. Match motion relative to the
                    // independently recorded source binding, not these unlike
                    // bone directions. Face twist remains unobserved.
                    var chestUp=LastSource[12]-LastSource[9];
                    var chestForward=Vector3.Cross(LastSource[17]-LastSource[16],chestUp);
                    if(chestForward.sqrMagnitude<.0000001f||SourceNeckBinding.sqrMagnitude<.5f){reason="Missing or degenerate calibrated neck reference";return false;}
                    var sourceChest=Quaternion.LookRotation(chestForward,chestUp);
                    var localDirection=Quaternion.Inverse(sourceChest)*(LastSource[15]-LastSource[12]).normalized;
                    var swing=Quaternion.FromToRotation(SourceNeckBinding,localDirection);
                    var targetChest=pose.Rotation(mapped[9])*Quaternion.Inverse(restWorld(9))*ReferenceBodyFrame;
                    expectedNeckRotation=targetChest*swing*Quaternion.Inverse(targetChest)*pose.Rotation(joint);
                    pose.SetRotation(joint,expectedNeckRotation);NormalizeLocal(joint);
                    continue;
                }
                var target=frameRotation*(LastSource[pair.Value]-LastSource[pair.Key]);
                var current=pose.Position(child)-pose.Position(joint);
                if(target.sqrMagnitude<.0000001f || current.sqrMagnitude<.0000001f){reason="Degenerate source/target segment.";return false;}
                pose.SetRotation(joint,Quaternion.FromToRotation(current,target)*pose.Rotation(joint));
                NormalizeLocal(joint);
            }
            // Foundation observes neck->head direction only. Head local bind
            // rotation is retained; there is no measured face/palm twist in RIC.
            if(Adapted&&!Foundation&&Plays("head"))
            {
                var neckDirection=Quaternion.Inverse(sourceFrame)*(LastSource[15]-LastSource[12]).normalized;
                var swing=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(Calibration.sourceNeutralNeck.normalized,neckDirection),Calibration.headSwing);
                var desired=frameRotation*sourceFrame*swing*Quaternion.Inverse(bodyFrame)*headRest;
                if(Segment.headTarget!="none"&&!string.IsNullOrEmpty(Segment.headTarget))
                {
                    var yaw=Segment.headTarget=="left"?-Calibration.headYaw:Segment.headTarget=="right"?Calibration.headYaw:0;
                    var pitch=Segment.headTarget=="down"?25:Segment.headTarget=="up"?-20:0;
                    desired=bodyFrame*Quaternion.Euler(pitch,yaw,0)*Quaternion.Inverse(bodyFrame)*headRest;
                }
                pose.SetRotation(mapped[15],desired);NormalizeLocal(mapped[15]);
            }
            float u=Mathf.Clamp01(seconds/(Adapted?Calibration.blendSeconds:.25f));
            float blend=Adapted?u*u*u*(10+u*(-15+6*u)):Mathf.SmoothStep(0,1,u);
            foreach(var joint in owned)
            {
                var extrapolated=entryRotations[joint];
                if(Adapted&&entryVelocity.TryGetValue(joint,out var v)&&v.sqrMagnitude>.00001f)extrapolated=Quaternion.AngleAxis(v.magnitude*seconds,v.normalized)*extrapolated;
                pose.SetLocal(joint,Quaternion.Slerp(extrapolated,pose.LocalRotation(joint),blend),Vector3.Lerp(entryPositions[joint],pose.LocalPosition(joint),blend));
            }
            if(Adapted&&!Foundation)
            {
                if(Plays("leftArm"))CorrectArm(16,18,20);
                if(Plays("rightArm"))CorrectArm(17,19,21);
                if(Plays("support"))CorrectFeet(clip,a,seconds);
            }
            foreach(var joint in owned) if(!owner.WriteSpatial(id,joint,pose.LocalRotation(joint).normalized,pose.LocalPosition(joint),false))
            {reason="Ownership changed during retargeting.";owner.ReleaseSpatial(id,.35f);return false;}
            // Retire only after the candidate successfully wrote the entire group.
            // ReleaseSpatial checks identity, so it cannot release the new pose.
            if(pendingTakeover){foreach(var group in Segment.parts)ReleaseHold(group);pendingTakeover=false;}
            var now=Time.realtimeSinceStartupAsDouble;float dt=(float)(now-lastOutputTime);
            foreach(var joint in owned)
            {
                var rotation=pose.LocalRotation(joint).normalized;
                if(dt>.0001f&&dt<.15f&&previousOutput.TryGetValue(joint,out var previous))
                {var delta=rotation*Quaternion.Inverse(previous);delta.ToAngleAxis(out var angle,out var axis);if(angle>180)angle-=360;angularVelocity[joint]=Mathf.Abs(angle)<.0001f?Vector3.zero:Vector3.ClampMagnitude(axis*(angle/dt),360);}
                previousOutput[joint]=rotation;
            }
            lastOutputTime=now;
            return true;
        }
        Vector3 PushOut(Vector3 point,out float depth)
        {
            depth=0;var result=point;
            foreach(var e in Calibration.envelopes)
            {
                var basis=pose.Rotation(mapped[e.bone])*Quaternion.Inverse(restWorld(e.bone))*bodyFrame;
                var center=pose.Position(mapped[e.bone])+basis*e.offset;
                var local=Quaternion.Inverse(basis)*(result-center);
                var radius=e.radii+Vector3.one*(Calibration.armRadius+Calibration.clearance);
                var scaled=new Vector3(local.x/radius.x,local.y/radius.y,local.z/radius.z);
                if(scaled.magnitude>=1)continue;
                if(scaled.sqrMagnitude<.000001f)scaled=Vector3.forward;
                var boundary=Vector3.Scale(scaled.normalized,radius);
                var delta=basis*(boundary-local);depth=Mathf.Max(depth,delta.magnitude);result+=delta;
            }
            return result;
        }
        Quaternion restWorld(int i)
        {
            var t=mapped[i];Quaternion q=restRotations[index[t]];
            while(t.parent!=null){t=t.parent;q=(index.ContainsKey(t)?restRotations[index[t]]:t.localRotation)*q;}
            return q;
        }
        void CorrectArm(int upper,int mid,int tip)
        {
            var initial=pose.Position(mapped[tip]);var target=initial;var hint=pose.Position(mapped[mid]);
            for(int pass=0;pass<Calibration.correctionIterations;pass++)
            {
                var wrist=pose.Position(mapped[tip]);var elbow=pose.Position(mapped[mid]);
                var pushed=PushOut(wrist,out var wd);var pushedElbow=PushOut(elbow,out var ed);
                var midpoint=(wrist+elbow)*.5f;var pm=PushOut(midpoint,out var md);
                if(Mathf.Max(wd,ed,md)<.0005f)break;
                target=wrist+(pushed-wrist)+(pm-midpoint);hint=elbow+(pushedElbow-elbow)+(pm-midpoint);
                target=initial+Vector3.ClampMagnitude(target-initial,Calibration.maximumArmCorrection);
                var root=pose.Position(mapped[upper]);var reach=Vector3.Distance(root,elbow)+Vector3.Distance(elbow,wrist);
                target=root+Vector3.ClampMagnitude(target-root,reach*.995f);
                pose.SolveLimb(mapped[upper],mapped[mid],mapped[tip],target,hint,null,limbLimits,solveLimits,out _,out _);
                NormalizeLocal(mapped[upper]);NormalizeLocal(mapped[mid]);
            }
            // Resolve elbow-plane ambiguity by comparing nearby bend directions.
            // Candidate evaluation stays in the detached buffer; no speculative scene writes.
            var baseUpper=pose.LocalRotation(mapped[upper]);var baseMid=pose.LocalRotation(mapped[mid]);
            var bestUpper=baseUpper;var bestMid=baseMid;float best=ArmPenetration(mid,tip);
            var rootPoint=pose.Position(mapped[upper]);var wristTarget=pose.Position(mapped[tip]);var bend=pose.Position(mapped[mid])-rootPoint;
            if(best>.001f)
            foreach(float angle in new[]{-30f,30f,-60f,60f,-90f,90f})
            {
                pose.SetLocal(mapped[upper],baseUpper,pose.LocalPosition(mapped[upper]));pose.SetLocal(mapped[mid],baseMid,pose.LocalPosition(mapped[mid]));
                var pole=rootPoint+Quaternion.AngleAxis(angle,(wristTarget-rootPoint).normalized)*bend;
                pose.SolveLimb(mapped[upper],mapped[mid],mapped[tip],wristTarget,pole,null,limbLimits,solveLimits,out _,out _);
                float score=ArmPenetration(mid,tip)+Mathf.Abs(angle)*.00001f;
                if(score<best){best=score;bestUpper=pose.LocalRotation(mapped[upper]);bestMid=pose.LocalRotation(mapped[mid]);}
            }
            pose.SetLocal(mapped[upper],bestUpper.normalized,pose.LocalPosition(mapped[upper]));pose.SetLocal(mapped[mid],bestMid.normalized,pose.LocalPosition(mapped[mid]));
            correction=Mathf.Max(correction,Vector3.Distance(initial,pose.Position(mapped[tip])));
        }
        float ArmPenetration(int mid,int tip)
        {
            float maximum=0;for(int sample=0;sample<=4;sample++){PushOut(Vector3.Lerp(pose.Position(mapped[mid]),pose.Position(mapped[tip]),sample/4f),out var d);maximum=Mathf.Max(maximum,d);}return maximum;
        }
        void CorrectFeet(MotionLabClip clip,int frame,float time)
        {
            for(int side=0;side<2;side++)
            {
                int thigh=side+1,knee=side+4,foot=side+7;
                var speed=Vector3.Distance(clip.frames[frame].positions[foot],clip.frames[Mathf.Max(0,frame-1)].positions[foot])*clip.fps*sourceScale;
                var channels=clip.frames[frame].contacts;
                bool predicted=channels==null||channels.Length!=4||Mathf.Max(channels[side*2],channels[side*2+1])>.5f;
                bool contact=predicted&&speed<Calibration.footLockSpeed&&time>.25f;
                if(contact&&!footContact[side])footAnchors[side]=pose.Position(mapped[foot]);
                footContact[side]=contact;if(!contact)continue;
                var current=pose.Position(mapped[foot]);var delta=footAnchors[side]-current;
                footCorrection=Mathf.Max(footCorrection,delta.magnitude);
                var target=current+Vector3.ClampMagnitude(delta,Calibration.footMaxCorrection);
                pose.SolveLimb(mapped[thigh],mapped[knee],mapped[foot],target,pose.Position(mapped[knee]),null,limbLimits,solveLimits,out _,out _);
                NormalizeLocal(mapped[thigh]);NormalizeLocal(mapped[knee]);
            }
        }
        public void EstablishHold(string id)
        {
            if(Segment.completion!="hold"&&Segment.completion!="infinite")return;
            foreach(var group in Segment.parts)
            {
                var held=new Held{id=id+":"+group,intent=Segment.intent,remaining=Segment.completion=="infinite"?-1:Segment.holdSeconds};
                for(int i=0;i<22;i++)if(mapped[i]!=null&&MotionSegment.Group(i)==group){held.rotations[i]=owner.ReadVisibleRotation(mapped[i]);held.positions[i]=owner.ReadVisiblePosition(mapped[i]);}
                if(group=="leftArm"||group=="rightArm")
                {int side=group=="leftArm"?0:1;held.target=mapped[9].InverseTransformPoint(mapped[20+side].position);held.hint=mapped[9].InverseTransformPoint(mapped[18+side].position);}
                holds[group]=held;
                foreach(var r in held.rotations)owner.WriteSpatial(held.id,mapped[r.Key],r.Value,held.positions[r.Key],false);
            }
        }
        public void ReleaseHold(string group){if(holds.TryGetValue(group,out var h)){owner.ReleaseSpatial(h.id,.35f);holds.Remove(group);}}
        public void ClearHolds(){foreach(var group in new List<string>(holds.Keys))ReleaseHold(group);}
        public void TickHolds(float delta)
        {
            foreach(var pair in new List<KeyValuePair<string,Held>>(holds))
            {
                var h=pair.Value;if(h.remaining>=0){h.remaining-=delta;if(h.remaining<=0){ReleaseHold(pair.Key);continue;}}
                var bones=new List<Transform>();foreach(var r in h.rotations)bones.Add(mapped[r.Key]);
                if(!owner.CanAcquireSpatial(bones,false,out _))continue;
                foreach(var r in h.rotations)owner.WriteSpatial(h.id,mapped[r.Key],r.Value,h.positions[r.Key],false);
                if(pair.Key=="leftArm"||pair.Key=="rightArm")
                {
                    int side=pair.Key=="leftArm"?0:1;pose.Capture(rig,owner,true);
                    foreach(var bone in rig)NormalizeLocal(bone);
                    var chest=pose.Position(mapped[9]);var rotation=pose.Rotation(mapped[9]);
                    var target=chest+rotation*Vector3.Scale(h.target,mapped[9].lossyScale);
                    var hint=chest+rotation*Vector3.Scale(h.hint,mapped[9].lossyScale);
                    pose.SolveLimb(mapped[16+side],mapped[18+side],mapped[20+side],target,hint,null,limbLimits,solveLimits,out _,out _);
                    NormalizeLocal(mapped[16+side]);NormalizeLocal(mapped[18+side]);
                    if(!Foundation)CorrectArm(16+side,18+side,20+side);
                    foreach(var r in h.rotations)owner.WriteSpatial(h.id,mapped[r.Key],pose.LocalRotation(mapped[r.Key]).normalized,h.positions[r.Key],false);
                }
            }
        }
        public void Dispose(){ClearHolds();UnityEngine.Object.Destroy(solveLimits);}
        // Must run after ActionCoordinator.FinalizeFrame; these are actual scene transforms.
        public void Measure(bool settled=true)
        {
            if(LastSource==null)return;
            // Diagnose committed transforms, not the detached solver pose.
            pose.Capture(rig,owner,false);
            for(int i=18;i<22;i++)if(mapped[i]!=null){PushOut(mapped[i].position,out var d);penetration=Mathf.Max(penetration,d);}
            var rotations=new Quaternion[22];var positions=new Vector3[22];for(int i=0;i<22;i++)if(mapped[i]!=null){rotations[i]=mapped[i].localRotation;positions[i]=mapped[i].position;}
            var face=mapped[15].rotation*Quaternion.Inverse(headRest)*(ReferenceBodyFrame*Vector3.forward);
            Diagnostic=new MotionFrameDiagnostic{correction=correction,residualPenetration=penetration,footCorrection=footCorrection,headTilt=Mathf.Asin(Mathf.Clamp(Vector3.Dot(face,Vector3.up),-1,1))*Mathf.Rad2Deg,positions=positions,localRotations=rotations,note="playbackParts="+string.Join(",",PlaybackParts)+"; "+ProcessingLayer+(Foundation?"; face/palm twist inferred; no collision/foot/head-target correction":"; legacy")};
            var hipForward=Vector3.Cross(LastSource[2]-LastSource[1],LastSource[9]-LastSource[0]).normalized;
            Diagnostic.sourceFacingConflict=Vector3.Dot((LastSource[15]-LastSource[12]).normalized,hipForward)<-.25f&&Vector3.Dot((LastSource[10]-LastSource[7]).normalized,hipForward)<-.25f&&Vector3.Dot((LastSource[11]-LastSource[8]).normalized,hipForward)<-.25f;
            Diagnostic.neckUsesBindingDelta=RelativeNeck;
            Diagnostic.neckSegmentDifference=Vector3.Angle(mapped[15].position-mapped[12].position,SourcePlacement*(LastSource[15]-LastSource[12]));
            Diagnostic.neckRotationError=RelativeNeck&&Plays("head")?Quaternion.Angle(mapped[12].rotation,expectedNeckRotation):0;
            if(Diagnostic.sourceFacingConflict)Diagnostic.note+="; SOURCE_FACING_CONFLICT: neck and both feet oppose hip reference; not a verified face direction; no automatic flip";
            foreach(var pair in aimChild)
                if(!Adapted||Plays(MotionSegment.Group(pair.Key)))
                {
                    // Different anatomical landmarks deliberately have different
                    // directions. Keep that difference in the report; compare
                    // their calibrated rotational motion separately.
                    if(RelativeNeck&&pair.Key==12)continue;
                    float error=Vector3.Angle(mapped[pair.Value].position-mapped[pair.Key].position,SourcePlacement*(LastSource[pair.Value]-LastSource[pair.Key]));
                    MaxDirectionError=Mathf.Max(MaxDirectionError,error);
                    if(settled)MaxSettledDirectionError=Mathf.Max(MaxSettledDirectionError,error);
                }
            foreach(var t in mapped)if(t!=null&&t!=mapped[0])MaxBoneLengthChange=Mathf.Max(MaxBoneLengthChange,Mathf.Abs(t.localPosition.magnitude-restPositions[index[t]].magnitude));
            if(!measured){leftFootStart=mapped[7].position;rightFootStart=mapped[8].position;measured=true;}
            MaxFootTravel=Mathf.Max(MaxFootTravel,Vector3.Distance(leftFootStart,mapped[7].position)/legLength,Vector3.Distance(rightFootStart,mapped[8].position)/legLength);
            MaxSourceFootTravel=Mathf.Max(MaxSourceFootTravel,Vector3.Distance(sourceLeftFoot,LastSource[7])*sourceScale/legLength,Vector3.Distance(sourceRightFoot,LastSource[8])*sourceScale/legLength);
            float bodyRadius=legLength*.12f;
            if(SpatialPoseBuffer.SegmentDistance(mapped[20].position,mapped[0].position,mapped[9].position)<bodyRadius ||
               SpatialPoseBuffer.SegmentDistance(mapped[21].position,mapped[0].position,mapped[9].position)<bodyRadius)TorsoIntersectionSamples++;
        }
    }
}
