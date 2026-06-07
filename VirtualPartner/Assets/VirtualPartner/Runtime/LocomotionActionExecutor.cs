using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LocomotionActionExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private Transform root;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private MovementConstraintController movementConstraintController;

        [Header("Runtime Status")]
        [SerializeField] private bool active;
        [SerializeField] private string activeMode;
        [SerializeField] private float elapsed;
        [SerializeField] private float duration;
        [SerializeField] private float speed;
        [SerializeField] private int controlledBoneCount;
        [SerializeField] private string lastMessage;

        private readonly List<LocomotionBonePose> sampledPoses = new List<LocomotionBonePose>();
        private LocomotionClipBinding activeBinding;
        private string activeId;
        private int sequence;
        private bool ownersAcquired;
        private bool missingConstraintWarningLogged;
        private BoneRequestFailureKind lastFailureKind;

        public bool IsActive => active;
        public string ActiveMode => activeMode;
        public float Elapsed => elapsed;
        public float Duration => duration;
        public float Speed => speed;
        public int ControlledBoneCount => controlledBoneCount;
        public string LastMessage => lastMessage;
        public BoneRequestFailureKind LastFailureKind => lastFailureKind;
        public Transform Root => root;

        public void Configure(
            LocomotionProfile profile,
            GameObject character,
            Transform rootTransform,
            Transform boneRootTransform,
            AvatarPoseApplier poseApplier,
            ActionCoordinator coordinator,
            MovementConstraintController constraintController)
        {
            locomotionProfile = profile;
            characterRoot = character;
            root = rootTransform;
            boneRoot = boneRootTransform;
            avatarPoseApplier = poseApplier;
            actionCoordinator = coordinator;
            movementConstraintController = constraintController;
            missingConstraintWarningLogged = false;
            StopLocomotion();
        }

        public bool StartLocomotion(string mode, float actionDuration, out string failureReason)
        {
            failureReason = string.Empty;

            StopLocomotion();

            if (!ValidateReferences(out failureReason))
                return false;

            if (!locomotionProfile.TryBuildClipBinding(mode, boneRoot, out activeBinding, out failureReason))
                return false;

            if (actionDuration <= 0f)
            {
                failureReason = "Locomotion duration must be greater than 0.";
                return false;
            }

            sequence++;
            activeId = $"{activeBinding.Mode}:{sequence}";
            activeMode = activeBinding.Mode;
            elapsed = 0f;
            duration = actionDuration;
            speed = activeBinding.Speed;
            controlledBoneCount = activeBinding.Targets.Length;
            active = true;
            ownersAcquired = false;
            lastFailureKind = BoneRequestFailureKind.None;
            lastMessage = $"Started {activeMode}.";
            return true;
        }

        public void StopLocomotion()
        {
            if (active && actionCoordinator != null)
                actionCoordinator.ReleaseLocomotion(activeId);

            active = false;
            activeBinding = null;
            activeId = string.Empty;
            activeMode = string.Empty;
            ownersAcquired = false;
            elapsed = 0f;
            duration = 0f;
            speed = 0f;
            controlledBoneCount = 0;
        }

        public void ManualUpdate(float deltaTime, AnimationClip idleClip, float idleTime)
        {
            if (!active)
                return;

            var frameDelta = Mathf.Min(Mathf.Max(0f, deltaTime), Mathf.Max(0f, duration - elapsed));
            if (frameDelta <= 0f)
            {
                StopLocomotion();
                return;
            }

            var nextElapsed = elapsed + frameDelta;
            var clipTime = GetClipTime(nextElapsed);
            if (!TrySampleLocomotion(clipTime, idleClip, idleTime, out var sampleFailure))
            {
                lastMessage = sampleFailure;
                lastFailureKind = BoneRequestFailureKind.InvalidData;
                Debug.LogWarning($"[VirtualPartner] Locomotion stopped: {sampleFailure}", this);
                StopLocomotion();
                return;
            }

            string requestFailure;
            BoneRequestFailureKind requestFailureKind;
            var updatedOwner = ownersAcquired
                ? actionCoordinator.UpdateLocomotionTargets(activeId, activeMode, sampledPoses, out requestFailure, out requestFailureKind)
                : actionCoordinator.RequestLocomotion(activeId, activeMode, sampledPoses, out requestFailure, out requestFailureKind);

            if (!updatedOwner)
            {
                lastMessage = requestFailure;
                lastFailureKind = requestFailureKind;
                Debug.LogWarning($"[VirtualPartner] Locomotion stopped: {requestFailure}", this);
                StopLocomotion();
                return;
            }

            ownersAcquired = true;

            if (!StillOwnsAllBones())
            {
                lastMessage = "A required bone was taken by a higher priority owner.";
                // Preserve prior classification: this message was treated as a generic
                // failure (it lacks the "owned by" marker the old text match looked for).
                lastFailureKind = BoneRequestFailureKind.None;
                Debug.LogWarning($"[VirtualPartner] Locomotion stopped: {lastMessage}", this);
                StopLocomotion();
                return;
            }

            var proposedPosition = root.position + root.forward * speed * frameDelta;
            if (!CanMoveTo(proposedPosition, out var constraintFailure))
            {
                lastMessage = constraintFailure;
                lastFailureKind = BoneRequestFailureKind.None;
                Debug.LogWarning($"[VirtualPartner] Locomotion stopped: {constraintFailure}", this);
                StopLocomotion();
                return;
            }

            root.position = proposedPosition;
            elapsed = nextElapsed;

            if (elapsed >= duration)
                StopLocomotion();
        }

        private bool CanMoveTo(Vector3 proposedPosition, out string failureReason)
        {
            failureReason = string.Empty;

            if (movementConstraintController == null)
            {
                if (!missingConstraintWarningLogged)
                {
                    Debug.LogWarning("[VirtualPartner] MovementConstraint disabled: controller reference is missing.", this);
                    missingConstraintWarningLogged = true;
                }

                return true;
            }

            if (movementConstraintController.CanMoveTo(proposedPosition, out failureReason))
                return true;

            if (string.IsNullOrWhiteSpace(failureReason))
                failureReason = "Movement constraint rejected locomotion.";

            return false;
        }

        private float GetClipTime(float sampleElapsed)
        {
            if (activeBinding == null || activeBinding.Clip == null || activeBinding.Clip.length <= 0f)
                return 0f;

            return activeBinding.Loop
                ? Mathf.Repeat(sampleElapsed, activeBinding.Clip.length)
                : Mathf.Min(sampleElapsed, activeBinding.Clip.length);
        }

        private bool TrySampleLocomotion(float clipTime, AnimationClip idleClip, float idleTime, out string failureReason)
        {
            sampledPoses.Clear();
            failureReason = string.Empty;

            if (activeBinding == null || activeBinding.Clip == null)
            {
                failureReason = "Clip binding is missing.";
                return false;
            }

            if (characterRoot == null || root == null)
            {
                failureReason = "Character root reference is missing.";
                return false;
            }

            if (avatarPoseApplier == null || idleClip == null)
            {
                failureReason = "Idle restore reference is missing.";
                return false;
            }

            var rootPosition = root.position;
            var rootRotation = root.rotation;

            try
            {
                activeBinding.Clip.SampleAnimation(characterRoot, clipTime);

                var targets = activeBinding.Targets;
                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target == null || target.Transform == null)
                    {
                        failureReason = $"Locomotion '{activeBinding.Mode}' has a missing target transform.";
                        return false;
                    }

                    sampledPoses.Add(new LocomotionBonePose(target.Transform, target.DisplayName, target.Transform.localRotation));
                }
            }
            catch (System.Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                avatarPoseApplier.ApplyIdle(idleClip, idleTime);
                root.position = rootPosition;
                root.rotation = rootRotation;
            }

            return sampledPoses.Count > 0;
        }

        private bool StillOwnsAllBones()
        {
            if (activeBinding == null || actionCoordinator == null)
                return false;

            var targets = activeBinding.Targets;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || !actionCoordinator.IsLocomotionOwner(target.Transform, activeId))
                    return false;
            }

            return true;
        }

        private bool ValidateReferences(out string failureReason)
        {
            failureReason = string.Empty;

            if (locomotionProfile == null)
                failureReason = "LocomotionProfile reference is missing.";
            else if (characterRoot == null)
                failureReason = "Character root reference is missing.";
            else if (root == null)
                failureReason = "Root reference is missing.";
            else if (boneRoot == null)
                failureReason = "Bone root reference is missing.";
            else if (avatarPoseApplier == null)
                failureReason = "AvatarPoseApplier reference is missing.";
            else if (actionCoordinator == null)
                failureReason = "ActionCoordinator reference is missing.";

            return string.IsNullOrEmpty(failureReason);
        }
    }
}
