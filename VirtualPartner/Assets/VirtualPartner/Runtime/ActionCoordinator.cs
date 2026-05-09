using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum BoneOwner
    {
        Idle,
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
        [SerializeField] private int activeTransitionCount;
        [SerializeField] private string activeBoneName;
        [SerializeField] private BoneOwner activeOwner;

        private readonly Dictionary<Transform, BoneControlState> states = new Dictionary<Transform, BoneControlState>();
        private readonly List<Transform> removeBuffer = new List<Transform>();

        public int DebugOwnedBoneCount => debugOwnedBoneCount;
        public int ActiveTransitionCount => activeTransitionCount;

        public void Configure(AvatarPoseApplier applier)
        {
            avatarPoseApplier = applier;
            states.Clear();
            RefreshStatus();
        }

        public bool RequestDebug(BoneMapInstance instance, Vector3 semanticRotation)
        {
            if (instance == null || instance.Transform == null || avatarPoseApplier == null)
                return false;

            var clampedRotation = instance.Entry.ClampRotation(semanticRotation);
            var mirrorSign = instance.Entry.GetMirrorSign(instance.Side);
            if (!avatarPoseApplier.TryBuildSemanticBoneRotation(
                    instance.Transform,
                    clampedRotation,
                    mirrorSign,
                    out var debugTarget))
            {
                return false;
            }

            var state = GetOrCreateState(instance);
            state.DebugTarget = debugTarget;

            if (state.Owner != BoneOwner.Debug)
            {
                StartTransition(state, BoneOwner.Debug, instance.Transform.localRotation);
                UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} {state.Transition.FromOwner} -> Debug.", this);
            }

            RefreshStatus();
            return true;
        }

        public void ReleaseDebug(BoneMapInstance instance)
        {
            if (instance == null)
                return;

            ReleaseDebug(instance.Transform);
        }

        public void ReleaseDebug(Transform bone)
        {
            if (bone == null || !states.TryGetValue(bone, out var state))
                return;

            if (state.Owner != BoneOwner.Debug)
                return;

            StartTransition(state, BoneOwner.Idle, bone.localRotation);
            UnityEngine.Debug.Log($"[VirtualPartner] Bone owner changed: {state.DisplayName} Debug -> Idle.", this);
            RefreshStatus();
        }

        public void ReleaseAllDebug()
        {
            foreach (var state in states.Values)
            {
                if (state.Owner == BoneOwner.Debug)
                    StartTransition(state, BoneOwner.Idle, state.Bone.localRotation);
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
                    var targetPose = state.Transition.ToOwner == BoneOwner.Debug ? state.DebugTarget : idlePoseNow;
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

                if (shouldWrite)
                    avatarPoseApplier.ApplyBoneLocalRotation(state.Bone, finalPose);

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

        private BoneControlState GetOrCreateState(BoneMapInstance instance)
        {
            if (states.TryGetValue(instance.Transform, out var state))
                return state;

            state = new BoneControlState(instance.Transform, instance.DisplayName);
            states.Add(instance.Transform, state);
            return state;
        }

        private void StartTransition(BoneControlState state, BoneOwner toOwner, Quaternion fromPose)
        {
            var fromOwner = state.Owner;
            state.Owner = toOwner;
            state.Transition = new BoneHandoffTransition(fromOwner, toOwner, fromPose, handoffDuration);
        }

        private void RefreshStatus()
        {
            debugOwnedBoneCount = 0;
            activeTransitionCount = 0;
            activeBoneName = string.Empty;
            activeOwner = BoneOwner.Idle;

            foreach (var state in states.Values)
            {
                if (state.Owner == BoneOwner.Debug)
                    debugOwnedBoneCount++;
                if (state.Transition != null)
                    activeTransitionCount++;

                if (!string.IsNullOrEmpty(activeBoneName))
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
            }

            public Transform Bone { get; }
            public string DisplayName { get; }
            public BoneOwner Owner { get; set; }
            public Quaternion DebugTarget { get; set; }
            public BoneHandoffTransition Transition { get; set; }
        }
    }
}
