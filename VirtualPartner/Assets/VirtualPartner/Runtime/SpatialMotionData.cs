using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable] public sealed class SpatialTrackDto
    {
        public string group; // leftArm, rightArm, support, torso, head
        public string anchor = "body";
        public SpatialKeyDto[] keys;
        public SpatialCompletionDto completion;
    }
    [Serializable] public sealed class SpatialKeyDto
    {
        public float time;
        public StagePlanRotationDto position;
        public StagePlanRotationDto palm;
        public StagePlanRotationDto fingers;
        public StagePlanRotationDto hint;
        public StagePlanBonePoseDto[] bones;
        public bool stop;
    }
    [Serializable] public sealed class SpatialCompletionDto
    {
        public string mode; // null inherits the action; otherwise idle, hold, timed, restore
        public float seconds;
    }
    public static class SpatialMotionContract
    {
        public static readonly string[] Groups = { "leftArm", "rightArm", "support", "torso", "head" };
        public static bool IsGroup(string value) => Array.IndexOf(Groups, value) >= 0;
        public static string BoneGroup(string bone, string side)
        {
            if (bone == "Clavicle" || bone == "UpperArm" || bone == "Forearm" || bone == "Hand") return side == "L" ? "leftArm" : "rightArm";
            if (bone == "Neck" || bone == "Head") return "head";
            if (bone == "Spine" || bone == "Chest") return "torso";
            return "support";
        }
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(StagePlanRotationDto v) => v == null || (Finite(v.x) && Finite(v.y) && Finite(v.z));
        public static bool Validate(StagePlanActionDto action, out string error)
        {
            error = null;
            if (action == null) { error="Spatial action is null.";return false; }
            if (string.Equals(action.type,"poseReset",StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(action.target) && action.target != "all" && !IsGroup(action.target)) error = "Unknown reset group.";
                return error == null;
            }
            if (action.sync != null && action.sync != "" && action.sync != "prepare" && action.sync != "speech") error = "sync must be prepare or speech.";
            var finish = action.completion;
            if (finish != null && !ValidCompletion(finish)) error = "Unknown completion mode.";
            if (finish != null && finish.mode == "timed" && (!Finite(finish.seconds) || finish.seconds <= 0 || finish.seconds > 3600)) error = "Timed hold must be 0..3600 seconds.";
            if (action.tracks == null || action.tracks.Length == 0 || action.tracks.Length > 5) error = "spatialPose requires 1..5 tracks.";
            if (error != null) return false;
            var groups = new HashSet<string>();
            foreach (var track in action.tracks)
            {
                if (track == null || !IsGroup(track.group) || !groups.Add(track.group)) { error = "Invalid or duplicate track group."; return false; }
                if (track.anchor != "body" && track.anchor != "head" && track.anchor != "chest" && track.anchor != "shoulder") { error = "Unknown anchor."; return false; }
                if (track.group != "leftArm" && track.group != "rightArm" && track.anchor != "body") { error = "Only arm targets support moving anchors."; return false; }
                if (track.completion != null && !ValidCompletion(track.completion)) { error = "Invalid track completion."; return false; }
                if (track.keys == null || track.keys.Length == 0 || track.keys.Length > 32) { error = "Track requires 1..32 keys."; return false; }
                float previous = -1;
                foreach (var key in track.keys)
                {
                    if (key == null || !Finite(key.time) || key.time < 0 || key.time <= previous || key.time > 30 || !Finite(key.position) || !Finite(key.palm) || !Finite(key.fingers) || !Finite(key.hint)) { error = "Invalid key time " + (key == null ? "null" : key.time.ToString()) + " after " + previous + ", or non-finite vector."; return false; }
                    previous = key.time;
                    bool arm = track.group == "leftArm" || track.group == "rightArm";
                    if ((arm || track.group == "support") && key.position == null) { error = "Arm/support keys require position."; return false; }
                    if (arm && key.bones != null && key.bones.Length > 0) { error = "IK and direct rotations cannot share an arm track."; return false; }
                    if ((key.palm == null || key.palm.ToVector3().sqrMagnitude < .000001f) != (key.fingers == null || key.fingers.ToVector3().sqrMagnitude < .000001f)) { error = "Provide palm and fingers together."; return false; }
                    if (key.palm != null && key.palm.ToVector3().sqrMagnitude > .000001f && Vector3.Cross(key.palm.ToVector3(), key.fingers.ToVector3()).sqrMagnitude < .001f) { error = "Palm and fingers must define independent directions."; return false; }
                    if (key.bones != null) foreach (var bone in key.bones)
                    {
                        if (bone == null || bone.rotation == null || !Finite(bone.rotation) || !AllowsBone(track.group, bone.bone)) { error = "Direct bone is outside track ownership."; return false; }
                    }
                }
            }
            return true;
        }
        public static SpatialCompletionDto ResolveCompletion(SpatialTrackDto track, SpatialCompletionDto action) =>
            !string.IsNullOrEmpty(track?.completion?.mode) ? track.completion :
            !string.IsNullOrEmpty(action?.mode) ? action : new SpatialCompletionDto { mode = "idle" };
        public static bool ValidCompletion(SpatialCompletionDto c) => c != null && (string.IsNullOrEmpty(c.mode) || c.mode == "idle" || c.mode == "hold" || c.mode == "restore" || (c.mode == "timed" && Finite(c.seconds) && c.seconds > 0 && c.seconds <= 3600));
        public static bool AllowsBone(string group, string bone) =>
            group == "head" ? bone == "Head" || bone == "Neck" :
            group == "torso" ? bone == "Spine" || bone == "Chest" :
            group == "support" && bone == "Pelvis";
    }
}
