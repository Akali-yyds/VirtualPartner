using UnityEngine;

namespace VirtualPartner.Runtime
{
    public sealed class CharacterRuntimeContext
    {
        public CharacterRuntimeContext(
            CharacterProfile profile,
            GameObject runtimeRoot,
            StagePlanPlayer stagePlanPlayer,
            ActionCoordinator actionCoordinator,
            AvatarPoseApplier avatarPoseApplier,
            RootOrientationController rootOrientationController,
            LocomotionActionExecutor locomotionActionExecutor,
            AutonomousBehaviorScheduler autonomousBehaviorScheduler,
            SpeechBubbleView speechBubbleView,
            MouthTextureController mouthTextureController,
            ExpressionActionExecutor expressionActionExecutor,
            SpeechMouthDriver speechMouthDriver)
        {
            Profile = profile;
            RuntimeRoot = runtimeRoot;
            StagePlanPlayer = stagePlanPlayer;
            ActionCoordinator = actionCoordinator;
            AvatarPoseApplier = avatarPoseApplier;
            RootOrientationController = rootOrientationController;
            LocomotionActionExecutor = locomotionActionExecutor;
            AutonomousBehaviorScheduler = autonomousBehaviorScheduler;
            SpeechBubbleView = speechBubbleView;
            MouthTextureController = mouthTextureController;
            ExpressionActionExecutor = expressionActionExecutor;
            SpeechMouthDriver = speechMouthDriver;
        }

        public string CharacterId => Profile != null ? Profile.CharacterId : string.Empty;
        public CharacterProfile Profile { get; }
        public GameObject RuntimeRoot { get; }
        public StagePlanPlayer StagePlanPlayer { get; }
        public ActionCoordinator ActionCoordinator { get; }
        public AvatarPoseApplier AvatarPoseApplier { get; }
        public RootOrientationController RootOrientationController { get; }
        public LocomotionActionExecutor LocomotionActionExecutor { get; }
        public AutonomousBehaviorScheduler AutonomousBehaviorScheduler { get; }
        public SpeechBubbleView SpeechBubbleView { get; }
        public MouthTextureController MouthTextureController { get; }
        public ExpressionActionExecutor ExpressionActionExecutor { get; }
        public SpeechMouthDriver SpeechMouthDriver { get; }
    }
}
