using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public sealed class TtsRequest
    {
        public TtsRequest(
            string characterId,
            string provider,
            string voiceId,
            string text,
            string emotion,
            float speed,
            float duration,
            string cacheKey,
            string cachePath)
        {
            CharacterId = characterId ?? string.Empty;
            Provider = provider ?? string.Empty;
            VoiceId = voiceId ?? string.Empty;
            Text = text ?? string.Empty;
            Emotion = emotion ?? string.Empty;
            Speed = speed;
            Duration = duration;
            CacheKey = cacheKey ?? string.Empty;
            CachePath = cachePath ?? string.Empty;
        }

        public string CharacterId { get; }
        public string Provider { get; }
        public string VoiceId { get; }
        public string Text { get; }
        public string Emotion { get; }
        public float Speed { get; }
        public float Duration { get; }
        public string CacheKey { get; }
        public string CachePath { get; }
    }

    public sealed class TtsResult
    {
        public TtsResult(TtsRequest request, bool cached, string error)
        {
            Request = request;
            Cached = cached;
            Error = error ?? string.Empty;
        }

        public TtsRequest Request { get; }
        public bool Cached { get; }
        public string Error { get; }
    }

    [DisallowMultipleComponent]
    public sealed class TtsManager : MonoBehaviour
    {
        private const string MockProviderId = "MockTTS";
        private const string UnavailableProviderId = "Unavailable";
        private const string MockVersion = "mock-v1";

        [Header("References")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private SpeechMouthDriver speechMouthDriver;
        [SerializeField] private AudioSource audioSource;

        [Header("Mock TTS")]
        [SerializeField] private bool mockTtsEnabled = true;
        [SerializeField] private bool forceMockFailure;
        [SerializeField] private bool use3DAudio;
        [SerializeField] private int mockSampleRate = 16000;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool active;
        [SerializeField] private string modeText = "Idle";
        [SerializeField] private string statusText = "Not initialized.";
        [SerializeField] private string currentTextSummary = "-";
        [SerializeField] private string currentCharacterId = "-";
        [SerializeField] private string currentProvider = "-";
        [SerializeField] private string currentVoiceId = "-";
        [SerializeField] private string currentEmotion = "-";
        [SerializeField] private float currentSpeed = 1f;
        [SerializeField] private float elapsed;
        [SerializeField] private float duration;
        [SerializeField] private string cacheKey = "-";
        [SerializeField] private string cachePath = "-";
        [SerializeField] private bool cached;
        [SerializeField] private string latestError;
        [SerializeField] private string lastMessage;

        private bool waitingForAudio;
        private bool terminalReady;
        private StageActionStatus terminalStatus = StageActionStatus.Completed;
        private string terminalMessage = string.Empty;
        private TtsRequest currentRequest;
        private TtsResult latestResult;

        public bool Initialized => initialized;
        public bool Active => active && !terminalReady;
        public bool HasTerminalResult => terminalReady;
        public StageActionStatus TerminalStatus => terminalStatus;
        public string TerminalMessage => terminalMessage;
        public string ModeText => modeText;
        public string StatusText => statusText;
        public string CurrentTextSummary => currentTextSummary;
        public string CurrentCharacterId => currentCharacterId;
        public string CurrentProvider => currentProvider;
        public string CurrentVoiceId => currentVoiceId;
        public string CurrentEmotion => currentEmotion;
        public float CurrentSpeed => currentSpeed;
        public float Elapsed => elapsed;
        public float Duration => duration;
        public string CacheKey => cacheKey;
        public string CachePath => cachePath;
        public bool Cached => cached;
        public string LatestError => latestError;
        public string LastMessage => lastMessage;
        public bool MockTtsEnabled => mockTtsEnabled;
        public bool ForceMockFailure => forceMockFailure;
        public bool Use3DAudio => use3DAudio;
        public bool AudioSourcePlaying => audioSource != null && audioSource.isPlaying;
        public string AudioSourceState => FormatAudioSourceState();
        public TtsRequest CurrentRequest => currentRequest;
        public TtsResult LatestResult => latestResult;

        public void Configure(CharacterProfile profile, SpeechMouthDriver mouthDriver, AudioSource source)
        {
            characterProfile = profile;
            speechMouthDriver = mouthDriver;
            audioSource = source;
            initialized = ValidateReferences();
            ApplyAudioSourceSettings();

            if (initialized)
            {
                modeText = "MockTTS";
                statusText = "Ready";
                lastMessage = "Ready.";
            }
        }

        public bool StartSpeech(StagePlanActionDto action, float defaultDuration, out string failureReason)
        {
            failureReason = string.Empty;
            if (!initialized && !ValidateReferences())
            {
                failureReason = lastMessage;
                return false;
            }

            ReleaseSpeech();

            var text = action != null ? action.text : string.Empty;
            var voiceProfile = characterProfile != null ? characterProfile.VoiceProfile : null;
            ResolveRequest(action, voiceProfile, text);
            duration = EstimateDuration(text, defaultDuration);
            elapsed = 0f;
            cached = false;
            terminalReady = false;
            terminalStatus = StageActionStatus.Completed;
            terminalMessage = string.Empty;
            currentTextSummary = Summarize(text);
            BuildCacheInfo(text);
            currentRequest = new TtsRequest(
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                text,
                currentEmotion,
                currentSpeed,
                duration,
                cacheKey,
                cachePath);
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            if (voiceProfile == null)
            {
                latestError = "CharacterVoiceProfile missing. Using text fallback.";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
                StartFallback(text, latestError);
                return true;
            }

            if (!mockTtsEnabled)
            {
                latestError = "MockTTS disabled. Using text fallback.";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
                StartFallback(text, latestError);
                return true;
            }

            if (!SameProvider(currentProvider, MockProviderId))
            {
                latestError = $"TTS provider '{currentProvider}' is unavailable in Stage 2.10. Using text fallback.";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
                StartFallback(text, latestError);
                return true;
            }

            if (forceMockFailure)
            {
                latestError = "MockTTS forced failure. Using text fallback.";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
                StartFallback(text, latestError);
                return true;
            }

            StartMock(text);
            return true;
        }

        public void ManualUpdate(float deltaTime)
        {
            ApplyAudioSourceSettings();
            if (!active || terminalReady)
                return;

            var frameDelta = Mathf.Max(0f, deltaTime);
            elapsed = Mathf.Min(duration, elapsed + frameDelta);

            if (speechMouthDriver != null)
                speechMouthDriver.ManualUpdate(frameDelta);

            if (waitingForAudio)
            {
                if ((audioSource != null && !audioSource.isPlaying && elapsed > 0.01f) || elapsed >= duration)
                    MarkTerminal(StageActionStatus.Completed, "MockTTS playback completed.");
                return;
            }

            if (elapsed >= duration)
                MarkTerminal(StageActionStatus.Completed, "TTS fallback completed.");
        }

        public void StopSpeech()
        {
            StopSpeech("TTS stopped.");
        }

        public void StopSpeech(string message)
        {
            StopAudio();
            if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();

            active = false;
            waitingForAudio = false;
            terminalReady = true;
            terminalStatus = StageActionStatus.Interrupted;
            terminalMessage = string.IsNullOrWhiteSpace(message) ? "TTS stopped." : message;
            statusText = "Stopped";
            lastMessage = terminalMessage;
            latestResult = new TtsResult(currentRequest, cached, terminalMessage);
        }

        public void ReleaseSpeech()
        {
            StopAudio();
            if (speechMouthDriver != null && speechMouthDriver.Active)
                speechMouthDriver.StopSpeech();

            active = false;
            waitingForAudio = false;
            terminalReady = false;
        }

        public void SetMockFailureMode(bool shouldFail)
        {
            forceMockFailure = shouldFail;
        }

        public void SetUse3DAudio(bool value)
        {
            use3DAudio = value;
            ApplyAudioSourceSettings();
        }

        private void ResolveRequest(StagePlanActionDto action, CharacterVoiceProfile voiceProfile, string text)
        {
            currentCharacterId = ResolveText(
                voiceProfile != null ? voiceProfile.CharacterId : null,
                characterProfile != null ? characterProfile.CharacterId : "unknown");
            currentProvider = voiceProfile != null ? voiceProfile.TtsProvider : UnavailableProviderId;
            currentVoiceId = ResolveText(action != null ? action.voiceId : null, voiceProfile != null ? voiceProfile.DefaultVoiceId : string.Empty);
            currentEmotion = ResolveText(action != null ? action.emotion : null, voiceProfile != null ? voiceProfile.DefaultEmotion : string.Empty);
            currentSpeed = action != null && action.speed > 0f
                ? action.speed
                : voiceProfile != null ? voiceProfile.DefaultSpeed : 1f;

            if (currentSpeed <= 0f)
                currentSpeed = 1f;

            if (string.IsNullOrWhiteSpace(currentVoiceId))
                currentVoiceId = "-";
            if (string.IsNullOrWhiteSpace(currentEmotion))
                currentEmotion = "-";
        }

        private void StartMock(string text)
        {
            modeText = "MockTTS";
            statusText = "Playing";
            latestError = string.Empty;
            active = true;
            waitingForAudio = true;
            lastMessage = "MockTTS playback started.";
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            StartMouth(text);
            var clip = CreateSilentClip(duration);
            audioSource.clip = clip;
            audioSource.Play();
        }

        private void StartFallback(string text, string reason)
        {
            modeText = "Fallback";
            statusText = "Fallback";
            active = true;
            waitingForAudio = false;
            lastMessage = reason;
            latestResult = new TtsResult(currentRequest, cached, latestError);
            StartMouth(text);
        }

        private void StartMouth(string text)
        {
            if (speechMouthDriver == null)
                return;

            if (!speechMouthDriver.StartSpeech(text, duration))
            {
                latestError = $"SpeechMouthDriver failed: {speechMouthDriver.LastMessage}";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
            }
        }

        private void MarkTerminal(StageActionStatus status, string message)
        {
            StopAudio();
            if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();

            active = false;
            waitingForAudio = false;
            terminalReady = true;
            terminalStatus = status;
            terminalMessage = message;
            statusText = status == StageActionStatus.Completed ? "Completed" : "Stopped";
            lastMessage = message;
            latestResult = new TtsResult(currentRequest, cached, latestError);
        }

        private float EstimateDuration(string text, float defaultDuration)
        {
            if (speechMouthDriver != null)
                return speechMouthDriver.EstimateDuration(text);

            return Mathf.Max(0.01f, defaultDuration);
        }

        private AudioClip CreateSilentClip(float clipDuration)
        {
            var sampleRate = Mathf.Max(8000, mockSampleRate);
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, clipDuration) * sampleRate));
            return AudioClip.Create($"MockTTS_{currentCharacterId}", sampleCount, 1, sampleRate, false);
        }

        private void StopAudio()
        {
            if (audioSource == null)
                return;

            if (audioSource.isPlaying)
                audioSource.Stop();
            audioSource.clip = null;
        }

        private void BuildCacheInfo(string text)
        {
            var speedText = currentSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            var rawKey = string.Join(
                "|",
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                text ?? string.Empty,
                currentEmotion,
                speedText,
                MockVersion);
            var hash = Hash(rawKey);

            cacheKey = string.Join(
                "/",
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                currentEmotion,
                speedText,
                MockVersion,
                hash);
            cachePath = Path.Combine(GetProjectUserDataRoot(), "TTSCache", SanitizePathSegment(currentCharacterId), $"{hash}.wav");
        }

        private string GetProjectUserDataRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return Path.Combine(Application.persistentDataPath, "VirtualPartner", "UserData");

            return Path.Combine(projectRoot.FullName, "UserData");
        }

        private void ApplyAudioSourceSettings()
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = use3DAudio ? 1f : 0f;
        }

        private bool ValidateReferences()
        {
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.");
            if (speechMouthDriver == null)
                return Fail("SpeechMouthDriver reference is missing.");
            if (audioSource == null)
                return Fail("AudioSource reference is missing.");

            initialized = true;
            return true;
        }

        private bool Fail(string message)
        {
            initialized = false;
            statusText = "Failed";
            lastMessage = message;
            Debug.LogError($"[VirtualPartner] TtsManager failed: {message}", this);
            return false;
        }

        private string FormatAudioSourceState()
        {
            if (audioSource == null)
                return "Missing";

            var clipName = audioSource.clip != null ? audioSource.clip.name : "-";
            return $"{(audioSource.isPlaying ? "Playing" : "Stopped")} spatial={(use3DAudio ? "3D" : "2D")} clip={clipName}";
        }

        private static string ResolveText(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred.Trim();
        }

        private static bool SameProvider(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string Summarize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "-";

            var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 60 ? normalized : normalized.Substring(0, 60) + "...";
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(16);
                for (var i = 0; i < 8 && i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));

                return builder.ToString();
            }
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
