using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    [Serializable] public sealed class MotionLabSemantic
    {
        public string intent, unsupported;
        public bool reset;
        public MotionLabStep[] steps;
    }
    [Serializable] public sealed class MotionLabStep
    {
        public float duration = 2, holdSeconds;
        public string completion = "idle";
        public MotionLabGoal[] goals;
    }
    [Serializable] public sealed class MotionLabGoal
    {
        public string part, target, direction, motion, palm;
        public float extent = .5f;
        public int cycles = 1;
    }
    [Serializable] public sealed class MotionLabFrame { public Vector3[] positions; public float[] contacts,rotation6d; public Quaternion rootRotation; }
    [Serializable] public sealed class MotionLabClip
    {
        public int version, seed;
        public string route, caption, coordinates;
        public string sourceFormat,coordinateVersion;
        public bool hasRootOrientation,fixture;
        public float fps;
        public int[] parents;
        public MotionLabFrame[] frames;
        public void Validate()
        {
            if (version != 1 || fps <= 0 || fps > 120 || frames == null || frames.Length < 2 || frames.Length > 12000)
                throw new ArgumentException("Invalid motion header or frame count.");
            if (coordinates == null || !coordinates.StartsWith("body-right/up/forward"))
                throw new ArgumentException("Unknown coordinate convention.");
            if (parents == null || parents.Length != 22 || parents[0] != -1)
                throw new ArgumentException("HumanML22 hierarchy required.");
            for (int i=1;i<22;i++) if (parents[i]!=MotionLabSkeleton.Parents[i]) throw new ArgumentException("Hierarchy differs from HumanML22.");
            foreach (var f in frames)
            {
                if(f==null)throw new ArgumentException("Missing motion frame.");
                if(hasRootOrientation){var q=f.rootRotation;float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;if(!SpatialMotionContract.Finite(norm)||Mathf.Abs(norm-1)>.001f)throw new ArgumentException("Invalid source root quaternion");}
                if (f?.positions == null || f.positions.Length != 22) throw new ArgumentException("Expected 22 positions per frame.");
                foreach (var v in f.positions) if (!SpatialMotionContract.Finite(v.x) || !SpatialMotionContract.Finite(v.y) || !SpatialMotionContract.Finite(v.z))
                    throw new ArgumentException("Non-finite joint position.");
                if(f.contacts!=null&&f.contacts.Length!=0&&f.contacts.Length!=4)throw new ArgumentException("Four contact channels required");
                if(f.rotation6d!=null&&f.rotation6d.Length!=0&&f.rotation6d.Length!=126)throw new ArgumentException("21 HumanML rotation channels required");
                if(f.contacts!=null)foreach(var v in f.contacts)if(!SpatialMotionContract.Finite(v))throw new ArgumentException("Non-finite contact");
                if(f.rotation6d!=null)foreach(var v in f.rotation6d)if(!SpatialMotionContract.Finite(v))throw new ArgumentException("Non-finite rotation channel");
            }
        }
    }

    // HumanML3D has three spine samples; current rig has two. Sample 6 is a virtual midpoint.
    public static class MotionLabSkeleton
    {
        public static readonly string[] Bones = {"Pelvis","Thigh:L","Thigh:R","Spine","Calf:L","Calf:R","", "Foot:L","Foot:R","Chest","Toe:L","Toe:R","Neck","Clavicle:L","Clavicle:R","Head","UpperArm:L","UpperArm:R","Forearm:L","Forearm:R","Hand:L","Hand:R"};
        public static readonly int[] Parents = {-1,0,0,0,1,2,3,4,5,6,7,8,9,9,9,12,13,14,16,17,18,19};
        public static Dictionary<string, Transform> Resolve(BoneMapProfile map, Transform root)
        {
            var instances = new List<BoneMapInstance>();
            if (map.BuildControlInstances(root, instances) != 0) throw new ArgumentException("Incomplete target bone map.");
            var result = new Dictionary<string, Transform>();
            foreach (var b in instances) result[b.SemanticBone + (b.Side == BoneSide.None ? "" : ":" + b.Side)] = b.Transform;
            return result;
        }
    }
}
