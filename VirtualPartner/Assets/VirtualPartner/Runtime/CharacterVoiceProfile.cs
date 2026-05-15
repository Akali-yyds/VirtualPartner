using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Character Voice Profile")]
    public sealed class CharacterVoiceProfile : ScriptableObject
    {
        [SerializeField] private string characterId = "toki";
        [SerializeField] private string defaultVoiceId = "toki_default";
        [SerializeField] private float defaultSpeed = 1f;
        [SerializeField] private string defaultEmotion = "neutral";
        [SerializeField] private string ttsProvider = "MockTTS";

        public string CharacterId => characterId;
        public string DefaultVoiceId => defaultVoiceId;
        public float DefaultSpeed => defaultSpeed > 0f ? defaultSpeed : 1f;
        public string DefaultEmotion => defaultEmotion;
        public string TtsProvider => string.IsNullOrWhiteSpace(ttsProvider) ? "MockTTS" : ttsProvider.Trim();
    }
}
