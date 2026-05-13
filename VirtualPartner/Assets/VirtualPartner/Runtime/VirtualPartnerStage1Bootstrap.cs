using System.Collections;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualPartnerStage1Bootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private CharacterRuntimeBinder characterRuntimeBinder;
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private IdleBaseProvider idleBaseProvider;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private PresetAnimationProfile presetAnimationProfile;
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private FSMProfile fsmProfile;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private MovementConstraintController movementConstraintController;
        [SerializeField] private StagePlanPlayer stagePlanPlayer;
        [SerializeField] private SpeechBubbleView speechBubbleView;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private LlmRelay llmRelay;
        [SerializeField] private AutonomousBehaviorDebugPanel autonomousBehaviorDebugPanel;
        [SerializeField] private RootLocomotionDebugPanel rootLocomotionDebugPanel;
        [SerializeField] private VirtualPartnerBoneDebugPanel boneDebugPanel;
        [SerializeField] private StagePlanDebugPanel stagePlanDebugPanel;
        [SerializeField] private LlmInteractionDebugPanel llmInteractionDebugPanel;
        [SerializeField] private VirtualPartnerRuntimeDebugPanel runtimeDebugPanel;

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
            rootOrientationController.Configure(characterRoot.transform, Camera.main);
            if (movementConstraintController != null)
                movementConstraintController.Configure(characterRoot.transform);
            locomotionActionExecutor.Configure(
                locomotionProfile,
                characterRoot,
                characterRoot.transform,
                boneRoot,
                avatarPoseApplier,
                actionCoordinator,
                movementConstraintController);
            if (speechBubbleView != null)
                speechBubbleView.Configure(characterRoot.transform);
            stagePlanPlayer.Configure(
                characterProfile,
                boneMapProfile,
                presetAnimationProfile,
                locomotionProfile,
                characterRoot,
                boneRoot,
                avatarPoseApplier,
                actionCoordinator,
                rootOrientationController,
                locomotionActionExecutor,
                speechBubbleView,
                autonomousBehaviorScheduler);
            autonomousBehaviorScheduler.Configure(
                fsmProfile,
                stagePlanPlayer,
                rootOrientationController);
            llmRelay.Configure(
                boneMapProfile,
                characterProfile,
                boneRoot,
                avatarPoseApplier,
                presetAnimationProfile,
                locomotionProfile,
                stagePlanPlayer,
                autonomousBehaviorScheduler);
            if (llmInteractionDebugPanel != null)
                llmInteractionDebugPanel.Configure(llmRelay);
            if (runtimeDebugPanel != null)
            {
                runtimeDebugPanel.Configure(
                    llmRelay,
                    stagePlanPlayer,
                    autonomousBehaviorScheduler,
                    rootOrientationController,
                    locomotionActionExecutor,
                    movementConstraintController,
                    actionCoordinator,
                    stagePlanDebugPanel,
                    llmInteractionDebugPanel,
                    autonomousBehaviorDebugPanel,
                    rootLocomotionDebugPanel,
                    boneDebugPanel);
            }

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
            stagePlanPlayer.ManualUpdate(0f, idleBaseProvider.Clip, currentIdleTime);
            actionCoordinator.FinalizeFrame(0f);
            idlePlaying = idleBaseProvider.IsPlaying;

            if (!characterRuntimeBinder.TryRegister(out var characterRegisterFailure))
            {
                Fail($"Character registration failed: {characterRegisterFailure}");
                yield break;
            }

            initialized = true;
            autonomousBehaviorScheduler.StartScheduler();

            Debug.Log(
                $"[VirtualPartner] Stage 1 initialized. BaseRotation bones: {registeredBoneCount}, Idle clip: {idleClip.name}.",
                this);
        }

        private void Update()
        {
            if (!initialized)
                return;

            ApplyIdleFrame(Time.deltaTime);
            var wasStagePlanPlaying = stagePlanPlayer.IsPlaying;
            stagePlanPlayer.ManualUpdate(Time.deltaTime, idleBaseProvider.Clip, currentIdleTime);
            if (!wasStagePlanPlaying && rootOrientationController != null)
                rootOrientationController.ManualUpdate(Time.deltaTime);
            llmRelay.ManualUpdate(Time.deltaTime);
            autonomousBehaviorScheduler.ManualUpdate(Time.deltaTime);
            actionCoordinator.FinalizeFrame(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (characterRuntimeBinder != null)
                characterRuntimeBinder.Unregister();
            if (llmRelay != null)
                llmRelay.StopPendingRequest();
            if (autonomousBehaviorScheduler != null)
                autonomousBehaviorScheduler.StopScheduler();
            if (stagePlanPlayer != null)
                stagePlanPlayer.StopStagePlan();
            if (actionCoordinator != null)
                actionCoordinator.ReleaseAllDebug();
            if (locomotionActionExecutor != null)
                locomotionActionExecutor.StopLocomotion();
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
            if (characterRuntimeBinder == null)
                return Fail("CharacterRuntimeBinder reference is missing.");
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.");
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
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (presetAnimationProfile == null)
                return Fail("PresetAnimationProfile reference is missing.");
            if (locomotionProfile == null)
                return Fail("LocomotionProfile reference is missing.");
            if (fsmProfile == null)
                return Fail("FSMProfile reference is missing.");
            if (rootOrientationController == null)
                return Fail("RootOrientationController reference is missing.");
            if (locomotionActionExecutor == null)
                return Fail("LocomotionActionExecutor reference is missing.");
            if (stagePlanPlayer == null)
                return Fail("StagePlanPlayer reference is missing.");
            if (speechBubbleView == null)
                return Fail("SpeechBubbleView reference is missing.");
            if (autonomousBehaviorScheduler == null)
                return Fail("AutonomousBehaviorScheduler reference is missing.");
            if (llmRelay == null)
                return Fail("LlmRelay reference is missing.");
            if (autonomousBehaviorDebugPanel == null)
                return Fail("AutonomousBehaviorDebugPanel reference is missing.");
            if (rootLocomotionDebugPanel == null)
                return Fail("RootLocomotionDebugPanel reference is missing.");
            if (boneDebugPanel == null)
                return Fail("VirtualPartnerBoneDebugPanel reference is missing.");
            if (llmInteractionDebugPanel == null)
                return Fail("LlmInteractionDebugPanel reference is missing.");
            if (runtimeDebugPanel == null)
                return Fail("VirtualPartnerRuntimeDebugPanel reference is missing.");
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
