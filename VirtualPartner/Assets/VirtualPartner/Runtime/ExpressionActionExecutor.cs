using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ExpressionActionExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private MouthTextureController mouthTextureController;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool expressionActive;
        [SerializeField] private string currentExpression = "-";
        [SerializeField] private string currentMouthPose = "-";
        [SerializeField] private string lastMessage = "Not initialized.";

        private ExpressionProfile expressionProfile;
        private MouthPoseProfile mouthPoseProfile;
        private MouthPoseSet currentPoseSet;

        public bool Initialized => initialized;
        public bool ExpressionActive => expressionActive;
        public string CurrentExpression => currentExpression;
        public string CurrentMouthPose => currentMouthPose;
        public string LastMessage => lastMessage;

        public void Configure(CharacterProfile profile, MouthTextureController mouthController)
        {
            characterProfile = profile;
            mouthTextureController = mouthController;
            expressionProfile = characterProfile != null ? characterProfile.ExpressionProfile : null;
            mouthPoseProfile = characterProfile != null ? characterProfile.MouthPoseProfile : null;
            initialized = ValidateReferences();
        }

        public bool StartExpression(string expressionName, float transitionDuration, out string failureReason)
        {
            failureReason = string.Empty;

            if (!initialized && !ValidateReferences())
            {
                failureReason = lastMessage;
                return false;
            }

            if (!expressionProfile.TryFindEntry(expressionName, out var entry) || !entry.Enabled)
            {
                failureReason = $"Expression '{expressionName}' is not registered or disabled.";
                lastMessage = failureReason;
                return false;
            }

            var mouthPoseName = string.IsNullOrWhiteSpace(entry.MouthPoseName)
                ? entry.ExpressionName
                : entry.MouthPoseName;
            if (!mouthPoseProfile.TryFindPoseSet(mouthPoseName, out var poseSet))
            {
                failureReason = $"Expression '{expressionName}' mouth pose '{mouthPoseName}' is missing.";
                lastMessage = failureReason;
                return false;
            }

            expressionActive = true;
            currentExpression = entry.ExpressionName;
            currentMouthPose = poseSet.PoseName;
            currentPoseSet = poseSet;
            mouthTextureController.SetExpressionMouthIndex(poseSet.Closed);
            lastMessage = $"Expression '{currentExpression}' active.";
            return true;
        }

        public void ClearExpression()
        {
            expressionActive = false;
            currentExpression = "-";
            currentMouthPose = "-";
            currentPoseSet = null;

            if (mouthTextureController != null)
                mouthTextureController.ClearExpressionMouth();

            lastMessage = "Expression cleared.";
        }

        public bool TryGetCurrentSpeakingPoseSet(out MouthPoseSet poseSet, out string message)
        {
            poseSet = null;
            message = string.Empty;

            if (expressionActive && currentPoseSet != null && currentPoseSet.HasSpeakingMouth)
            {
                poseSet = currentPoseSet;
                return true;
            }

            if (mouthPoseProfile != null
                && mouthPoseProfile.TryGetNeutralPoseSet(out var neutralPose)
                && neutralPose.HasSpeakingMouth)
            {
                poseSet = neutralPose;
                if (expressionActive)
                    message = $"Expression '{currentExpression}' has no speaking mouth set. Falling back to neutral.";
                return true;
            }

            message = expressionActive
                ? $"Expression '{currentExpression}' and neutral have no speaking mouth set."
                : "Neutral has no speaking mouth set.";
            return false;
        }

        public int GetCurrentClosedMouthIndex()
        {
            if (currentPoseSet != null)
                return currentPoseSet.Closed;

            return -1;
        }

        private bool ValidateReferences()
        {
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.");
            expressionProfile = characterProfile.ExpressionProfile;
            mouthPoseProfile = characterProfile.MouthPoseProfile;
            if (expressionProfile == null)
                return Fail("ExpressionProfile reference is missing.");
            if (mouthPoseProfile == null)
                return Fail("MouthPoseProfile reference is missing.");
            if (mouthTextureController == null)
                return Fail("MouthTextureController reference is missing.");

            initialized = true;
            lastMessage = "Ready.";
            return true;
        }

        private bool Fail(string message)
        {
            initialized = false;
            lastMessage = message;
            return false;
        }
    }
}
