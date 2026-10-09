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
        readonly Vector3 pelvisOrigin;
        readonly float legLength;
        readonly Quaternion pelvisRest;
        readonly Dictionary<Transform,int> index=new Dictionary<Transform,int>();
        readonly Dictionary<Transform,Quaternion> entryRotations=new Dictionary<Transform,Quaternion>();
        readonly Dictionary<Transform,Vector3> entryPositions=new Dictionary<Transform,Vector3>();
        readonly Dictionary<int,int> aimChild=new Dictionary<int,int>{{1,4},{2,5},{3,9},{4,7},{5,8},{7,10},{8,11},{9,12},{12,15},{13,16},{14,17},{16,18},{17,19},{18,20},{19,21}};
        float sourceScale;
        Vector3 sourceOrigin;
        public Vector3[] LastSource {get;private set;}
        public float MaxDirectionError {get;private set;}
        public float MaxBoneLengthChange {get;private set;}
        public float MaxFootTravel {get;private set;}
        public float MaxSourceFootTravel {get;private set;}
        public int TorsoIntersectionSamples {get;private set;}
        Vector3 sourceLeftFoot,sourceRightFoot;
        Vector3 leftFootStart,rightFootStart;
        bool measured;

        public MotionLabRetargeter(Dictionary<string,Transform> bones,Transform root,Transform character,SpatialRigProfile profile,ActionCoordinator coordinator)
        {
            owner=coordinator;rig=root.GetComponentsInChildren<Transform>(true);
            restRotations=new Quaternion[rig.Length];restPositions=new Vector3[rig.Length];
            for(int i=0;i<rig.Length;i++){restRotations[i]=rig[i].localRotation;restPositions[i]=rig[i].localPosition;index[rig[i]]=i;}
            for(int i=0;i<22;i++) if(MotionLabSkeleton.Bones[i]!="") mapped[i]=bones[MotionLabSkeleton.Bones[i]];
            bodyFrame=character.rotation*Quaternion.LookRotation(profile.bodyForward,profile.bodyUp);
            pelvisOrigin=mapped[0].position;pelvisRest=mapped[0].rotation;
            legLength=Vector3.Distance(mapped[1].position,mapped[4].position)+Vector3.Distance(mapped[4].position,mapped[7].position);
        }
        public void Begin(MotionLabClip clip)
        {
            clip.Validate();var p=clip.frames[0].positions;
            float length=Vector3.Distance(p[1],p[4])+Vector3.Distance(p[4],p[7]);
            if(length<.01f)throw new ArgumentException("Degenerate source legs.");
            sourceScale=legLength/length;sourceOrigin=p[0];
            MaxDirectionError=MaxBoneLengthChange=MaxFootTravel=MaxSourceFootTravel=0;TorsoIntersectionSamples=0;measured=false;
            sourceLeftFoot=p[7];sourceRightFoot=p[8];
            entryRotations.Clear();entryPositions.Clear();foreach(var t in mapped)if(t!=null){entryRotations[t]=owner.ReadVisibleRotation(t);entryPositions[t]=owner.ReadVisiblePosition(t);}
        }
        public bool Apply(MotionLabClip clip,float seconds,string id,out string reason)
        {
            var owned=new List<Transform>();foreach(var t in mapped)if(t!=null)owned.Add(t);
            if(!owner.CanAcquireSpatial(owned,false,out reason))return false;
            float frame=Mathf.Clamp(seconds*clip.fps,0,clip.frames.Length-1);int a=(int)frame,b=Mathf.Min(a+1,clip.frames.Length-1);
            LastSource=new Vector3[22];for(int i=0;i<22;i++)LastSource[i]=Vector3.Lerp(clip.frames[a].positions[i],clip.frames[b].positions[i],frame-a);
            // Unmapped ancestors (notably Bip001) continue to be sampled by Idle. The FK
            // preview must use their actual current transform, not an unapplied rest pose.
            pose.Capture(rig,owner,false);foreach(var t in mapped)if(t!=null){int i=index[t];pose.SetLocal(t,restRotations[i],restPositions[i]);}
            Vector3 right=LastSource[2]-LastSource[1],up=LastSource[9]-LastSource[0];
            Vector3 forward=Vector3.Cross(right,up);
            if(forward.sqrMagnitude<.000001f){reason="Degenerate torso basis.";return false;}
            var sourceFrame=Quaternion.LookRotation(forward,up);
            pose.SetRotation(mapped[0],bodyFrame*sourceFrame*Quaternion.Inverse(bodyFrame)*pelvisRest);
            pose.SetPosition(mapped[0],pelvisOrigin+bodyFrame*((LastSource[0]-sourceOrigin)*sourceScale));
            foreach(var pair in aimChild)
            {
                var joint=mapped[pair.Key];var child=mapped[pair.Value];
                var target=bodyFrame*(LastSource[pair.Value]-LastSource[pair.Key]);
                var current=pose.Position(child)-pose.Position(joint);
                if(target.sqrMagnitude<.0000001f || current.sqrMagnitude<.0000001f){reason="Degenerate source/target segment.";return false;}
                pose.SetRotation(joint,Quaternion.FromToRotation(current,target)*pose.Rotation(joint));
            }
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/.25f));
            foreach(var joint in owned) if(!owner.WriteSpatial(id,joint,Quaternion.Slerp(entryRotations[joint],pose.LocalRotation(joint),blend),Vector3.Lerp(entryPositions[joint],pose.LocalPosition(joint),blend),false))
            {reason="Ownership changed during retargeting.";owner.ReleaseSpatial(id,.35f);return false;}
            return true;
        }
        // Must run after ActionCoordinator.FinalizeFrame; these are actual scene transforms.
        public void Measure()
        {
            if(LastSource==null)return;
            foreach(var pair in aimChild)
                MaxDirectionError=Mathf.Max(MaxDirectionError,Vector3.Angle(mapped[pair.Value].position-mapped[pair.Key].position,bodyFrame*(LastSource[pair.Value]-LastSource[pair.Key])));
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
