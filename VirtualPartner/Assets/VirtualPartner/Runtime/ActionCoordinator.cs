using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum BoneOwner
    {
        Idle,
        PresetAnimation,
        Locomotion,
        StagePlanBonePose,
        Debug
    }

    public sealed class BoneHandoffTransition
    {
        public BoneHandoffTransition(BoneOwner fromOwner, BoneOwner toOwner, Quaternion fromPose, float duration)
        {
            FromOwner = fromOwner;
            ToOwner = toOwner;
            FromPose = fromPose;
            Duration = Mathf.Max(0.0001f, duration);
            Elapsed = 0f;
        }

        public BoneOwner FromOwner { get; }
        public BoneOwner ToOwner { get; }
        public Quaternion FromPose { get; }
        public float Duration { get; }
        public float Elapsed { get; private set; }
        public bool IsComplete => Elapsed >= Duration;
        public float Progress => Mathf.Clamp01(Elapsed / Duration);

        public void Advance(float deltaTime)
        {
            Elapsed = Mathf.Min(Duration, Elapsed + Mathf.Max(0f, deltaTime));
        }
    }

    [DisallowMultipleComponent]
    public sealed class ActionCoordinator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;

        [Header("Settings")]
        [SerializeField] private float handoffDuration = 0.25f;

        [Header("Runtime Status")]
        [SerializeField] private int debugOwnedBoneCount;
        [SerializeField] private int stagePlanBonePoseOwnedBoneCount;
        [SerializeField] private int locomotionOwnedBoneCount;
        [SerializeField] private int presetAnimationOwnedBoneCount;
        [SerializeField] private int activeTransitionCount;
        [SerializeField] private string activeBoneName;
        [SerializeField] private BoneOwner activeOwner;

        private readonly Dictionary<Transform, BoneControlState> states = new Dictionary<Transform, BoneControlState>();
        private readonly List<Transform> removeBuffer = new List<Transform>();
        private readonly List<string> displacedPresetBuffer = new List<string>();

        public int DebugOwnedBoneCount => debugOwnedBoneCount;
        public int StagePlanBonePoseOwnedBoneCount => stagePlanBonePoseOwnedBoneCount;
        public int LocomotionOwnedBoneCount => locomotionOwnedBoneCount;
        public int PresetAnimationOwnedBoneCount => presetAnimationOwnedBoneCount;
        public int ActiveTransitionCount => activeTransitionCount;
        public string ActiveBoneName => activeBoneName;
        public BoneOwner ActiveOwner => activeOwner;

        public void Configure(AvatarPoseApplier applier)
        {
            avatarPoseApplier = applier;
            states.Clear();
            RefreshStatus();
        }

        public bool RequestDebug(BoneMapInstance instance, Vector3 semanticRotation)
        {
            if (!HasResolvedTransforms(instance) || avatarPoseApplier == null)
                return false;

            var clampedRotation = instance.Entry.ClampRotation(semanticRotation);
            var mirrorSign = instance.Entry.GetMirrorSign(instance.Side);
            var debugTargets = new Quaternion[instance.Transforms.Count];

            for (var i = 0; i < instance.Transforms.Count; i++)
            {
                var transform = instance.Transforms[i];
                if (!avatarPoseApplier.TryBuildSemanticBoneRotation(
                        transform,
                        clampedRotation,
                        mirrorSign,
                        out debugTargets[i]))
                {
                    return false;
                }
            }

            for (var i = 0; i < instance.Transforms.Count; i++)
            {
                var state = GetOrCreateState(instance.Transforms[i], GetInstanceDisplayName(instance, i));
                state.DebugTarget = debugTargets[i];

                if (state.Owner != BoneOwner.Debug)
                {
                    StartTransition(state, BoneOwner.Debug, GetCurrentOwnedPose(state));
                    UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} {state.Transition.FromOwner} -> Debug.", this);
                }
            }

            RefreshStatus();
            return true;
        }

        public void ReleaseDebug(BoneMapInstance instance)
        {
            if (instance == null)
                return;

            for (var i = 0; i < instance.Transforms.Count; i++)
                ReleaseDebug(instance.Transforms[i]);
        }

        public void ReleaseDebug(Transform bone)
        {
            if (bone == null || !states.TryGetValue(bone, out var state))
                return;

            if (state.Owner != BoneOwner.Debug)
                return;

            StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
            UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} Debug -> Idle.", this);
            RefreshStatus();
        }

        public void ReleaseAllDebug()
        {
            foreach (var state in states.Values)
            {
                if (state.Owner == BoneOwner.Debug)
                    StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
            }

            RefreshStatus();
        }

        public bool RequestStagePlanBonePose(BoneMapInstance instance, Vector3 semanticRotation, out string failureReason)
        {
            return RequestStagePlanBonePose(instance, semanticRotation, handoffDuration, out failureReason);
        }

        public bool RequestStagePlanBonePose(BoneMapInstance instance, Vector3 semanticRotation, float transitionDuration, out string failureReason)
        {
            failureReason = string.Empty;

            if (!HasResolvedTransforms(instance))
            {
                failureReason = "StagePlan bone instance is missing.";
                return false;
            }

            if (avatarPoseApplier == null)
            {
                failureReason = "AvatarPoseApplier reference is missing.";
                return false;
            }

            for (var i = 0; i < instance.Transforms.Count; i++)
            {
                var transform = instance.Transforms[i];
                var displayName = GetInstanceDisplayName(instance, i);
                if (states.TryGetValue(transform, out var existingState) && existingState.Owner == BoneOwner.Debug)
                {
                    failureReason = $"{displayName} is owned by Debug.";
                    return false;
                }
            }

            var clampedRotation = instance.Entry.ClampRotation(semanticRotation);
            var mirrorSign = instance.Entry.GetMirrorSign(instance.Side);
            var stagePlanBonePoseTargets = new Quaternion[instance.Transforms.Count];

            for (var i = 0; i < instance.Transforms.Count; i++)
            {
                if (!avatarPoseApplier.TryBuildSemanticBoneRotation(
                        instance.Transforms[i],
                        clampedRotation,
                        mirrorSign,
                        out stagePlanBonePoseTargets[i]))
                {
                    failureReason = $"Could not build StagePlan pose for {GetInstanceDisplayName(instance, i)}.";
                    return false;
                }
            }

            for (var i = 0; i < instance.Transforms.Count; i++)
            {
                var state = GetOrCreateState(instance.Transforms[i], GetInstanceDisplayName(instance, i));
                state.StagePlanBonePoseTarget = stagePlanBonePoseTargets[i];

                if (state.Owner != BoneOwner.StagePlanBonePose)
                {
                    StartTransition(state, BoneOwner.StagePlanBonePose, GetCurrentOwnedPose(state), transitionDuration);
                    UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} {state.Transition.FromOwner} -> StagePlanBonePose.", this);
                }
                else
                {
                    StartTransition(state, BoneOwner.StagePlanBonePose, GetCurrentOwnedPose(state), transitionDuration);
                }
            }

            RefreshStatus();
            return true;
        }

        public void ReleaseStagePlanBonePose(BoneMapInstance instance)
        {
            if (instance == null)
                return;

            for (var i = 0; i < instance.Transforms.Count; i++)
                ReleaseStagePlanBonePose(instance.Transforms[i]);
        }

        public void ReleaseStagePlanBonePose(Transform bone)
        {
            if (bone == null || !states.TryGetValue(bone, out var state))
                return;

            if (state.Owner != BoneOwner.StagePlanBonePose)
                return;

            StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
            UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} StagePlanBonePose -> Idle.", this);
            RefreshStatus();
        }

        public void ReleaseAllStagePlanBonePoses()
        {
            foreach (var state in states.Values)
            {
                if (state.Owner == BoneOwner.StagePlanBonePose)
                    StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
            }

            RefreshStatus();
        }

        public bool RequestPresetAnimation(
            string presetId,
            string displayName,
            IReadOnlyList<PresetAnimationBonePose> poses,
            List<string> displacedPresetIds,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(presetId))
            {
                failureReason = "Preset animation id is missing.";
                return false;
            }

            if (poses == null || poses.Count == 0)
            {
                failureReason = $"Preset animation '{displayName}' has no target poses.";
                return false;
            }

            if (avatarPoseApplier == null)
            {
                failureReason = "AvatarPoseApplier reference is missing.";
                return false;
            }

            displacedPresetBuffer.Clear();
            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                if (pose == null || pose.Bone == null)
                {
                    failureReason = $"Preset animation '{displayName}' contains a missing bone pose.";
                    return false;
                }

                if (!avatarPoseApplier.TryGetBaseRotation(pose.Bone, out _))
                {
                    failureReason = $"{pose.DisplayName} is outside the captured BaseRotation set.";
                    return false;
                }

                var state = GetOrCreateState(pose.Bone, pose.DisplayName);
                if (state.Owner == BoneOwner.Debug)
                {
                    failureReason = $"{state.DisplayName} is owned by Debug.";
                    return false;
                }

                if (state.Owner == BoneOwner.StagePlanBonePose)
                {
                    failureReason = $"{state.DisplayName} is owned by StagePlanBonePose.";
                    return false;
                }

                if (state.Owner == BoneOwner.Locomotion)
                {
                    failureReason = $"{state.DisplayName} is owned by Locomotion.";
                    return false;
                }

                if (state.Owner == BoneOwner.PresetAnimation
                    && state.PresetAnimationId != presetId
                    && !displacedPresetBuffer.Contains(state.PresetAnimationId))
                {
                    displacedPresetBuffer.Add(state.PresetAnimationId);
                }
            }

            for (var i = 0; i < displacedPresetBuffer.Count; i++)
            {
                var displacedId = displacedPresetBuffer[i];
                if (displacedPresetIds != null && !displacedPresetIds.Contains(displacedId))
                    displacedPresetIds.Add(displacedId);

                ReleasePresetAnimation(displacedId);
            }

            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                var state = GetOrCreateState(pose.Bone, pose.DisplayName);
                var wasSamePreset = state.Owner == BoneOwner.PresetAnimation && state.PresetAnimationId == presetId;

                state.PresetAnimationTarget = pose.LocalRotation;
                state.PresetAnimationId = presetId;

                if (!wasSamePreset)
                {
                    StartTransition(state, BoneOwner.PresetAnimation, GetCurrentOwnedPose(state));
                    UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} {state.Transition.FromOwner} -> PresetAnimation ({displayName}).", this);
                }
            }

            RefreshStatus();
            return true;
        }

        public void ReleasePresetAnimation(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                return;

            foreach (var state in states.Values)
            {
                if (state.Owner != BoneOwner.PresetAnimation || state.PresetAnimationId != presetId)
                    continue;

                StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
                state.PresetAnimationId = string.Empty;
            }

            RefreshStatus();
        }

        public void ReleaseAllPresetAnimations()
        {
            foreach (var state in states.Values)
            {
                if (state.Owner != BoneOwner.PresetAnimation)
                    continue;

                StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
                state.PresetAnimationId = string.Empty;
            }

            RefreshStatus();
        }

        public bool RequestLocomotion(
            string locomotionId,
            string displayName,
            IReadOnlyList<LocomotionBonePose> poses,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(locomotionId))
            {
                failureReason = "Locomotion id is missing.";
                return false;
            }

            if (poses == null || poses.Count == 0)
            {
                failureReason = $"Locomotion '{displayName}' has no target poses.";
                return false;
            }

            if (avatarPoseApplier == null)
            {
                failureReason = "AvatarPoseApplier reference is missing.";
                return false;
            }

            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                if (pose == null || pose.Bone == null)
                {
                    failureReason = $"Locomotion '{displayName}' contains a missing bone pose.";
                    return false;
                }

                if (!avatarPoseApplier.TryGetBaseRotation(pose.Bone, out _))
                {
                    failureReason = $"{pose.DisplayName} is outside the captured BaseRotation set.";
                    return false;
                }

                var state = GetOrCreateState(pose.Bone, pose.DisplayName);
                if (state.Owner == BoneOwner.Debug)
                {
                    failureReason = $"{state.DisplayName} is owned by Debug.";
                    return false;
                }

                if (state.Owner == BoneOwner.StagePlanBonePose)
                {
                    failureReason = $"{state.DisplayName} is owned by StagePlanBonePose.";
                    return false;
                }

                if (state.Owner == BoneOwner.Locomotion && state.LocomotionId != locomotionId)
                {
                    failureReason = $"{state.DisplayName} is owned by another Locomotion.";
                    return false;
                }
            }

            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                var state = GetOrCreateState(pose.Bone, pose.DisplayName);
                var wasSameLocomotion = state.Owner == BoneOwner.Locomotion && state.LocomotionId == locomotionId;

                state.LocomotionTarget = pose.LocalRotation;
                state.LocomotionId = locomotionId;

                if (!wasSameLocomotion)
                {
                    StartTransition(state, BoneOwner.Locomotion, GetCurrentOwnedPose(state));
                    UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} {state.Transition.FromOwner} -> Locomotion ({displayName}).", this);
                }
            }

            RefreshStatus();
            return true;
        }

        public void ReleaseLocomotion(string locomotionId)
        {
            if (string.IsNullOrWhiteSpace(locomotionId))
                return;

            foreach (var state in states.Values)
            {
                if (state.Owner != BoneOwner.Locomotion || state.LocomotionId != locomotionId)
                    continue;

                StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
                state.LocomotionId = string.Empty;
            }

            RefreshStatus();
        }

        public bool UpdateLocomotionTargets(
            string locomotionId,
            string displayName,
            IReadOnlyList<LocomotionBonePose> poses,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(locomotionId))
            {
                failureReason = "Locomotion id is missing.";
                return false;
            }

            if (poses == null || poses.Count == 0)
            {
                failureReason = $"Locomotion '{displayName}' has no target poses.";
                return false;
            }

            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                if (pose == null || pose.Bone == null)
                {
                    failureReason = $"Locomotion '{displayName}' contains a missing bone pose.";
                    return false;
                }

                if (!states.TryGetValue(pose.Bone, out var state)
                    || state.Owner != BoneOwner.Locomotion
                    || state.LocomotionId != locomotionId)
                {
                    failureReason = $"{pose.DisplayName} is no longer owned by Locomotion.";
                    return false;
                }

                state.LocomotionTarget = pose.LocalRotation;
            }

            RefreshStatus();
            return true;
        }

        public void ReleaseAllLocomotion()
        {
            foreach (var state in states.Values)
            {
                if (state.Owner != BoneOwner.Locomotion)
                    continue;

                StartTransition(state, BoneOwner.Idle, GetCurrentOwnedPose(state));
                state.LocomotionId = string.Empty;
            }

            RefreshStatus();
        }

        public void FinalizeFrame(float deltaTime)
        {
            if (avatarPoseApplier == null)
                return;

            removeBuffer.Clear();

            foreach (var pair in states)
            {
                var state = pair.Value;
                var idlePoseNow = state.Bone.localRotation;
                var shouldWrite = false;
                var finalPose = idlePoseNow;

                if (state.Transition != null)
                {
                    state.Transition.Advance(deltaTime);
                    var smoothProgress = Mathf.SmoothStep(0f, 1f, state.Transition.Progress);
                    var targetPose = GetTargetPose(state, idlePoseNow);
                    finalPose = Quaternion.Slerp(state.Transition.FromPose, targetPose, smoothProgress);
                    shouldWrite = true;

                    if (state.Transition.IsComplete)
                        state.Transition = null;
                }
                else if (state.Owner == BoneOwner.Debug)
                {
                    finalPose = state.DebugTarget;
                    shouldWrite = true;
                }
                else if (state.Owner == BoneOwner.StagePlanBonePose)
                {
                    finalPose = state.StagePlanBonePoseTarget;
                    shouldWrite = true;
                }
                else if (state.Owner == BoneOwner.Locomotion)
                {
                    finalPose = state.LocomotionTarget;
                    shouldWrite = true;
                }
                else if (state.Owner == BoneOwner.PresetAnimation)
                {
                    finalPose = state.PresetAnimationTarget;
                    shouldWrite = true;
                }

                if (shouldWrite)
                {
                    avatarPoseApplier.ApplyBoneLocalRotation(state.Bone, finalPose);
                    state.LastAppliedPose = finalPose;
                    state.HasLastAppliedPose = true;
                }

                if (state.Owner == BoneOwner.Idle && state.Transition == null)
                    removeBuffer.Add(pair.Key);
            }

            for (var i = 0; i < removeBuffer.Count; i++)
                states.Remove(removeBuffer[i]);

            RefreshStatus();
        }

        public BoneOwner GetOwner(Transform bone)
        {
            if (bone != null && states.TryGetValue(bone, out var state))
                return state.Owner;

            return BoneOwner.Idle;
        }

        public string GetStatus(Transform bone)
        {
            if (bone == null || !states.TryGetValue(bone, out var state))
                return BoneOwner.Idle.ToString();

            if (state.Transition == null)
                return state.Owner.ToString();

            return $"{state.Transition.FromOwner} -> {state.Transition.ToOwner} {state.Transition.Progress:0.00}";
        }

        public bool IsPresetAnimationOwner(Transform bone, string presetId)
        {
            if (bone == null || string.IsNullOrWhiteSpace(presetId))
                return false;

            return states.TryGetValue(bone, out var state)
                && state.Owner == BoneOwner.PresetAnimation
                && state.PresetAnimationId == presetId;
        }

        public bool IsLocomotionOwner(Transform bone, string locomotionId)
        {
            if (bone == null || string.IsNullOrWhiteSpace(locomotionId))
                return false;

            return states.TryGetValue(bone, out var state)
                && state.Owner == BoneOwner.Locomotion
                && state.LocomotionId == locomotionId;
        }

        private BoneControlState GetOrCreateState(Transform bone, string displayName)
        {
            if (states.TryGetValue(bone, out var state))
                return state;

            state = new BoneControlState(bone, displayName);
            states.Add(bone, state);
            return state;
        }

        private static bool HasResolvedTransforms(BoneMapInstance instance)
        {
            return instance != null && instance.Transforms != null && instance.Transforms.Count > 0 && instance.Transform != null;
        }

        private static string GetInstanceDisplayName(BoneMapInstance instance, int transformIndex)
        {
            if (instance == null)
                return string.Empty;

            if (instance.Transforms == null || instance.Transforms.Count <= 1)
                return instance.DisplayName;

            var transform = transformIndex >= 0 && transformIndex < instance.Transforms.Count
                ? instance.Transforms[transformIndex]
                : null;

            return transform == null
                ? instance.DisplayName
                : $"{instance.DisplayName} ({transform.name})";
        }

        private void StartTransition(BoneControlState state, BoneOwner toOwner, Quaternion fromPose)
        {
            StartTransition(state, toOwner, fromPose, handoffDuration);
        }

        private void StartTransition(BoneControlState state, BoneOwner toOwner, Quaternion fromPose, float duration)
        {
            var fromOwner = state.Owner;
            state.Owner = toOwner;
            state.Transition = new BoneHandoffTransition(fromOwner, toOwner, fromPose, duration);
        }

        private Quaternion GetCurrentOwnedPose(BoneControlState state)
        {
            if (state.HasLastAppliedPose && (state.Owner != BoneOwner.Idle || state.Transition != null))
                return state.LastAppliedPose;

            return state.Bone.localRotation;
        }

        private static Quaternion GetTargetPose(BoneControlState state, Quaternion idlePoseNow)
        {
            if (state.Transition.ToOwner == BoneOwner.Debug)
                return state.DebugTarget;
            if (state.Transition.ToOwner == BoneOwner.StagePlanBonePose)
                return state.StagePlanBonePoseTarget;
            if (state.Transition.ToOwner == BoneOwner.Locomotion)
                return state.LocomotionTarget;
            if (state.Transition.ToOwner == BoneOwner.PresetAnimation)
                return state.PresetAnimationTarget;

            return idlePoseNow;
        }

        private void RefreshStatus()
        {
            debugOwnedBoneCount = 0;
            stagePlanBonePoseOwnedBoneCount = 0;
            locomotionOwnedBoneCount = 0;
            presetAnimationOwnedBoneCount = 0;
            activeTransitionCount = 0;
            activeBoneName = string.Empty;
            activeOwner = BoneOwner.Idle;

            foreach (var state in states.Values)
            {
                if (state.Owner == BoneOwner.Debug)
                    debugOwnedBoneCount++;
                if (state.Owner == BoneOwner.StagePlanBonePose)
                    stagePlanBonePoseOwnedBoneCount++;
                if (state.Owner == BoneOwner.Locomotion)
                    locomotionOwnedBoneCount++;
                if (state.Owner == BoneOwner.PresetAnimation)
                    presetAnimationOwnedBoneCount++;
                if (state.Transition != null)
                    activeTransitionCount++;

                if (!string.IsNullOrEmpty(activeBoneName))
                    continue;

                if (state.Owner == BoneOwner.Idle && state.Transition == null)
                    continue;

                activeBoneName = state.DisplayName;
                activeOwner = state.Owner;
            }
        }

        private sealed class BoneControlState
        {
            public BoneControlState(Transform bone, string displayName)
            {
                Bone = bone;
                DisplayName = displayName;
                Owner = BoneOwner.Idle;
                DebugTarget = bone.localRotation;
                StagePlanBonePoseTarget = bone.localRotation;
                LocomotionTarget = bone.localRotation;
                LocomotionId = string.Empty;
                PresetAnimationTarget = bone.localRotation;
                PresetAnimationId = string.Empty;
                LastAppliedPose = bone.localRotation;
            }

            public Transform Bone { get; }
            public string DisplayName { get; }
            public BoneOwner Owner { get; set; }
            public Quaternion DebugTarget { get; set; }
            public Quaternion StagePlanBonePoseTarget { get; set; }
            public Quaternion LocomotionTarget { get; set; }
            public string LocomotionId { get; set; }
            public Quaternion PresetAnimationTarget { get; set; }
            public string PresetAnimationId { get; set; }
            public BoneHandoffTransition Transition { get; set; }
            public Quaternion LastAppliedPose { get; set; }
            public bool HasLastAppliedPose { get; set; }
        }
    }
}
