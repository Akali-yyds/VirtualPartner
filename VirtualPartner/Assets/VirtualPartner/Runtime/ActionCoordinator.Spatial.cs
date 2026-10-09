using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public sealed partial class ActionCoordinator
    {
        public void ReleasePresetBone(Transform bone)
        {
            if (states.TryGetValue(bone, out var state) && state.Owner == BoneOwner.PresetAnimation)
                StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state), .35f);
        }
        public bool CanAcquireSpatial(IReadOnlyList<Transform> bones, bool thinking, out string reason)
        {
            reason = null;
            foreach (var bone in bones)
            {
                var owner = GetOwner(bone);
                if (owner == BoneOwner.Debug || owner == BoneOwner.Locomotion ||
                    (thinking && owner != BoneOwner.Idle && owner != BoneOwner.Thinking))
                { reason = bone.name + " is owned by " + owner; return false; }
            }
            return true;
        }
        public bool WriteSpatial(string id, Transform bone, Quaternion rotation, Vector3 position, bool thinking)
        {
            var state = GetOrCreateState(bone, bone.name);
            if (state.Owner == BoneOwner.Debug || state.Owner == BoneOwner.Locomotion ||
                (thinking && state.Owner != BoneOwner.Idle && state.Owner != BoneOwner.Thinking)) return false;
            state.SpatialId = id;
            state.SpatialRotation = rotation; state.SpatialPosition = position;
            state.Owner = thinking ? BoneOwner.Thinking : BoneOwner.SpatialMotion;
            state.Transition = null; // The spatial trajectory already starts from the last visible pose.
            return true;
        }
        public void ReleaseSpatial(string id, float duration)
        {
            foreach (var state in states.Values)
                if (state.SpatialId == id && (state.Owner == BoneOwner.SpatialMotion || state.Owner == BoneOwner.Thinking))
                { StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state), duration); state.SpatialId = null; }
        }
        public Quaternion ReadVisibleRotation(Transform bone) => states.TryGetValue(bone, out var s) ? GetCurrentOwnedPose(s) : bone.localRotation;
        public Vector3 ReadVisiblePosition(Transform bone) => states.TryGetValue(bone, out var s) ? GetCurrentOwnedPosition(s) : bone.localPosition;
    }
}
