using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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
        private const string RealProviderId = "GptSoVits";
        private const string UnavailableProviderId = "Unavailable";
        private const string MockVersion = "mock-v1";
        private const string RealProviderVersionFallback = "gpt-sovits-api-v2";

        [Header("References")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private SpeechMouthDriver speechMouthDriver;
        [SerializeField] private AudioSource audioSource;

        [Header("Real TTS")]
        [SerializeField] private string serviceUrl = "http://127.0.0.1:8765";
        [SerializeField] private int requestTimeoutSeconds = 180;
        [SerializeField] private int healthTimeoutSeconds = 10;

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
        [SerializeField] private string healthStatusText = "Not checked.";
        [SerializeField] private string currentSessionId = "-";
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
        [SerializeField] private string providerVersion = "-";
        [SerializeField] private string referenceAudioHash = "-";
        [SerializeField] private string promptTextHash = "-";
        [SerializeField] private string promptLang = "-";
        [SerializeField] private string textLang = "-";
        [SerializeField] private string latestError;
        [SerializeField] private string lastMessage;

        private bool requestInFlight;
        private bool waitingForAudio;
        private bool terminalReady;
        private int speechSessionId;
        private StageActionStatus terminalStatus = StageActionStatus.Completed;
        private string terminalMessage = string.Empty;
        private TtsRequest currentRequest;
        private TtsResult latestResult;
        private UnityWebRequest activeRequest;
        private Coroutine activeRoutine;
        private Coroutine healthRoutine;

        public bool Initialized => initialized;
        public bool Active => active && !terminalReady;
        public bool HasTerminalResult => terminalReady;
        public StageActionStatus TerminalStatus => terminalStatus;
        public string TerminalMessage => terminalMessage;
        public string ModeText => modeText;
        public string StatusText => statusText;
        public string HealthStatusText => healthStatusText;
        public string CurrentSessionId => currentSessionId;
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
        public string ProviderVersion => providerVersion;
        public string ReferenceAudioHash => referenceAudioHash;
        public string PromptTextHash => promptTextHash;
        public string PromptLang => promptLang;
        public string TextLang => textLang;
        public string LatestError => latestError;
        public string LastMessage => lastMessage;
        public bool MockTtsEnabled => mockTtsEnabled;
        public bool ForceMockFailure => forceMockFailure;
        public bool Use3DAudio => use3DAudio;
        public string ServiceUrl => NormalizeServiceUrl();
        public int RequestTimeoutSeconds => Mathf.Max(1, requestTimeoutSeconds);
        public int HealthTimeoutSeconds => Mathf.Max(1, healthTimeoutSeconds);
        public bool AudioSourcePlaying => audioSource != null && audioSource.isPlaying;
        public string AudioSourceState => FormatAudioSourceState();
        public TtsRequest CurrentRequest => currentRequest;
        public TtsResult LatestResult => latestResult;

        public event Action SpeechPlaybackStarted;

        public void Configure(CharacterProfile profile, SpeechMouthDriver mouthDriver, AudioSource source)
        {
            characterProfile = profile;
            speechMouthDriver = mouthDriver;
            audioSource = source;
            initialized = ValidateReferences();
            ApplyAudioSourceSettings();

            if (initialized)
            {
                modeText = "Ready";
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
            speechSessionId++;
            var sessionId = speechSessionId;
            currentSessionId = sessionId.ToString(CultureInfo.InvariantCulture);

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
            ResetRealSignature();
            BuildCacheInfo(text, MockVersion, "-", "-", "-", "-");
            currentRequest = CreateCurrentRequest(text);
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            if (voiceProfile == null)
            {
                latestError = "CharacterVoiceProfile missing. Using text fallback.";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
                StartFallback(text, latestError);
                return true;
            }

            if (SameProvider(currentProvider, MockProviderId))
            {
                if (!mockTtsEnabled)
                {
                    latestError = "MockTTS disabled. Using text fallback.";
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

            if (SameProvider(currentProvider, RealProviderId))
            {
                StartReal(text, sessionId);
                return true;
            }

            latestError = $"TTS provider '{currentProvider}' is unavailable. Using text fallback.";
            Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
            StartFallback(text, latestError);
            return true;
        }

        public void ManualUpdate(float deltaTime)
        {
            ApplyAudioSourceSettings();
            if (!active || terminalReady)
                return;

            var frameDelta = Mathf.Max(0f, deltaTime);
            if (requestInFlight)
            {
                elapsed += frameDelta;
                return;
            }

            elapsed = Mathf.Min(duration, elapsed + frameDelta);

            if (speechMouthDriver != null)
                speechMouthDriver.ManualUpdate(frameDelta);

            if (waitingForAudio)
            {
                if ((audioSource != null && !audioSource.isPlaying && elapsed > 0.05f) || elapsed >= duration)
                    MarkTerminal(StageActionStatus.Completed, "TTS audio playback completed.");
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
            InvalidateSessionAndAbort();
            StopAudio();
            if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();

            active = false;
            requestInFlight = false;
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
            InvalidateSessionAndAbort();
            StopAudio();
            if (speechMouthDriver != null && speechMouthDriver.Active)
                speechMouthDriver.StopSpeech();

            active = false;
            requestInFlight = false;
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

        public void RequestHealthCheck()
        {
            if (!initialized && !ValidateReferences())
                return;
            if (Active)
            {
                healthStatusText = "Skipped while TTS is active.";
                return;
            }

            if (healthRoutine != null)
                StopCoroutine(healthRoutine);
            healthRoutine = StartCoroutine(HealthCheckRoutine());
        }

        private IEnumerator HealthCheckRoutine()
        {
            healthStatusText = "Checking...";
            using (var request = UnityWebRequest.Get(BuildUrl("/health")))
            {
                request.timeout = HealthTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    healthStatusText = $"Failed: HTTP {request.responseCode} {request.error}";
                    healthRoutine = null;
                    yield break;
                }

                var response = ParseHealthResponse(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                healthStatusText = FormatHealthSummary(response);
                healthRoutine = null;
            }
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
            currentSpeed = Mathf.Clamp(currentSpeed <= 0f ? 1f : currentSpeed, 0.5f, 2f);

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
            requestInFlight = false;
            waitingForAudio = true;
            elapsed = 0f;
            lastMessage = "MockTTS playback started.";
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            StartTextMouth(text);
            var clip = CreateSilentClip(duration);
            audioSource.clip = clip;
            audioSource.Play();
            NotifySpeechPlaybackStarted();
        }

        private void StartReal(string text, int sessionId)
        {
            modeText = "RealTTS";
            statusText = "Requesting";
            latestError = string.Empty;
            active = true;
            requestInFlight = true;
            waitingForAudio = false;
            elapsed = 0f;
            lastMessage = "Real TTS request started.";
            activeRoutine = StartCoroutine(RealSpeechRoutine(text, sessionId));
        }

        private IEnumerator RealSpeechRoutine(string text, int sessionId)
        {
            var healthRequest = UnityWebRequest.Get(BuildUrl("/health"));
            using (healthRequest)
            {
                activeRequest = healthRequest;
                healthRequest.timeout = HealthTimeoutSeconds;
                yield return healthRequest.SendWebRequest();

                if (!IsSessionCurrent(sessionId))
                    yield break;
                activeRequest = null;

                if (healthRequest.result != UnityWebRequest.Result.Success)
                {
                    var reason = $"TTS health failed: HTTP {healthRequest.responseCode} {healthRequest.error}";
                    healthStatusText = reason;
                    StartFallbackWithWarning(text, reason);
                    yield break;
                }

                var health = ParseHealthResponse(healthRequest.downloadHandler != null ? healthRequest.downloadHandler.text : string.Empty);
                healthStatusText = FormatHealthSummary(health);
                if (!TryResolveVoiceHealth(health, currentVoiceId, out var voiceHealth, out var healthFailure))
                {
                    StartFallbackWithWarning(text, healthFailure);
                    yield break;
                }

                providerVersion = ResolveText(
                    health != null && health.wrapper != null ? health.wrapper.version : null,
                    RealProviderVersionFallback);
                referenceAudioHash = ResolveText(voiceHealth.refAudioHash, "-");
                promptTextHash = ResolveText(voiceHealth.promptTextHash, "-");
                promptLang = ResolveText(voiceHealth.promptLang, "ja");
                textLang = ResolveText(voiceHealth.textLang, "zh");
                BuildCacheInfo(text, providerVersion, referenceAudioHash, promptTextHash, promptLang, textLang);
                currentRequest = CreateCurrentRequest(text);

                if (File.Exists(cachePath))
                {
                    cached = true;
                    latestResult = new TtsResult(currentRequest, cached, string.Empty);
                    yield return LoadAndPlayClip(cachePath, text, sessionId, "TTS cache hit.");
                    yield break;
                }

                if (health.upstream == null || !health.upstream.ok)
                {
                    var reason = $"TTS upstream unavailable: {(health.upstream != null ? health.upstream.message : "missing upstream status")}";
                    StartFallbackWithWarning(text, reason);
                    yield break;
                }
            }

            if (!IsSessionCurrent(sessionId))
                yield break;

            var requestJson = BuildRealTtsRequestJson(text);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            using (var request = new UnityWebRequest(BuildUrl("/tts"), "POST"))
            {
                activeRequest = request;
                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (!IsSessionCurrent(sessionId))
                    yield break;
                activeRequest = null;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    StartFallbackWithWarning(text, FormatHttpFailure(request));
                    yield break;
                }

                var audioBytes = request.downloadHandler != null ? request.downloadHandler.data : null;
                if (audioBytes == null || audioBytes.Length == 0)
                {
                    StartFallbackWithWarning(text, "TTS returned empty audio.");
                    yield break;
                }

                if (!TryWriteCacheFile(audioBytes, cachePath, out var writeFailure))
                {
                    StartFallbackWithWarning(text, writeFailure);
                    yield break;
                }

                cached = false;
                latestResult = new TtsResult(currentRequest, cached, string.Empty);
                yield return LoadAndPlayClip(cachePath, text, sessionId, "Real TTS playback started.");
            }
        }

        private IEnumerator LoadAndPlayClip(string path, string text, int sessionId, string message)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.WAV))
            {
                activeRequest = request;
                request.timeout = HealthTimeoutSeconds;
                yield return request.SendWebRequest();

                if (!IsSessionCurrent(sessionId))
                    yield break;
                activeRequest = null;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    StartFallbackWithWarning(text, $"TTS audio load failed: {request.error}");
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.length <= 0f)
                {
                    StartFallbackWithWarning(text, "TTS audio clip is empty.");
                    yield break;
                }

                StartAudioPlayback(clip, message);
            }
        }

        private void StartAudioPlayback(AudioClip clip, string message)
        {
            modeText = "RealTTS";
            statusText = cached ? "Playing Cached" : "Playing";
            active = true;
            requestInFlight = false;
            waitingForAudio = true;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, clip.length);
            currentRequest = CreateCurrentRequest(currentRequest != null ? currentRequest.Text : string.Empty);
            latestError = string.Empty;
            lastMessage = message;
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            audioSource.clip = clip;
            StartAudioMouth();
            audioSource.Play();
            NotifySpeechPlaybackStarted();
        }

        private void StartFallback(string text, string reason)
        {
            modeText = "Fallback";
            statusText = "Fallback";
            active = true;
            requestInFlight = false;
            waitingForAudio = false;
            elapsed = 0f;
            duration = EstimateDuration(text, duration);
            lastMessage = reason;
            latestResult = new TtsResult(currentRequest, cached, latestError);
            StartTextMouth(text);
            NotifySpeechPlaybackStarted();
        }

        private void StartFallbackWithWarning(string text, string reason)
        {
            latestError = string.IsNullOrWhiteSpace(reason) ? "TTS failed. Using text fallback." : reason;
            Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
            StartFallback(text, latestError);
        }

        private void StartTextMouth(string text)
        {
            if (speechMouthDriver == null)
                return;

            if (!speechMouthDriver.StartSpeech(text, duration))
            {
                latestError = $"SpeechMouthDriver failed: {speechMouthDriver.LastMessage}";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
            }
        }

        private void StartAudioMouth()
        {
            if (speechMouthDriver == null)
                return;

            if (!speechMouthDriver.StartSpeechFromAudio(audioSource, duration))
            {
                latestError = $"SpeechMouthDriver audio failed: {speechMouthDriver.LastMessage}";
                Debug.LogWarning($"[VirtualPartner] TTS warning: {latestError}", this);
            }
        }

        private void MarkTerminal(StageActionStatus status, string message)
        {
            StopAudio();
            if (speechMouthDriver != null)
                speechMouthDriver.StopSpeech();

            active = false;
            requestInFlight = false;
            waitingForAudio = false;
            terminalReady = true;
            terminalStatus = status;
            terminalMessage = message;
            statusText = status == StageActionStatus.Completed ? "Completed" : "Stopped";
            lastMessage = message;
            latestResult = new TtsResult(currentRequest, cached, latestError);
        }

        private void NotifySpeechPlaybackStarted()
        {
            SpeechPlaybackStarted?.Invoke();
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

        private void BuildCacheInfo(
            string text,
            string engineVersion,
            string refHash,
            string promptHash,
            string resolvedPromptLang,
            string resolvedTextLang)
        {
            var speedText = currentSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            var versionText = string.IsNullOrWhiteSpace(engineVersion) ? MockVersion : engineVersion.Trim();
            var rawKey = string.Join(
                "|",
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                text ?? string.Empty,
                currentEmotion,
                speedText,
                refHash ?? string.Empty,
                promptHash ?? string.Empty,
                resolvedPromptLang ?? string.Empty,
                resolvedTextLang ?? string.Empty,
                versionText);
            var hash = Hash(rawKey);

            cacheKey = string.Join(
                "/",
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                currentEmotion,
                speedText,
                versionText,
                hash);
            cachePath = Path.Combine(GetProjectUserDataRoot(), "TTSCache", SanitizePathSegment(currentCharacterId), $"{hash}.wav");
        }

        private TtsRequest CreateCurrentRequest(string text)
        {
            return new TtsRequest(
                currentCharacterId,
                currentProvider,
                currentVoiceId,
                text,
                currentEmotion,
                currentSpeed,
                duration,
                cacheKey,
                cachePath);
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

        private void InvalidateSessionAndAbort()
        {
            speechSessionId++;
            currentSessionId = "-";

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            AbortActiveRequest();
        }

        private void AbortActiveRequest()
        {
            if (activeRequest == null)
                return;

            activeRequest.Abort();
            activeRequest = null;
        }

        private bool IsSessionCurrent(int sessionId)
        {
            return sessionId == speechSessionId && active && !terminalReady;
        }

        private bool TryWriteCacheFile(byte[] audioBytes, string targetPath, out string failureReason)
        {
            failureReason = string.Empty;
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = targetPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                File.WriteAllBytes(tempPath, audioBytes);

                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);

                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"TTS cache write failed: {exception.Message}";
                return false;
            }
        }

        private bool TryResolveVoiceHealth(TtsHealthResponse health, string voiceId, out TtsHealthVoice voice, out string failureReason)
        {
            voice = null;
            failureReason = string.Empty;

            if (health == null)
            {
                failureReason = "TTS health response is empty.";
                return false;
            }

            if (health.voices == null || health.voices.Length == 0)
            {
                failureReason = "TTS health has no voice entries.";
                return false;
            }

            for (var i = 0; i < health.voices.Length; i++)
            {
                var candidate = health.voices[i];
                if (candidate == null || !SameProvider(candidate.voiceId, voiceId))
                    continue;

                voice = candidate;
                break;
            }

            if (voice == null)
            {
                failureReason = $"TTS voice '{voiceId}' is not configured.";
                return false;
            }

            if (!voice.ok)
            {
                failureReason = $"TTS voice '{voiceId}' is unavailable: {voice.message}";
                return false;
            }

            return true;
        }

        private TtsHealthResponse ParseHealthResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<TtsHealthResponse>(json);
            }
            catch (Exception exception)
            {
                healthStatusText = $"Parse failed: {exception.Message}";
                return null;
            }
        }

        private string FormatHealthSummary(TtsHealthResponse health)
        {
            if (health == null)
                return "Health parse failed.";

            var wrapper = health.wrapper != null && health.wrapper.ok ? "wrapper ok" : "wrapper failed";
            var upstream = health.upstream != null && health.upstream.ok ? "upstream ok" : "upstream failed";
            var voice = "voice missing";
            if (health.voices != null && health.voices.Length > 0)
            {
                var targetVoiceId = currentVoiceId == "-" && characterProfile != null && characterProfile.VoiceProfile != null
                    ? characterProfile.VoiceProfile.DefaultVoiceId
                    : currentVoiceId;
                for (var i = 0; i < health.voices.Length; i++)
                {
                    if (SameProvider(health.voices[i].voiceId, targetVoiceId))
                    {
                        voice = health.voices[i].ok ? "voice ok" : "voice failed";
                        break;
                    }
                }
            }

            return $"{wrapper}; {upstream}; {voice}; {health.message}";
        }

        private string BuildRealTtsRequestJson(string text)
        {
            var builder = new StringBuilder(256 + (text != null ? text.Length : 0));
            builder.Append('{');
            AppendJson(builder, "characterId", currentCharacterId, false);
            AppendJson(builder, "voiceId", currentVoiceId, true);
            AppendJson(builder, "text", text, true);
            AppendJson(builder, "emotion", currentEmotion, true);
            builder.Append(",\"speed\":").Append(currentSpeed.ToString("0.###", CultureInfo.InvariantCulture));
            AppendJson(builder, "format", "wav", true);
            builder.Append('}');
            return builder.ToString();
        }

        private string FormatHttpFailure(UnityWebRequest request)
        {
            if (request == null)
                return "TTS request failed.";

            var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(body))
                return $"TTS request failed: HTTP {request.responseCode} {body}";

            return $"TTS request failed: HTTP {request.responseCode} {request.error}";
        }

        private string NormalizeServiceUrl()
        {
            return string.IsNullOrWhiteSpace(serviceUrl)
                ? "http://127.0.0.1:8765"
                : serviceUrl.Trim().TrimEnd('/');
        }

        private string BuildUrl(string path)
        {
            return NormalizeServiceUrl() + path;
        }

        private void ResetRealSignature()
        {
            providerVersion = "-";
            referenceAudioHash = "-";
            promptTextHash = "-";
            promptLang = "-";
            textLang = "-";
        }

        private string FormatAudioSourceState()
        {
            if (audioSource == null)
                return "Missing";

            var clipName = audioSource.clip != null ? audioSource.clip.name : "-";
            return $"{(audioSource.isPlaying ? "Playing" : "Stopped")} spatial={(use3DAudio ? "3D" : "2D")} clip={clipName}";
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool prependComma)
        {
            if (prependComma)
                builder.Append(',');

            builder.Append('"')
                .Append(EscapeJson(key))
                .Append("\":\"")
                .Append(EscapeJson(value))
                .Append('"');
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

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 16);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        [Serializable]
        private sealed class TtsHealthResponse
        {
            public bool success;
            public string message;
            public TtsHealthWrapper wrapper;
            public TtsHealthUpstream upstream;
            public TtsHealthVoice[] voices;
        }

        [Serializable]
        private sealed class TtsHealthWrapper
        {
            public bool ok;
            public string version;
            public string message;
        }

        [Serializable]
        private sealed class TtsHealthUpstream
        {
            public bool ok;
            public string url;
            public string message;
        }

        [Serializable]
        private sealed class TtsHealthVoice
        {
            public string voiceId;
            public bool ok;
            public string message;
            public string refAudioPath;
            public string refAudioHash;
            public string promptTextPath;
            public string promptTextHash;
            public string promptLang;
            public string textLang;
        }
    }
}
