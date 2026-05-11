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
        PausedByExternalTimeline,
        UserInteraction
    }

    [DisallowMultipleComponent]
    public sealed class AutonomousBehaviorScheduler : MonoBehaviour
    {
        public const string FsmOwnerId = "FSM";

        [Header("References")]
        [SerializeField] private FSMProfile fsmProfile;
        [SerializeField] private TimelinePlayer timelinePlayer;
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
        private bool currentActionIsLocomotion;
        private bool locomotionObservedActive;

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
            TimelinePlayer player,
            RootOrientationController orientationController)
        {
            fsmProfile = profile;
            timelinePlayer = player;
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
            StopOwnedTimeline();
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            schedulerActive = false;
            state = AutonomousBehaviorState.Disabled;
            waitRemaining = 0f;
            currentActionName = string.Empty;
            pendingAction = null;
            currentActionIsLocomotion = false;
            locomotionObservedActive = false;
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

            StopOwnedTimeline();
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            inUserInteraction = true;
            state = AutonomousBehaviorState.UserInteraction;
            waitRemaining = 0f;
            currentActionName = string.Empty;
            pendingAction = null;
            currentActionIsLocomotion = false;
            locomotionObservedActive = false;

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

            if (IsExternalTimelinePlaying())
            {
                PauseForExternalTimeline();
                return;
            }

            if (state == AutonomousBehaviorState.PausedByExternalTimeline)
                BeginWaiting("External timeline finished.");

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

                StartActionTimeline(pendingAction);
                return;
            }

            if (state == AutonomousBehaviorState.PlayingAction)
                UpdatePlayingAction();
        }

        private void StartNextAction()
        {
            if (timelinePlayer.IsPlaying)
            {
                PauseForExternalTimeline();
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
                StartActionTimeline(pendingAction);
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

        private void StartActionTimeline(FSMActionEntry action)
        {
            if (action == null)
            {
                BeginWaiting("FSM action is missing.");
                return;
            }

            var json = action.ActionType == FSMActionType.Locomotion
                ? BuildLocomotionTimeline(action)
                : BuildAnimationTimeline(action);

            if (string.IsNullOrWhiteSpace(json))
            {
                BeginWaiting("FSM action has missing timeline data.");
                return;
            }

            if (!timelinePlayer.PlayJsonForOwner(json, FsmOwnerId))
            {
                BeginWaiting("FSM timeline failed to start.");
                return;
            }

            state = AutonomousBehaviorState.PlayingAction;
            currentActionIsLocomotion = action.ActionType == FSMActionType.Locomotion;
            locomotionObservedActive = false;
            lastMessage = $"FSM action started: {currentActionName}.";
        }

        private void UpdatePlayingAction()
        {
            if (timelinePlayer.IsOwnerPlaying(FsmOwnerId))
            {
                if (!currentActionIsLocomotion)
                    return;

                if (timelinePlayer.LocomotionActive)
                {
                    locomotionObservedActive = true;
                    return;
                }

                if (locomotionObservedActive || timelinePlayer.CurrentTime > 0.05f)
                {
                    StopOwnedTimeline();
                    BeginWaiting("FSM locomotion ended or stopped early.");
                }

                return;
            }

            if (!timelinePlayer.IsPlaying)
            {
                BeginWaiting("FSM action finished.");
                return;
            }

            PauseForExternalTimeline();
        }

        private void PauseForExternalTimeline()
        {
            if (state == AutonomousBehaviorState.Turning && rootOrientationController != null)
                rootOrientationController.StopCurrentTurn();

            pendingAction = null;
            currentActionName = string.Empty;
            currentActionIsLocomotion = false;
            locomotionObservedActive = false;
            waitRemaining = 0f;
            state = AutonomousBehaviorState.PausedByExternalTimeline;
            lastMessage = "FSM paused for external timeline.";
        }

        private void BeginWaiting(string message)
        {
            StopOwnedTimeline();
            pendingAction = null;
            currentActionName = string.Empty;
            currentActionIsLocomotion = false;
            locomotionObservedActive = false;
            waitRemaining = fsmProfile != null ? fsmProfile.GetRandomWaitDuration() : 0f;
            state = AutonomousBehaviorState.Waiting;
            lastMessage = message;
        }

        private void StopOwnedTimeline()
        {
            if (timelinePlayer != null)
                timelinePlayer.StopTimelineForOwner(FsmOwnerId);
        }

        private bool IsExternalTimelinePlaying()
        {
            return timelinePlayer != null &&
                timelinePlayer.IsPlaying &&
                !timelinePlayer.IsOwnerPlaying(FsmOwnerId);
        }

        private static string BuildAnimationTimeline(FSMActionEntry action)
        {
            var duration = action.GetDuration();
            if (string.IsNullOrWhiteSpace(action.AnimationName) || duration <= 0f)
                return string.Empty;

            return "{\"schemaVersion\":\"1.0\",\"timeline\":[{\"start\":0.0,\"end\":" +
                FormatFloat(duration) +
                ",\"actions\":[{\"type\":\"animation\",\"name\":\"" +
                EscapeJson(action.AnimationName) +
                "\"}]}]}";
        }

        private static string BuildLocomotionTimeline(FSMActionEntry action)
        {
            var duration = action.GetDuration();
            if (string.IsNullOrWhiteSpace(action.LocomotionMode) || duration <= 0f)
                return string.Empty;

            return "{\"schemaVersion\":\"1.0\",\"timeline\":[{\"start\":0.0,\"end\":" +
                FormatFloat(duration) +
                ",\"actions\":[{\"type\":\"locomotion\",\"mode\":\"" +
                EscapeJson(action.LocomotionMode) +
                "\"}]}]}";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private bool ValidateReferences()
        {
            if (fsmProfile == null)
                return Fail("FSMProfile reference is missing.");
            if (timelinePlayer == null)
                return Fail("TimelinePlayer reference is missing.");
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
