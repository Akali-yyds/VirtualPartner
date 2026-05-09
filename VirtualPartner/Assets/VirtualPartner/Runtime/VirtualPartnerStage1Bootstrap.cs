using System.Collections;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualPartnerStage1Bootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private IdleBaseProvider idleBaseProvider;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private ActionCoordinator actionCoordinator;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool baseRotationCaptured;
        [SerializeField] private int registeredBoneCount;
        [SerializeField] private bool idlePlaying;
        [SerializeField] private float currentIdleTime;

        private IEnumerator Start()
        {
            initialized = false;
            baseRotationCaptured = false;
            registeredBoneCount = 0;
            idlePlaying = false;
            currentIdleTime = 0f;

            if (!ValidateReferences())
                yield break;

            idleBaseProvider.Configure(idleClip);
            avatarPoseApplier.Configure(characterRoot, boneRoot);
            actionCoordinator.Configure(avatarPoseApplier);

            yield return null;

            if (!ValidateStaticBaseline())
                yield break;

            registeredBoneCount = avatarPoseApplier.CaptureBaseRotations();
            baseRotationCaptured = registeredBoneCount > 0;

            if (!baseRotationCaptured)
            {
                Fail("BaseRotation capture failed because no bones were registered.");
                yield break;
            }

            idleBaseProvider.Play();
            ApplyIdleFrame(0f);
            actionCoordinator.FinalizeFrame(0f);
            idlePlaying = idleBaseProvider.IsPlaying;
            initialized = true;

            Debug.Log(
                $"[VirtualPartner] Stage 1 initialized. BaseRotation bones: {registeredBoneCount}, Idle clip: {idleClip.name}.",
                this);
        }

        private void Update()
        {
            if (!initialized)
                return;

            ApplyIdleFrame(Time.deltaTime);
            actionCoordinator.FinalizeFrame(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (actionCoordinator != null)
                actionCoordinator.ReleaseAllDebug();
            if (idleBaseProvider != null)
                idleBaseProvider.Stop();
        }

        private void ApplyIdleFrame(float deltaTime)
        {
            currentIdleTime = idleBaseProvider.Advance(deltaTime);
            idlePlaying = idleBaseProvider.IsPlaying;
            avatarPoseApplier.ApplyIdle(idleBaseProvider.Clip, currentIdleTime);
        }

        private bool ValidateReferences()
        {
            if (characterRoot == null)
                return Fail("Character root reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (idleClip == null)
                return Fail("Idle clip reference is missing.");
            if (idleBaseProvider == null)
                return Fail("IdleBaseProvider reference is missing.");
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.");
            if (actionCoordinator == null)
                return Fail("ActionCoordinator reference is missing.");
            if (!boneRoot.IsChildOf(characterRoot.transform))
                return Fail("Bone root must be inside the character root hierarchy.");

            return true;
        }

        private bool ValidateStaticBaseline()
        {
            var animators = characterRoot.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || !animator.enabled)
                    continue;

                if (animator.runtimeAnimatorController != null)
                    return Fail($"Animator is active before BaseRotation capture: {GetHierarchyPath(animator.transform)}.");
            }

            var animations = characterRoot.GetComponentsInChildren<Animation>(true);
            for (var i = 0; i < animations.Length; i++)
            {
                var animationComponent = animations[i];
                if (animationComponent == null || !animationComponent.enabled)
                    continue;

                if (animationComponent.clip != null || animationComponent.isPlaying)
                    return Fail($"Animation is active before BaseRotation capture: {GetHierarchyPath(animationComponent.transform)}.");
            }

            return true;
        }

        private bool Fail(string message)
        {
            Debug.LogError($"[VirtualPartner] Stage 1 bootstrap failed: {message}", this);
            enabled = false;
            return false;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
