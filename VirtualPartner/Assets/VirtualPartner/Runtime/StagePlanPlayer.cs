using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum StageActionStatus
    {
        Completed,
        Failed,
        Interrupted,
        Skipped,
        OwnershipDenied
    }

    public sealed class StageActionResult
    {
        public StageActionResult(
            int stageIndex,
            int actionIndex,
            string actionType,
            StageActionStatus status,
            string message)
        {
            StageIndex = stageIndex;
            ActionIndex = actionIndex;
            ActionType = actionType;
            Status = status;
            Message = message ?? string.Empty;
        }

        public int StageIndex { get; }
        public int ActionIndex { get; }
        public string ActionType { get; }
        public StageActionStatus Status { get; }
        public string Message { get; }
        public bool IsCompleted => Status == StageActionStatus.Completed;
    }

    public sealed class StagePlanSpeechEvent
    {
        public StagePlanSpeechEvent(
            string ownerId,
            string characterId,
            int requestId,
            string planId,
            int stageIndex,
            int actionIndex,
            string text)
        {
            OwnerId = ownerId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            RequestId = requestId;
            PlanId = planId ?? string.Empty;
            StageIndex = stageIndex;
            ActionIndex = actionIndex;
            Text = text ?? string.Empty;
        }

        public string OwnerId { get; }
        public string CharacterId { get; }
        public int RequestId { get; }
        public string PlanId { get; }
        public int StageIndex { get; }
        public int ActionIndex { get; }
        public string Text { get; }
    }

    public sealed class StagePlanFinishedEvent
    {
        public StagePlanFinishedEvent(
            string ownerId,
            string characterId,
            int requestId,
            string planId)
        {
            OwnerId = ownerId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            RequestId = requestId;
            PlanId = planId ?? string.Empty;
        }

        public string OwnerId { get; }
        public string CharacterId { get; }
        public int RequestId { get; }
        public string PlanId { get; }
    }

    [DisallowMultipleComponent]
    public sealed class StagePlanPlayer : MonoBehaviour
    {
        public const string StagePlanOwnerId = "StagePlan";

        [Header("References")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private PresetAnimationProfile presetAnimationProfile;
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private SpeechBubbleView speechBubbleView;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private ExpressionActionExecutor expressionActionExecutor;
        [SerializeField] private SpeechMouthDriver speechMouthDriver;
        [SerializeField] private TtsManager ttsManager;

        [Header("Settings")]
        [SerializeField] private float debugSpeechDurationSeconds = 1f;
        [SerializeField] private float defaultActionDurationSeconds = 1f;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool playing;
        [SerializeField] private int currentStageIndex = -1;
        [SerializeField] private int activeActionCount;
        [SerializeField] private int terminalActionCount;
        [SerializeField] private int validationErrorCount;
        [SerializeField] private int validationWarningCount;
        [SerializeField] private int runtimeWarningCount;
        [SerializeField] private int completedCount;
        [SerializeField] private int failedCount;
        [SerializeField] private int interruptedCount;
        [SerializeField] private int skippedCount;
        [SerializeField] private int ownershipDeniedCount;
        [SerializeField] private string currentStageStatus = "-";
        [SerializeField] private string statusText = "Idle";
        [SerializeField] private string lastMessage;
        [SerializeField] private string activeOwnerId;
        [SerializeField] private int activeRequestId;
        [SerializeField] private string activePlanId;

        private RunningStageAction pendingSpeechPlaybackAction;
        private bool subscribedToTtsPlaybackStarted;
        private bool currentStageSpeechPlaybackStarted;

        private enum StageActionKind
        {
            Speech,
            BonePose,
            Animation,
            Facing,
            Locomotion,
            Expression,
            Unknown
        }

        private readonly List<BoneMapInstance> controlInstances = new List<BoneMapInstance>();
        private readonly List<RunningStageAction> runningActions = new List<RunningStageAction>();
        private readonly List<BoneMapInstance> stageOwnedBones = new List<BoneMapInstance>();
        private readonly List<PresetAnimationBonePose> sampledPresetPoses = new List<PresetAnimationBonePose>();
        private readonly List<string> displacedPresetIds = new List<string>();
        private readonly List<PresetTransformSnapshot> presetTransformSnapshots = new List<PresetTransformSnapshot>();

        private StagePlanStageDto[] activeStages;
        private int presetSequence;

        public bool IsPlaying => playing;
        public int CurrentStageIndex => currentStageIndex;
        public int ActiveActionCount => activeActionCount;
        public int TerminalActionCount => terminalActionCount;
        public int ErrorCount => validationErrorCount;
        public int WarningCount => validationWarningCount + runtimeWarningCount;
        public int CompletedCount => completedCount;
        public int FailedCount => failedCount;
        public int InterruptedCount => interruptedCount;
        public int SkippedCount => skippedCount;
        public int OwnershipDeniedCount => ownershipDeniedCount;
        public string CurrentStageStatus => currentStageStatus;
        public string StatusText => statusText;
        public string LastMessage => lastMessage;
        public string ActiveOwnerId => activeOwnerId;
        public int ActiveRequestId => activeRequestId;
        public int CurrentLlmStagePlanRequestId => IsOwnerPlaying(LlmRelay.LlmOwnerId) ? activeRequestId : 0;
        public float DebugSpeechDurationSeconds => Mathf.Max(0.01f, debugSpeechDurationSeconds);

        // Raised when speech output actually begins, so chat/scene bubbles stay aligned with TTS playback or fallback.
        public event Action<StagePlanSpeechEvent> SpeechActionStarted;
        public event Action<StagePlanFinishedEvent> StagePlanFinished;

        private void OnDisable()
        {
            ClearPendingSpeechPlayback(null);
        }

        public void Configure(
            CharacterProfile profile,
            BoneMapProfile boneProfile,
            PresetAnimationProfile presetProfile,
            LocomotionProfile locomotion,
            GameObject character,
            Transform root,
            AvatarPoseApplier poseApplier,
            ActionCoordinator coordinator,
            RootOrientationController orientationController,
            LocomotionActionExecutor locomotionExecutor,
            SpeechBubbleView speechView,
            AutonomousBehaviorScheduler scheduler,
            ExpressionActionExecutor expressionExecutor,
            SpeechMouthDriver mouthDriver,
            TtsManager tts)
        {
            characterProfile = profile;
            boneMapProfile = boneProfile;
            presetAnimationProfile = presetProfile;
            locomotionProfile = locomotion;
            characterRoot = character;
            boneRoot = root;
            avatarPoseApplier = poseApplier;
            actionCoordinator = coordinator;
            rootOrientationController = orientationController;
            locomotionActionExecutor = locomotionExecutor;
            speechBubbleView = speechView;
            autonomousBehaviorScheduler = scheduler;
            expressionActionExecutor = expressionExecutor;
            speechMouthDriver = mouthDriver;
            ttsManager = tts;
            initialized = ValidateReferences();

            if (!initialized)
                return;

            var missingCount = boneMapProfile.BuildControlInstances(boneRoot, controlInstances);
            if (missingCount > 0)
                RecordWarning($"StagePlan BoneMap resolved with {missingCount} missing bone instance(s).");

            statusText = "Ready";
        }

        public StagePlanValidationResult ValidateStagePlanJson(string json)
        {
            var result = StagePlanValidator.Validate(json, characterProfile);
            validationErrorCount = result.ErrorCount;
            validationWarningCount = result.WarningCount;
            runtimeWarningCount = 0;
            lastMessage = result.IsValid ? "Validation passed." : "Validation failed.";
            statusText = result.IsValid ? "Validated" : "Validation Failed";
            LogValidationMessages(result);
            return result;
        }

        public bool PlayJson(string json)
        {
            return StartStagePlan(json, string.Empty, 0, string.Empty);
        }

        public bool ReplaceJson(string json)
        {
            return StartStagePlan(json, string.Empty, 0, string.Empty);
        }

        public bool PlayJsonForOwner(string json, string ownerId)
        {
            return StartStagePlan(json, ownerId, 0, string.Empty);
        }

        public bool ReplaceJsonForOwner(string json, string ownerId)
        {
            return StartStagePlan(json, ownerId, 0, string.Empty);
        }

        public bool PlayJsonForOwner(string json, string ownerId, int requestId)
        {
            return StartStagePlan(json, ownerId, requestId, string.Empty);
        }

        public bool ReplaceJsonForOwner(string json, string ownerId, int requestId)
        {
            return StartStagePlan(json, ownerId, requestId, string.Empty);
        }

        public bool IsOwnerPlaying(string ownerId)
        {
            return playing && SameOwner(activeOwnerId, ownerId);
        }

        public bool StopStagePlanForOwner(string ownerId)
        {
            if (!IsOwnerPlaying(ownerId))
                return false;

            StopActiveStagePlan(StageActionStatus.Interrupted, "StagePlan stopped.", UsesUserInteraction(ownerId));
            statusText = "Stopped";
            lastMessage = "StagePlan stopped.";
            return true;
        }

        public void StopStagePlan()
        {
            StopActiveStagePlan(StageActionStatus.Interrupted, "StagePlan stopped.", UsesUserInteraction(activeOwnerId));
            statusText = "Stopped";
            lastMessage = "StagePlan stopped.";
        }

        public void ManualUpdate(float deltaTime, AnimationClip idleClip, float idleTime)
        {
            if (!playing || activeStages == null)
                return;

            var frameDelta = Mathf.Max(0f, deltaTime);
            if (autonomousBehaviorScheduler != null && UsesUserInteraction(activeOwnerId))
                autonomousBehaviorScheduler.KeepUserInteractionAlive();

            if (rootOrientationController != null)
                rootOrientationController.ManualUpdate(frameDelta);

            UpdateRunningActions(frameDelta, idleClip, idleTime);

            if (AllActionsTerminal())
                CompleteCurrentStage();
        }

        private bool StartStagePlan(string json, string ownerId, int requestId, string planId)
        {
            if (!initialized && !ValidateReferences())
                return false;

            var normalizedOwnerId = NormalizeOwnerId(ownerId);
            if (SameOwner(normalizedOwnerId, LlmRelay.LlmOwnerId) && requestId <= 0)
            {
                lastMessage = "LLM StagePlan requestId is required.";
                statusText = "Validation Failed";
                Debug.LogError($"[VirtualPartner] StagePlan validation error: {lastMessage}", this);
                return false;
            }

            if (controlInstances.Count == 0)
                boneMapProfile.BuildControlInstances(boneRoot, controlInstances);

            var result = ValidateStagePlanJson(json);
            if (!result.IsValid)
                return false;

            StopActiveStagePlan(StageActionStatus.Interrupted, "StagePlan replaced.", false);

            if (autonomousBehaviorScheduler != null && UsesUserInteraction(normalizedOwnerId))
                autonomousBehaviorScheduler.EnterUserInteraction();

            ResetResultCounts();
            activeStages = result.Root.stages;
            currentStageIndex = -1;
            playing = true;
            activeOwnerId = normalizedOwnerId;
            activeRequestId = requestId;
            activePlanId = planId ?? string.Empty;
            statusText = "Playing";
            lastMessage = "StagePlan started.";
            StartNextStage();
            return true;
        }

        private void StartNextStage()
        {
            while (playing && activeStages != null)
            {
                currentStageIndex++;
                if (currentStageIndex >= activeStages.Length)
                {
                    FinishStagePlan();
                    return;
                }

                runningActions.Clear();
                terminalActionCount = 0;
                activeActionCount = 0;
                currentStageSpeechPlaybackStarted = false;

                var stage = activeStages[currentStageIndex];
                currentStageStatus = $"Stage {currentStageIndex + 1}";

                if (stage == null || stage.actions == null || stage.actions.Length == 0)
                {
                    RecordWarning($"stageIndex {currentStageIndex} has no playable actions and will be skipped.");
                    continue;
                }

                if (!StageHasSpeech(stage) && speechBubbleView != null)
                    speechBubbleView.Clear();

                StartStageActions(stage);
                activeActionCount = runningActions.Count;
                if (runningActions.Count == 0 || AllActionsTerminal())
                {
                    ReleaseStageOwners();
                    continue;
                }

                return;
            }
        }

        private void StartStageActions(StagePlanStageDto stage)
        {
            var locomotionStarted = false;
            var facingStarted = false;
            var syncBodyActionsToSpeech = StageHasSpeech(stage);

            for (var actionIndex = 0; actionIndex < stage.actions.Length; actionIndex++)
            {
                var action = stage.actions[actionIndex];
                var actionType = NormalizeType(action != null ? action.type : string.Empty);
                var actionKind = GetActionKind(actionType);

                if (actionType == "locomotion" && locomotionStarted)
                {
                    AddImmediateResult(actionIndex, action, StageActionStatus.Skipped, "Only one locomotion action is supported per stage.");
                    continue;
                }

                if (actionType == "facing" && facingStarted)
                {
                    AddImmediateResult(actionIndex, action, StageActionStatus.Skipped, "Only one facing action is supported per stage.");
                    continue;
                }

                var runningAction = new RunningStageAction(currentStageIndex, actionIndex, action != null ? action.type : string.Empty, actionKind, action);
                if (syncBodyActionsToSpeech
                    && !currentStageSpeechPlaybackStarted
                    && ShouldDelayForSpeechPlayback(actionKind))
                {
                    runningAction.WaitingForSpeechPlaybackStart = true;
                }
                else
                {
                    StartAction(runningAction);
                }

                runningActions.Add(runningAction);

                if (runningAction.Kind == StageActionKind.Locomotion && runningAction.Result == null)
                    locomotionStarted = true;
                if (runningAction.Kind == StageActionKind.Facing && runningAction.Result == null)
                    facingStarted = true;
            }
        }

        private void StartAction(RunningStageAction runningAction)
        {
            if (runningAction == null || runningAction.Result != null)
                return;

            runningAction.WaitingForSpeechPlaybackStart = false;
            if (runningAction.Action == null)
            {
                CompleteAction(runningAction, StageActionStatus.Skipped, "Action is null.");
                return;
            }

            switch (runningAction.Kind)
            {
                case StageActionKind.Speech:
                    StartSpeechAction(runningAction);
                    break;
                case StageActionKind.BonePose:
                    StartBonePoseAction(runningAction);
                    break;
                case StageActionKind.Animation:
                    StartAnimationAction(runningAction);
                    break;
                case StageActionKind.Facing:
                    StartFacingAction(runningAction);
                    break;
                case StageActionKind.Locomotion:
                    StartLocomotionAction(runningAction);
                    break;
                case StageActionKind.Expression:
                    StartExpressionAction(runningAction);
                    break;
                default:
                    CompleteAction(runningAction, StageActionStatus.Skipped, $"Unknown action type '{runningAction.Action.type}'.");
                    break;
            }
        }

        private void AddImmediateResult(int actionIndex, StagePlanActionDto action, StageActionStatus status, string message)
        {
            var runningAction = new RunningStageAction(
                currentStageIndex,
                actionIndex,
                action != null ? action.type : string.Empty,
                GetActionKind(action != null ? action.type : string.Empty),
                action);
            CompleteAction(runningAction, status, message);
            runningActions.Add(runningAction);
        }

        private void StartSpeechAction(RunningStageAction runningAction)
        {
            if (speechBubbleView == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "SpeechBubbleView reference is missing.");
                return;
            }

            if (ttsManager == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "TtsManager reference is missing.");
                return;
            }

            WatchSpeechPlaybackStart(runningAction);
            if (!ttsManager.StartSpeech(runningAction.Action, DebugSpeechDurationSeconds, out var failureReason))
            {
                ClearPendingSpeechPlayback(runningAction);
                CompleteAction(runningAction, StageActionStatus.Failed, failureReason);
                return;
            }

            runningAction.Duration = ttsManager.Duration;
        }

        private void WatchSpeechPlaybackStart(RunningStageAction runningAction)
        {
            pendingSpeechPlaybackAction = runningAction;
            if (ttsManager == null || subscribedToTtsPlaybackStarted)
                return;

            ttsManager.SpeechPlaybackStarted += HandleTtsSpeechPlaybackStarted;
            subscribedToTtsPlaybackStarted = true;
        }

        private void ClearPendingSpeechPlayback(RunningStageAction runningAction)
        {
            if (runningAction != null && pendingSpeechPlaybackAction != runningAction)
                return;

            if (ttsManager != null && subscribedToTtsPlaybackStarted)
                ttsManager.SpeechPlaybackStarted -= HandleTtsSpeechPlaybackStarted;

            subscribedToTtsPlaybackStarted = false;
            pendingSpeechPlaybackAction = null;
        }

        private void HandleTtsSpeechPlaybackStarted()
        {
            var runningAction = pendingSpeechPlaybackAction;
            if (runningAction == null || runningAction.Result != null || runningAction.SpeechEventRaised)
                return;

            runningAction.SpeechEventRaised = true;
            currentStageSpeechPlaybackStarted = true;
            StartDelayedActionsForSpeechPlayback();
            speechBubbleView.Show(runningAction.Action.text);
            SpeechActionStarted?.Invoke(new StagePlanSpeechEvent(
                activeOwnerId,
                characterProfile != null ? characterProfile.CharacterId : string.Empty,
                activeRequestId,
                activePlanId,
                runningAction.StageIndex,
                runningAction.ActionIndex,
                runningAction.Action.text));
            ClearPendingSpeechPlayback(runningAction);
        }

        private void StartDelayedActionsForSpeechPlayback()
        {
            currentStageSpeechPlaybackStarted = true;
            for (var i = 0; i < runningActions.Count; i++)
            {
                var action = runningActions[i];
                if (action.Result != null || !action.WaitingForSpeechPlaybackStart)
                    continue;

                StartAction(action);
            }
        }

        private void StartBonePoseAction(RunningStageAction runningAction)
        {
            if (actionCoordinator == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "ActionCoordinator reference is missing.");
                return;
            }

            var action = runningAction.Action;
            if (action.bones == null || action.bones.Length == 0)
            {
                CompleteAction(runningAction, StageActionStatus.Skipped, "bonePose action has no bones.");
                return;
            }

            runningAction.Duration = GetDefaultedDuration(action.duration);
            var acquiredBones = runningAction.OwnedBones;
            for (var i = 0; i < action.bones.Length; i++)
            {
                if (!TryBuildDesiredBone(runningAction.StageIndex, action.bones[i], out var desiredBone, out var buildFailure))
                {
                    ReleaseBones(acquiredBones);
                    CompleteAction(runningAction, StageActionStatus.Failed, buildFailure);
                    return;
                }

                if (!actionCoordinator.RequestStagePlanBonePose(
                        desiredBone.Instance,
                        desiredBone.Rotation,
                        runningAction.Duration,
                        out var requestFailure))
                {
                    ReleaseBones(acquiredBones);
                    CompleteAction(runningAction, ClassifyOwnershipFailure(requestFailure), requestFailure);
                    return;
                }

                acquiredBones.Add(desiredBone.Instance);
                if (!stageOwnedBones.Contains(desiredBone.Instance))
                    stageOwnedBones.Add(desiredBone.Instance);
            }
        }

        private void StartAnimationAction(RunningStageAction runningAction)
        {
            if (presetAnimationProfile == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "PresetAnimationProfile reference is missing.");
                return;
            }

            if (!presetAnimationProfile.TryBuildClipBinding(runningAction.Action.name, boneRoot, out var binding, out var reason))
            {
                CompleteAction(runningAction, StageActionStatus.Failed, reason);
                return;
            }

            if (binding.Clip == null || binding.Clip.length <= 0f)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, $"Preset animation '{binding.ActionName}' has no playable length.");
                return;
            }

            presetSequence++;
            runningAction.InstanceId = $"{currentStageIndex}:{runningAction.ActionIndex}:{binding.ActionName}:{presetSequence}";
            runningAction.PresetBinding = binding;
            runningAction.Duration = binding.Clip.length;
        }

        private void StartFacingAction(RunningStageAction runningAction)
        {
            if (rootOrientationController == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "RootOrientationController reference is missing.");
                return;
            }

            runningAction.Duration = GetDefaultedDuration(runningAction.Action.duration);
            if (!rootOrientationController.RequestStagePlanFacing(runningAction.Action.target, runningAction.Duration, out var failureReason))
            {
                CompleteAction(runningAction, StageActionStatus.Failed, failureReason);
                return;
            }
        }

        private void StartLocomotionAction(RunningStageAction runningAction)
        {
            if (locomotionActionExecutor == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "LocomotionActionExecutor reference is missing.");
                return;
            }

            runningAction.Duration = GetDefaultedDuration(runningAction.Action.duration);
            if (!locomotionActionExecutor.StartLocomotion(runningAction.Action.mode, runningAction.Duration, out var failureReason))
            {
                CompleteAction(runningAction, ClassifyOwnershipFailure(failureReason), failureReason);
                return;
            }
        }

        private void StartExpressionAction(RunningStageAction runningAction)
        {
            if (expressionActionExecutor == null)
            {
                CompleteAction(runningAction, StageActionStatus.Failed, "ExpressionActionExecutor reference is missing.");
                return;
            }

            runningAction.Duration = GetDefaultedDuration(runningAction.Action.duration);
            if (!expressionActionExecutor.StartExpression(runningAction.Action.name, runningAction.Duration, out var failureReason))
            {
                CompleteAction(runningAction, StageActionStatus.Failed, failureReason);
                return;
            }

            if (speechMouthDriver != null)
                speechMouthDriver.RefreshSpeakingPoseSet();
        }

        private void UpdateRunningActions(float deltaTime, AnimationClip idleClip, float idleTime)
        {
            for (var i = 0; i < runningActions.Count; i++)
            {
                var action = runningActions[i];
                if (action.Result != null)
                    continue;
                if (action.WaitingForSpeechPlaybackStart)
                    continue;

                switch (action.Kind)
                {
                    case StageActionKind.Speech:
                        UpdateSpeechAction(action, deltaTime);
                        break;
                    case StageActionKind.BonePose:
                    case StageActionKind.Expression:
                        UpdateTimedAction(action, deltaTime);
                        break;
                    case StageActionKind.Animation:
                        UpdateAnimationAction(action, deltaTime, idleClip, idleTime);
                        break;
                    case StageActionKind.Facing:
                        UpdateFacingAction(action, deltaTime);
                        break;
                    case StageActionKind.Locomotion:
                        UpdateLocomotionAction(action, deltaTime, idleClip, idleTime);
                        break;
                }
            }

            StopPresetAnimationsThatLostOwnership();
        }

        private void UpdateSpeechAction(RunningStageAction action, float deltaTime)
        {
            action.Elapsed += deltaTime;
            if (ttsManager == null)
            {
                if (action.Elapsed >= action.Duration)
                    CompleteAction(action, StageActionStatus.Completed, "Completed.");
                return;
            }

            action.Duration = Mathf.Max(action.Duration, ttsManager.Duration);
            if (ttsManager.HasTerminalResult)
                CompleteAction(action, ttsManager.TerminalStatus, ttsManager.TerminalMessage);
        }

        private void UpdateTimedAction(RunningStageAction action, float deltaTime)
        {
            action.Elapsed += deltaTime;
            if (action.Elapsed >= action.Duration)
                CompleteAction(action, StageActionStatus.Completed, "Completed.");
        }

        private void UpdateAnimationAction(RunningStageAction action, float deltaTime, AnimationClip idleClip, float idleTime)
        {
            action.Elapsed += deltaTime;
            if (action.Elapsed >= action.Duration)
            {
                if (action.PresetStarted && actionCoordinator != null)
                    actionCoordinator.ReleasePresetAnimation(action.InstanceId);

                CompleteAction(action, StageActionStatus.Completed, "Completed.");
                return;
            }

            if (!TrySamplePresetAnimation(action.PresetBinding, action.Elapsed, idleClip, idleTime, sampledPresetPoses, out var sampleFailure))
            {
                if (action.PresetStarted && actionCoordinator != null)
                    actionCoordinator.ReleasePresetAnimation(action.InstanceId);

                CompleteAction(action, StageActionStatus.Failed, sampleFailure);
                return;
            }

            displacedPresetIds.Clear();
            if (!actionCoordinator.RequestPresetAnimation(
                    action.InstanceId,
                    action.PresetBinding.ActionName,
                    sampledPresetPoses,
                    displacedPresetIds,
                    out var requestFailure))
            {
                CompleteAction(action, ClassifyOwnershipFailure(requestFailure), requestFailure);
                return;
            }

            action.PresetStarted = true;
            CompleteDisplacedPresetActions(displacedPresetIds);
        }

        private void UpdateFacingAction(RunningStageAction action, float deltaTime)
        {
            action.Elapsed += deltaTime;
            if (!rootOrientationController.IsTurning || action.Elapsed >= action.Duration)
                CompleteAction(action, StageActionStatus.Completed, "Completed.");
        }

        private void UpdateLocomotionAction(RunningStageAction action, float deltaTime, AnimationClip idleClip, float idleTime)
        {
            action.Elapsed += deltaTime;
            locomotionActionExecutor.ManualUpdate(deltaTime, idleClip, idleTime);
            if (locomotionActionExecutor.IsActive)
            {
                action.LocomotionObservedActive = true;
                return;
            }

            if (action.Elapsed >= action.Duration - 0.001f)
            {
                CompleteAction(action, StageActionStatus.Completed, "Completed.");
                return;
            }

            var message = string.IsNullOrWhiteSpace(locomotionActionExecutor.LastMessage)
                ? "Locomotion stopped before duration completed."
                : locomotionActionExecutor.LastMessage;
            CompleteAction(action, ClassifyOwnershipFailure(message), message);
        }

        private void StopPresetAnimationsThatLostOwnership()
        {
            for (var i = 0; i < runningActions.Count; i++)
            {
                var action = runningActions[i];
                if (action.Result != null || action.Kind != StageActionKind.Animation || !action.PresetStarted)
                    continue;

                if (StillOwnsAllPresetBones(action))
                    continue;

                CompleteAction(action, StageActionStatus.OwnershipDenied, "Preset animation lost bone ownership.");
            }
        }

        private void CompleteDisplacedPresetActions(List<string> presetIds)
        {
            for (var idIndex = 0; idIndex < presetIds.Count; idIndex++)
            {
                var presetId = presetIds[idIndex];
                for (var i = 0; i < runningActions.Count; i++)
                {
                    var action = runningActions[i];
                    if (action.Result != null || action.InstanceId != presetId)
                        continue;

                    CompleteAction(action, StageActionStatus.OwnershipDenied, "Preset animation was displaced by another preset animation.");
                }
            }
        }

        private void CompleteCurrentStage()
        {
            ReleaseStageOwners();
            ReleaseStageExpression();
            StartNextStage();
        }

        private void FinishStagePlan()
        {
            var exitInteraction = UsesUserInteraction(activeOwnerId);
            var finishedOwnerId = activeOwnerId;
            var finishedRequestId = activeRequestId;
            var finishedPlanId = activePlanId;
            var finishedCharacterId = characterProfile != null ? characterProfile.CharacterId : string.Empty;
            playing = false;
            activeStages = null;
            runningActions.Clear();
            currentStageIndex = -1;
            activeActionCount = 0;
            terminalActionCount = 0;
            currentStageStatus = "-";
            activeOwnerId = string.Empty;
            activeRequestId = 0;
            activePlanId = string.Empty;
            statusText = "Finished";
            lastMessage = "StagePlan finished.";

            if (speechBubbleView != null)
                speechBubbleView.Clear();
            if (ttsManager != null)
                ttsManager.ReleaseSpeech();
            else if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();
            if (expressionActionExecutor != null)
                expressionActionExecutor.ClearExpression();
            if (autonomousBehaviorScheduler != null && exitInteraction)
                autonomousBehaviorScheduler.ExitUserInteraction();

            StagePlanFinished?.Invoke(new StagePlanFinishedEvent(
                finishedOwnerId,
                finishedCharacterId,
                finishedRequestId,
                finishedPlanId));
        }

        private void StopActiveStagePlan(StageActionStatus status, string message, bool exitInteraction)
        {
            if (playing)
            {
                for (var i = 0; i < runningActions.Count; i++)
                {
                    if (runningActions[i].Result == null)
                        CompleteAction(runningActions[i], status, message);
                }
            }

            ReleaseStageOwners();
            ReleaseActivePresetAnimations();
            if (locomotionActionExecutor != null)
                locomotionActionExecutor.StopLocomotion();
            if (rootOrientationController != null)
                rootOrientationController.StopStagePlanFacing();
            if (speechBubbleView != null)
                speechBubbleView.Clear();
            if (ttsManager != null)
                ttsManager.ReleaseSpeech();
            else if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();
            if (expressionActionExecutor != null)
                expressionActionExecutor.ClearExpression();

            playing = false;
            activeStages = null;
            runningActions.Clear();
            currentStageIndex = -1;
            activeActionCount = 0;
            terminalActionCount = 0;
            currentStageStatus = "-";
            activeOwnerId = string.Empty;
            activeRequestId = 0;
            activePlanId = string.Empty;

            if (exitInteraction && autonomousBehaviorScheduler != null)
                autonomousBehaviorScheduler.ExitUserInteraction();
        }

        private void ReleaseStageOwners()
        {
            ReleaseBones(stageOwnedBones);
            stageOwnedBones.Clear();
        }

        private void ReleaseStageExpression()
        {
            if (expressionActionExecutor != null)
                expressionActionExecutor.ClearExpression();
        }

        private void ReleaseBones(List<BoneMapInstance> bones)
        {
            if (actionCoordinator == null || bones == null)
                return;

            for (var i = 0; i < bones.Count; i++)
                actionCoordinator.ReleaseStagePlanBonePose(bones[i]);
        }

        private void ReleaseActivePresetAnimations()
        {
            if (actionCoordinator == null)
                return;

            for (var i = 0; i < runningActions.Count; i++)
            {
                var action = runningActions[i];
                if (action.Kind == StageActionKind.Animation && action.PresetStarted)
                    actionCoordinator.ReleasePresetAnimation(action.InstanceId);
            }
        }

        private bool AllActionsTerminal()
        {
            if (runningActions.Count == 0)
                return true;

            for (var i = 0; i < runningActions.Count; i++)
            {
                if (runningActions[i].Result == null)
                    return false;
            }

            return true;
        }

        private void CompleteAction(RunningStageAction action, StageActionStatus status, string message)
        {
            if (action == null || action.Result != null)
                return;

            if (action.Kind == StageActionKind.Speech)
            {
                if (!action.SpeechEventRaised && status != StageActionStatus.Interrupted)
                    StartDelayedActionsForSpeechPlayback();

                ClearPendingSpeechPlayback(action);
                if (ttsManager != null)
                {
                    if (status == StageActionStatus.Completed)
                        ttsManager.ReleaseSpeech();
                    else
                        ttsManager.StopSpeech(message);
                }
                else if (speechMouthDriver != null)
                {
                    speechMouthDriver.StopSpeech();
                }
            }

            action.Result = new StageActionResult(action.StageIndex, action.ActionIndex, action.ActionType, status, message);
            terminalActionCount++;

            switch (status)
            {
                case StageActionStatus.Completed:
                    completedCount++;
                    break;
                case StageActionStatus.Failed:
                    failedCount++;
                    break;
                case StageActionStatus.Interrupted:
                    interruptedCount++;
                    break;
                case StageActionStatus.Skipped:
                    skippedCount++;
                    break;
                case StageActionStatus.OwnershipDenied:
                    ownershipDeniedCount++;
                    break;
            }

            lastMessage = $"stageIndex {action.StageIndex} action {action.ActionIndex} {status}: {message}";
            if (status != StageActionStatus.Completed)
                RecordWarning(lastMessage);
        }

        private bool TryBuildDesiredBone(
            int stageIndex,
            StagePlanBonePoseDto bonePose,
            out DesiredStageBone desiredBone,
            out string failureReason)
        {
            desiredBone = null;
            failureReason = string.Empty;

            if (bonePose == null)
            {
                failureReason = $"stageIndex {stageIndex} bonePose contains a null bone entry.";
                return false;
            }

            if (bonePose.rotation == null)
            {
                failureReason = $"stageIndex {stageIndex} bonePose '{bonePose.bone}' is missing rotation.";
                return false;
            }

            if (!TryResolveBoneInstance(bonePose.bone, bonePose.side, out var instance, out var reason))
            {
                failureReason = $"stageIndex {stageIndex} bonePose skipped: {reason}";
                return false;
            }

            var rotation = bonePose.rotation.ToVector3();
            var clampedRotation = instance.Entry.ClampRotation(rotation);
            if (!Approximately(rotation, clampedRotation))
                RecordWarning($"stageIndex {stageIndex} {instance.DisplayName} rotation was clamped from {Format(rotation)} to {Format(clampedRotation)}.");

            desiredBone = new DesiredStageBone(instance, clampedRotation);
            return true;
        }

        private bool TryResolveBoneInstance(
            string boneName,
            string sideName,
            out BoneMapInstance instance,
            out string reason)
        {
            instance = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(boneName))
            {
                reason = "Bone name is missing.";
                return false;
            }

            if (!Enum.TryParse(boneName, true, out SemanticBone semanticBone))
            {
                reason = $"Unknown semantic bone '{boneName}'.";
                return false;
            }

            var side = BoneSide.None;
            if (!string.IsNullOrWhiteSpace(sideName) && !Enum.TryParse(sideName, true, out side))
            {
                reason = $"Invalid side '{sideName}' for {semanticBone}.";
                return false;
            }

            var hasSideVariant = false;
            for (var i = 0; i < controlInstances.Count; i++)
            {
                var candidate = controlInstances[i];
                if (candidate.SemanticBone != semanticBone)
                    continue;

                if (candidate.Side != BoneSide.None)
                    hasSideVariant = true;

                if (candidate.Side != side)
                    continue;

                instance = candidate;
                return true;
            }

            reason = hasSideVariant && side == BoneSide.None
                ? $"{semanticBone} requires side L or R."
                : $"BoneMap has no instance for {semanticBone} {side}.";
            return false;
        }

        private bool TrySamplePresetAnimation(
            PresetAnimationClipBinding binding,
            float clipTime,
            AnimationClip idleClip,
            float idleTime,
            List<PresetAnimationBonePose> poses,
            out string failureReason)
        {
            poses.Clear();
            failureReason = string.Empty;

            if (binding == null || binding.Clip == null)
            {
                failureReason = "Clip binding is missing.";
                return false;
            }

            if (characterRoot == null || boneRoot == null)
            {
                failureReason = "Character root reference is missing.";
                return false;
            }

            var rootTransform = characterRoot.transform;
            var rootPosition = rootTransform.position;
            var rootRotation = rootTransform.rotation;
            var rootLocalScale = rootTransform.localScale;
            CapturePresetTransformSnapshot();

            try
            {
                binding.Clip.SampleAnimation(characterRoot, clipTime);

                var targets = binding.Targets;
                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target == null || target.Transform == null)
                    {
                        failureReason = $"Preset animation '{binding.ActionName}' has a missing target transform.";
                        return false;
                    }

                    poses.Add(new PresetAnimationBonePose(
                        target.Transform,
                        target.DisplayName,
                        target.Transform.localRotation,
                        target.Transform.localPosition));
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                RestorePresetTransformSnapshot();
                if (avatarPoseApplier != null && idleClip != null)
                    avatarPoseApplier.ApplyIdle(idleClip, idleTime);
                rootTransform.position = rootPosition;
                rootTransform.rotation = rootRotation;
                rootTransform.localScale = rootLocalScale;
            }

            return poses.Count > 0;
        }

        private void CapturePresetTransformSnapshot()
        {
            presetTransformSnapshots.Clear();

            if (boneRoot == null)
                return;

            var transforms = boneRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                presetTransformSnapshots.Add(new PresetTransformSnapshot(transforms[i]));
        }

        private void RestorePresetTransformSnapshot()
        {
            for (var i = 0; i < presetTransformSnapshots.Count; i++)
                presetTransformSnapshots[i].Restore();

            presetTransformSnapshots.Clear();
        }

        private bool StillOwnsAllPresetBones(RunningStageAction action)
        {
            if (action == null || action.PresetBinding == null || actionCoordinator == null)
                return false;

            var targets = action.PresetBinding.Targets;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || !actionCoordinator.IsPresetAnimationOwner(target.Transform, action.InstanceId))
                    return false;
            }

            return true;
        }

        private bool ValidateReferences()
        {
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.");
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (presetAnimationProfile == null)
                return Fail("PresetAnimationProfile reference is missing.");
            if (locomotionProfile == null)
                return Fail("LocomotionProfile reference is missing.");
            if (characterRoot == null)
                return Fail("Character root reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.");
            if (actionCoordinator == null)
                return Fail("ActionCoordinator reference is missing.");
            if (rootOrientationController == null)
                return Fail("RootOrientationController reference is missing.");
            if (locomotionActionExecutor == null)
                return Fail("LocomotionActionExecutor reference is missing.");
            if (speechBubbleView == null)
                return Fail("SpeechBubbleView reference is missing.");
            if (autonomousBehaviorScheduler == null)
                return Fail("AutonomousBehaviorScheduler reference is missing.");
            if (expressionActionExecutor == null)
                return Fail("ExpressionActionExecutor reference is missing.");
            if (speechMouthDriver == null)
                return Fail("SpeechMouthDriver reference is missing.");
            if (ttsManager == null)
                return Fail("TtsManager reference is missing.");

            initialized = true;
            return true;
        }

        private bool Fail(string message)
        {
            initialized = false;
            statusText = "Failed";
            lastMessage = message;
            Debug.LogError($"[VirtualPartner] StagePlanPlayer failed: {message}", this);
            enabled = false;
            return false;
        }

        private void LogValidationMessages(StagePlanValidationResult result)
        {
            for (var i = 0; i < result.Errors.Count; i++)
                Debug.LogError($"[VirtualPartner] StagePlan validation error: {result.Errors[i]}", this);
            for (var i = 0; i < result.Warnings.Count; i++)
                Debug.LogWarning($"[VirtualPartner] StagePlan validation warning: {result.Warnings[i]}", this);
        }

        private void RecordWarning(string message)
        {
            runtimeWarningCount++;
            lastMessage = message;
            Debug.LogWarning($"[VirtualPartner] StagePlan warning: {message}", this);
        }

        private void ResetResultCounts()
        {
            runtimeWarningCount = 0;
            completedCount = 0;
            failedCount = 0;
            interruptedCount = 0;
            skippedCount = 0;
            ownershipDeniedCount = 0;
        }

        private float GetDefaultedDuration(float duration)
        {
            return duration > 0f ? duration : Mathf.Max(0.01f, defaultActionDurationSeconds);
        }

        private static bool StageHasSpeech(StagePlanStageDto stage)
        {
            if (stage == null || stage.actions == null)
                return false;

            for (var i = 0; i < stage.actions.Length; i++)
            {
                if (stage.actions[i] != null && NormalizeType(stage.actions[i].type) == "speech")
                    return true;
            }

            return false;
        }

        private static StageActionStatus ClassifyOwnershipFailure(string message)
        {
            return !string.IsNullOrWhiteSpace(message)
                && message.IndexOf("owned by", StringComparison.OrdinalIgnoreCase) >= 0
                    ? StageActionStatus.OwnershipDenied
                    : StageActionStatus.Failed;
        }

        private static StageActionKind GetActionKind(string actionType)
        {
            switch (NormalizeType(actionType))
            {
                case "speech":
                    return StageActionKind.Speech;
                case "bonepose":
                    return StageActionKind.BonePose;
                case "animation":
                    return StageActionKind.Animation;
                case "facing":
                    return StageActionKind.Facing;
                case "locomotion":
                    return StageActionKind.Locomotion;
                case "expression":
                    return StageActionKind.Expression;
                default:
                    return StageActionKind.Unknown;
            }
        }

        private static bool ShouldDelayForSpeechPlayback(StageActionKind kind)
        {
            switch (kind)
            {
                case StageActionKind.BonePose:
                case StageActionKind.Animation:
                case StageActionKind.Facing:
                case StageActionKind.Locomotion:
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeType(string actionType)
        {
            return string.IsNullOrWhiteSpace(actionType)
                ? string.Empty
                : actionType.Trim().ToLowerInvariant();
        }

        private static string NormalizeOwnerId(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : ownerId.Trim();
        }

        private static bool SameOwner(string left, string right)
        {
            return string.Equals(
                NormalizeOwnerId(left),
                NormalizeOwnerId(right),
                StringComparison.Ordinal);
        }

        private static bool UsesUserInteraction(string ownerId)
        {
            return !SameOwner(ownerId, AutonomousBehaviorScheduler.FsmOwnerId);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.001f
                && Mathf.Abs(left.y - right.y) < 0.001f
                && Mathf.Abs(left.z - right.z) < 0.001f;
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private sealed class RunningStageAction
        {
            public RunningStageAction(
                int stageIndex,
                int actionIndex,
                string actionType,
                StageActionKind kind,
                StagePlanActionDto action)
            {
                StageIndex = stageIndex;
                ActionIndex = actionIndex;
                ActionType = actionType;
                Kind = kind;
                Action = action;
            }

            public int StageIndex { get; }
            public int ActionIndex { get; }
            public string ActionType { get; }
            public StageActionKind Kind { get; }
            public StagePlanActionDto Action { get; }
            public float Elapsed { get; set; }
            public float Duration { get; set; }
            public string InstanceId { get; set; }
            public PresetAnimationClipBinding PresetBinding { get; set; }
            public bool PresetStarted { get; set; }
            public bool LocomotionObservedActive { get; set; }
            public bool SpeechEventRaised { get; set; }
            public bool WaitingForSpeechPlaybackStart { get; set; }
            public StageActionResult Result { get; set; }
            public List<BoneMapInstance> OwnedBones { get; } = new List<BoneMapInstance>();
        }

        private sealed class DesiredStageBone
        {
            public DesiredStageBone(BoneMapInstance instance, Vector3 rotation)
            {
                Instance = instance;
                Rotation = rotation;
            }

            public BoneMapInstance Instance { get; }
            public Vector3 Rotation { get; }
        }

        private readonly struct PresetTransformSnapshot
        {
            public PresetTransformSnapshot(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }

            public Transform Transform { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }

            public void Restore()
            {
                if (Transform == null)
                    return;

                Transform.localPosition = LocalPosition;
                Transform.localRotation = LocalRotation;
                Transform.localScale = LocalScale;
            }
        }
    }
}
