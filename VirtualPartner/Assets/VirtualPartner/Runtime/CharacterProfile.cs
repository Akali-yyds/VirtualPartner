using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Character Profile")]
    public sealed class CharacterProfile : ScriptableObject
    {
        [SerializeField] private string characterId = "toki";
        [SerializeField] private string displayName = "Toki";
        [SerializeField] private string momotalkStatus = "Available";
        [SerializeField] private Sprite avatarIcon;
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private PresetAnimationProfile presetAnimationProfile;
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private FSMProfile fsmProfile;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string MomotalkStatus => momotalkStatus;
        public Sprite AvatarIcon => avatarIcon;
        public BoneMapProfile BoneMapProfile => boneMapProfile;
        public PresetAnimationProfile PresetAnimationProfile => presetAnimationProfile;
        public LocomotionProfile LocomotionProfile => locomotionProfile;
        public FSMProfile FsmProfile => fsmProfile;

        public bool TryValidate(out string failureReason)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return Fail("CharacterProfile characterId is empty.", out failureReason);
            if (string.IsNullOrWhiteSpace(displayName))
                return Fail($"CharacterProfile '{characterId}' displayName is empty.", out failureReason);
            if (boneMapProfile == null)
                return Fail($"CharacterProfile '{characterId}' BoneMapProfile is missing.", out failureReason);
            if (presetAnimationProfile == null)
                return Fail($"CharacterProfile '{characterId}' PresetAnimationProfile is missing.", out failureReason);
            if (locomotionProfile == null)
                return Fail($"CharacterProfile '{characterId}' LocomotionProfile is missing.", out failureReason);
            if (fsmProfile == null)
                return Fail($"CharacterProfile '{characterId}' FSMProfile is missing.", out failureReason);

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
