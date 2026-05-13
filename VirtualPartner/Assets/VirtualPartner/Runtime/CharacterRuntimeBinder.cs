using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CharacterRuntimeBinder : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private GameObject runtimeRoot;

        [Header("Runtime Components")]
        [SerializeField] private TimelinePlayer timelinePlayer;
        [SerializeField] private StagePlanPlayer stagePlanPlayer;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private SpeechBubbleView speechBubbleView;

        [Header("Runtime Status")]
        [SerializeField] private bool registered;
        [SerializeField] private string registeredCharacterId;
        [SerializeField] private string lastMessage;

        public bool Registered => registered;
        public string RegisteredCharacterId => registeredCharacterId;
        public string LastMessage => lastMessage;

        public bool TryRegister(out string failureReason)
        {
            if (registered)
            {
                failureReason = string.Empty;
                return true;
            }

            if (!ValidateReferences(out failureReason))
            {
                lastMessage = failureReason;
                return false;
            }

            var context = new CharacterRuntimeContext(
                characterProfile,
                runtimeRoot,
                timelinePlayer,
                stagePlanPlayer,
                actionCoordinator,
                avatarPoseApplier,
                rootOrientationController,
                locomotionActionExecutor,
                autonomousBehaviorScheduler,
                speechBubbleView);

            if (!CharacterRegistry.TryRegister(context, out failureReason))
            {
                lastMessage = failureReason;
                return false;
            }

            registered = true;
            registeredCharacterId = characterProfile.CharacterId;
            lastMessage = $"Registered character '{registeredCharacterId}'.";
            Debug.Log($"[VirtualPartner] CharacterRuntimeBinder: {lastMessage}", this);
            return true;
        }

        public void Unregister()
        {
            if (!registered)
                return;

            CharacterRegistry.Unregister(registeredCharacterId);
            lastMessage = $"Unregistered character '{registeredCharacterId}'.";
            registered = false;
            registeredCharacterId = string.Empty;
        }

        private bool ValidateReferences(out string failureReason)
        {
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.", out failureReason);
            if (!characterProfile.TryValidate(out failureReason))
                return false;
            if (runtimeRoot == null)
                return Fail("Runtime root reference is missing.", out failureReason);
            if (timelinePlayer == null)
                return Fail("TimelinePlayer reference is missing.", out failureReason);
            if (stagePlanPlayer == null)
                return Fail("StagePlanPlayer reference is missing.", out failureReason);
            if (actionCoordinator == null)
                return Fail("ActionCoordinator reference is missing.", out failureReason);
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.", out failureReason);
            if (rootOrientationController == null)
                return Fail("RootOrientationController reference is missing.", out failureReason);
            if (locomotionActionExecutor == null)
                return Fail("LocomotionActionExecutor reference is missing.", out failureReason);
            if (autonomousBehaviorScheduler == null)
                return Fail("AutonomousBehaviorScheduler reference is missing.", out failureReason);
            if (speechBubbleView == null)
                return Fail("SpeechBubbleView reference is missing.", out failureReason);

            failureReason = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }
}
