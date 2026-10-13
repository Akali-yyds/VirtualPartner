using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.Experiments
{
    public enum MotionInspectionLayer { Foundation, LegacyRaw, LegacyCorrected }
    // Separate experiment calibration; distances are in target character metres.
    [Serializable] public sealed class MotionAdaptationProfile
    {
        public int version=1;
        public string character="Toki",description="Body-frame ellipsoids; provisional envelope, inspect overlay and residuals";
        public float blendSeconds=.35f,clearance=.008f,armRadius=.025f,maximumArmCorrection=.12f;
        public float headSwing=45,headYaw=65,footLockSpeed=.025f,footMaxCorrection=.035f;
        public int correctionIterations=5,smoothingRadius=2;
        // Dataset mean neck->head vector, expressed in the source torso frame.
        // This removes representation offset, not all generated head inclination.
        public Vector3 sourceNeutralNeck=new Vector3(.001f,.758f,.652f);
        public Envelope[] envelopes={
            new Envelope{name="head/hair",bone=15,offset=new Vector3(0,.073f,0),radii=new Vector3(.132f,.16f,.12f)},
            new Envelope{name="torso",bone=9,offset=new Vector3(0,-.025f,.013f),radii=new Vector3(.075f,.103f,.061f)},
            new Envelope{name="skirt/pelvis",bone=0,offset=new Vector3(0,-.015f,.005f),radii=new Vector3(.115f,.068f,.085f)}
        };
        public static MotionAdaptationProfile Load()
        {
            var text=Resources.Load<TextAsset>("TokiMotionAdaptation");
            return text==null?new MotionAdaptationProfile():JsonUtility.FromJson<MotionAdaptationProfile>(text.text);
        }
        [Serializable] public sealed class Envelope{public string name;public int bone;public Vector3 offset,radii;}
    }
    [Serializable] public sealed class MotionSegment
    {
        public int index,frames;
        public string operation="motion",intent,motion,completion="idle",headTarget="none",status,error;
        public string playbackScope="body";
        public string[] parts={"leftArm","rightArm","torso","head","support"};
        public float seconds=4,holdSeconds;
        public double generatedSeconds,llmSeconds,momaskSeconds,firstSegmentSeconds;
        public bool Uses(string group)=>parts!=null&&Array.IndexOf(parts,group)>=0;
        public string[] ResolvePlaybackParts(ICollection<string> held)
        {
            var result=new System.Collections.Generic.List<string>();
            foreach(var group in new[]{"leftArm","rightArm","torso","head","support"})
                if(Uses(group)||((string.IsNullOrEmpty(playbackScope)||playbackScope=="body")&&(held==null||!held.Contains(group))))result.Add(group);
            return result.ToArray();
        }
        public static string Group(int bone)=>bone==13||bone==16||bone==18||bone==20?"leftArm":bone==14||bone==17||bone==19||bone==21?"rightArm":bone==12||bone==15?"head":bone==3||bone==9?"torso":"support";
    }
    [Serializable] public sealed class MotionFrameDiagnostic
    {
        public bool sourceFacingConflict,neckUsesBindingDelta;
        public float time,correction,residualPenetration,footCorrection,headTilt,neckSegmentDifference,neckRotationError;
        public Vector3[] positions;
        public Quaternion[] localRotations;
        public string note;
    }
}
