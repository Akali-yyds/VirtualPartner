using System.Globalization;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum AutonomousBehaviorState
    {
        Disabled,
        Waiting,
        Turning,
        PlayingAction,
        PausedByExternalStagePlan,
        UserInteraction
    }

    [DisallowMultipleComponent]
    public sealed class AutonomousBehaviorScheduler : MonoBehaviour
    {
        public const string FsmOwnerId = "FSM";

        [Header("References")]
        [SerializeField] private FSMProfile fsmProfile;
        [SerializeField] private StagePlanPlayer stagePlanPlayer;
        [SerializeField] private RootOrientationController rootOrientationController;

        [Header("Settings")]
        [SerializeField] private float userInteractionTimeout = 10f;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool schedulerActive;
        [SerializeField] private AutonomousBehaviorState state = AutonomousBehaviorState.Disabled;
        [SerializeField] private float waitRemaining;
        [SerializeField] private bool inUserInteraction;
        [SerializeField] private float interactionRemaining;
        [SerializeField] private string currentActionName;
        [SerializeField] private string lastMessage;

        private FSMActionEntry pendingAction;

        public bool SchedulerActive => schedulerActive;
        public AutonomousBehaviorState State => state;
        public float WaitRemaining => waitRemaining;
        public bool IsInUserInteraction => inUserInteraction;
        public float InteractionRemaining => interactionRemaining;
        public string CurrentActionName => currentActionName;
        public string LastMessage => lastMessage;

        public void SetUserInteractionTimeout(float timeout)
        {
            userInteractionTimeout = Mathf.Max(0f, timeout);
            if (inUserInteraction)
                interactionRemaining = Mathf.Max(interactionRemaining, userInteractionTimeout);
        }

        public void Configure(
            FSMProfile profile,
            StagePlanPlayer player,
            RootOrientationController orientationController)
        {
            fsmProfile = profile;
            stagePlanPlayer = player;
            rootOrientationController = orientationController;
            initialized = ValidateReferences();
            state = AutonomousBehaviorState.Disabled;
            waitRemaining = 0f;
            interactionRemaining = 0f;
            currentActionName = string.Empty;
            lastMessage = initialized ? "FSM ready." : lastMessage;
        }

        public void StartScheduler()
        {
            if (!initialized && !ValidateReferences())
                return;

            schedulerActive = true;
            if (inUserInteraction)
            {
                state = AutonomousBehaviorState.UserInteraction;
                return;
            }

            BeginWaiting("FSM started.");
        }

        public void StopScheduler()
        {
            StopOwnedStagePlan();
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            schedulerActive = false;
            state = AutonomousBehaviorState.Disabled;
            waitRemaining = 0f;
            currentActionName = string.Empty;
            pendingAction = null;
            lastMessage = "FSM disabled.";
        }

        public void SetSchedulerActive(bool active)
        {
            if (active)
                StartScheduler();
            else
                StopScheduler();
        }

        public void EnterUserInteraction()
        {
            if (!initialized && !ValidateReferences())
                return;

            interactionRemaining = Mathf.Max(0f, userInteractionTimeout);

            if (inUserInteraction)
            {
                lastMessage = "User interaction timeout refreshed.";
                return;
            }

            StopOwnedStagePlan();
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            inUserInteraction = true;
            state = AutonomousBehaviorState.UserInteraction;
            waitRemaining = 0f;
            currentActionName = string.Empty;
            pendingAction = null;
            if (rootOrientationController != null)
                rootOrientationController.EnterUserInteraction();

            lastMessage = "User interaction entered.";
        }

        public void KeepUserInteractionAlive()
        {
            if (!initialized && !ValidateReferences())
                return;

            if (!inUserInteraction)
            {
                EnterUserInteraction();
                return;
            }

            interactionRemaining = Mathf.Max(0f, userInteractionTimeout);
        }

        public void ExitUserInteraction()
        {
            if (!inUserInteraction)
                return;

            inUserInteraction = false;
            interactionRemaining = 0f;

            if (rootOrientationController != null)
                rootOrientationController.ExitUserInteraction();

            if (schedulerActive)
                BeginWaiting("User interaction exited.");
            else
                state = AutonomousBehaviorState.Disabled;
        }

        public void ManualUpdate(float deltaTime)
        {
            if (!initialized || !schedulerActive)
                return;

            var frameDelta = Mathf.Max(0f, deltaTime);

            if (inUserInteraction)
            {
                state = AutonomousBehaviorState.UserInteraction;
                interactionRemaining -= frameDelta;
                if (interactionRemaining <= 0f)
                    ExitUserInteraction();
                return;
            }

            if (IsExternalStagePlanPlaying())
            {
                PauseForExternalStagePlan();
                return;
            }

            if (state == AutonomousBehaviorState.PausedByExternalStagePlan)
                BeginWaiting("External StagePlan finished.");

            if (state == AutonomousBehaviorState.Waiting)
            {
                waitRemaining -= frameDelta;
                if (waitRemaining <= 0f)
                    StartNextAction();
                return;
            }

            if (state == AutonomousBehaviorState.Turning)
            {
                if (rootOrientationController != null && rootOrientationController.IsTurning)
                    return;

                StartActionStagePlan(pendingAction);
                return;
            }

            if (state == AutonomousBehaviorState.PlayingAction)
                UpdatePlayingAction();
        }

        private void StartNextAction()
        {
            if (stagePlanPlayer.IsPlaying)
            {
                PauseForExternalStagePlan();
                return;
            }

            if (!fsmProfile.TryPickAction(out pendingAction))
            {
                BeginWaiting("No enabled FSM action is available.");
                return;
            }

            currentActionName = string.IsNullOrWhiteSpace(pendingAction.ActionName)
                ? pendingAction.ActionType.ToString()
                : pendingAction.ActionName;

            if (pendingAction.ActionType != FSMActionType.Locomotion)
            {
                StartActionStagePlan(pendingAction);
                return;
            }

            var yaw = Random.Range(0f, 360f);
            var failureReason = "RootOrientationController reference is missing.";
            if (rootOrientationController == null ||
                !rootOrientationController.RequestWorldYawFacing(
                    yaw,
                    rootOrientationController.DefaultTurnDuration,
                    out failureReason))
            {
                BeginWaiting($"FSM turn skipped: {failureReason}");
                return;
            }

            state = AutonomousBehaviorState.Turning;
            lastMessage = $"FSM turning to world yaw {yaw:0.#}.";
        }

        private void StartActionStagePlan(FSMActionEntry action)
        {
            if (action == null)
            {
                BeginWaiting("FSM action is missing.");
                return;
            }

            var json = action.ActionType == FSMActionType.Locomotion
                ? BuildLocomotionStagePlan(action)
                : BuildAnimationStagePlan(action);

            if (string.IsNullOrWhiteSpace(json))
            {
                BeginWaiting("FSM action has missing StagePlan data.");
                return;
            }

            if (!stagePlanPlayer.PlayJsonForOwner(json, FsmOwnerId))
            {
                BeginWaiting("FSM StagePlan failed to start.");
                return;
            }

            state = AutonomousBehaviorState.PlayingAction;
            lastMessage = $"FSM action started: {currentActionName}.";
        }

        private void UpdatePlayingAction()
        {
            if (stagePlanPlayer.IsOwnerPlaying(FsmOwnerId))
                return;

            if (!stagePlanPlayer.IsPlaying)
            {
                BeginWaiting("FSM action finished.");
                return;
            }

            PauseForExternalStagePlan();
        }

        private void PauseForExternalStagePlan()
        {
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            pendingAction = null;
            currentActionName = string.Empty;
            waitRemaining = 0f;
            state = AutonomousBehaviorState.PausedByExternalStagePlan;
            lastMessage = "FSM paused for external StagePlan.";
        }

        private void BeginWaiting(string message)
        {
            StopOwnedStagePlan();
            pendingAction = null;
            currentActionName = string.Empty;
            waitRemaining = fsmProfile != null ? fsmProfile.GetRandomWaitDuration() : 0f;
            state = AutonomousBehaviorState.Waiting;
            lastMessage = message;
        }

        private void StopOwnedStagePlan()
        {
            if (stagePlanPlayer != null)
                stagePlanPlayer.StopStagePlanForOwner(FsmOwnerId);
        }

        private bool IsExternalStagePlanPlaying()
        {
            return stagePlanPlayer != null &&
                stagePlanPlayer.IsPlaying &&
                !stagePlanPlayer.IsOwnerPlaying(FsmOwnerId);
        }

        private static string BuildAnimationStagePlan(FSMActionEntry action)
        {
            var duration = action.GetDuration();
            if (string.IsNullOrWhiteSpace(action.AnimationName) || duration <= 0f)
                return string.Empty;

            return "{\"schemaVersion\":\"2.0\",\"type\":\"stagePlan\",\"stages\":[{\"actions\":[{\"type\":\"animation\",\"name\":\"" +
                JsonTextUtility.Escape(action.AnimationName) +
                "\"}]}]}";
        }

        private static string BuildLocomotionStagePlan(FSMActionEntry action)
        {
            var duration = action.GetDuration();
            if (string.IsNullOrWhiteSpace(action.LocomotionMode) || duration <= 0f)
                return string.Empty;

            return "{\"schemaVersion\":\"2.0\",\"type\":\"stagePlan\",\"stages\":[{\"actions\":[{\"type\":\"locomotion\",\"mode\":\"" +
                JsonTextUtility.Escape(action.LocomotionMode) +
                "\",\"duration\":" +
                FormatFloat(duration) +
                "}]}]}";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private bool ValidateReferences()
        {
            if (fsmProfile == null)
                return Fail("FSMProfile reference is missing.");
            if (stagePlanPlayer == null)
                return Fail("StagePlanPlayer reference is missing.");
            if (rootOrientationController == null)
                return Fail("RootOrientationController reference is missing.");

            initialized = true;
            return true;
        }

        private bool Fail(string message)
        {
            initialized = false;
            schedulerActive = false;
            state = AutonomousBehaviorState.Disabled;
            lastMessage = message;
            Debug.LogError($"[VirtualPartner] AutonomousBehaviorScheduler failed: {message}", this);
            return false;
        }
    }
}
