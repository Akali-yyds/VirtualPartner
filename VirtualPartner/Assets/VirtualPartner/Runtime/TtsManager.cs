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
        [SerializeField] private bool streamRealTts = true;
        [SerializeField] private int streamMode = 1;
        [SerializeField] private int streamMinNonWhitespaceCharacters = 28;
        [SerializeField] private float streamPrebufferSeconds = 2f;
        [SerializeField] private float streamResumeBufferSeconds = 1f;
        [SerializeField] private float streamClipMaxSeconds = 180f;
        [SerializeField] private float audioCompletionTailGraceSeconds = 0.75f;
        [SerializeField] private bool cacheCompletedStream = true;
        [SerializeField] private bool fallbackToBufferedOnStreamFailure = true;

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
        [SerializeField] private bool streamingPlayback;
        [SerializeField] private int streamingSampleRate = 32000;
        [SerializeField] private int streamingModeInUse;
        [SerializeField] private float streamingBufferedSeconds;
        [SerializeField] private float streamingWrittenSeconds;
        [SerializeField] private long streamingReceivedBytes;
        [SerializeField] private bool streamingPausedForBuffer;
        [SerializeField] private int streamingUnderrunCount;
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
        private StreamingPcmBuffer activeStreamBuffer;
        private bool lastStreamingAttemptStartedPlayback;
        private string lastStreamingFailure = string.Empty;
        private float streamingTailGraceRemaining = -1f;

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
        public bool StreamingPlayback => streamingPlayback;
        public int StreamingSampleRate => streamingSampleRate;
        public int StreamingModeInUse => streamingModeInUse;
        public float StreamingBufferedSeconds => streamingBufferedSeconds;
        public float StreamingWrittenSeconds => streamingWrittenSeconds;
        public long StreamingReceivedBytes => streamingReceivedBytes;
        public bool StreamingPausedForBuffer => streamingPausedForBuffer;
        public int StreamingUnderrunCount => streamingUnderrunCount;
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
            RefreshStreamingStatus();
            if (streamingPlayback)
            {
                UpdateStreamingPlayback(frameDelta);
                return;
            }

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
                if (audioSource != null)
                {
                    if (!audioSource.isPlaying && elapsed > 0.05f)
                        MarkTerminal(StageActionStatus.Completed, "TTS audio playback completed.");

                    return;
                }

                if (elapsed >= duration + Mathf.Max(0.1f, audioCompletionTailGraceSeconds))
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
            TtsHealthVoice selectedVoiceHealth = null;
            var useStreaming = false;
            var resolvedStreamMode = 0;
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
                selectedVoiceHealth = voiceHealth;
                useStreaming = ShouldUseStreaming(voiceHealth, text);
                resolvedStreamMode = ResolveStreamingMode(voiceHealth);
                var cacheVersion = useStreaming
                    ? GetStreamingProviderVersion(providerVersion, resolvedStreamMode)
                    : providerVersion;
                BuildCacheInfo(text, cacheVersion, referenceAudioHash, promptTextHash, promptLang, textLang);
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

            if (useStreaming)
            {
                yield return StreamAndPlaySpeech(text, sessionId, selectedVoiceHealth);
                if (!IsSessionCurrent(sessionId))
                    yield break;

                if (lastStreamingAttemptStartedPlayback || waitingForAudio || terminalReady)
                    yield break;

                if (!fallbackToBufferedOnStreamFailure)
                {
                    StartFallbackWithWarning(text, lastStreamingFailure);
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(lastStreamingFailure))
                    Debug.LogWarning($"[VirtualPartner] TTS streaming failed, falling back to buffered wav: {lastStreamingFailure}", this);

                BuildCacheInfo(text, providerVersion, referenceAudioHash, promptTextHash, promptLang, textLang);
                currentRequest = CreateCurrentRequest(text);
            }

            yield return RequestBufferedSpeech(text, sessionId);
        }

        private IEnumerator RequestBufferedSpeech(string text, int sessionId)
        {
            var requestJson = BuildRealTtsRequestJson(text, false, 0);
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

        private IEnumerator StreamAndPlaySpeech(string text, int sessionId, TtsHealthVoice voiceHealth)
        {
            lastStreamingAttemptStartedPlayback = false;
            lastStreamingFailure = string.Empty;
            streamingSampleRate = ResolveStreamingSampleRate(voiceHealth);
            streamingModeInUse = ResolveStreamingMode(voiceHealth);
            streamingReceivedBytes = 0;
            streamingBufferedSeconds = 0f;
            streamingWrittenSeconds = 0f;
            streamingPausedForBuffer = false;
            streamingUnderrunCount = 0;
            streamingTailGraceRemaining = -1f;

            var sampleRate = Mathf.Max(8000, streamingSampleRate);
            var buffer = new StreamingPcmBuffer(sampleRate, 1, Mathf.CeilToInt(sampleRate * Mathf.Max(0.25f, streamPrebufferSeconds + 0.5f)));
            var requestJson = BuildRealTtsRequestJson(text, true, streamingModeInUse);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            using (var request = new UnityWebRequest(BuildUrl("/tts"), "POST"))
            {
                var handler = new StreamingPcmDownloadHandler(buffer);
                activeStreamBuffer = buffer;
                activeRequest = request;
                requestInFlight = true;
                waitingForAudio = false;
                statusText = "Streaming";
                lastMessage = "Real TTS streaming request started.";

                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = handler;
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                var prebufferSamples = Mathf.CeilToInt(sampleRate * Mathf.Clamp(streamPrebufferSeconds, 0.05f, 5f));
                while (!operation.isDone)
                {
                    if (!IsSessionCurrent(sessionId))
                        yield break;

                    RefreshStreamingStatus();
                    if (!lastStreamingAttemptStartedPlayback && buffer.TotalSamplesWritten >= prebufferSamples)
                        StartStreamingPlayback(buffer, text, "Real TTS streaming playback started.");

                    yield return null;
                }

                if (!IsSessionCurrent(sessionId))
                    yield break;

                activeRequest = null;
                buffer.MarkCompleted();
                RefreshStreamingStatus();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastStreamingFailure = FormatStreamFailure(request, buffer);
                    if (lastStreamingAttemptStartedPlayback)
                    {
                        latestError = lastStreamingFailure;
                        Debug.LogWarning($"[VirtualPartner] TTS streaming warning: {latestError}", this);
                        requestInFlight = false;
                        waitingForAudio = true;
                    }
                    else
                    {
                        requestInFlight = false;
                        waitingForAudio = false;
                        streamingPlayback = false;
                        activeStreamBuffer = null;
                    }

                    yield break;
                }

                if (!lastStreamingAttemptStartedPlayback)
                {
                    if (buffer.TotalSamplesWritten <= 0)
                    {
                        lastStreamingFailure = "TTS stream returned empty audio.";
                        requestInFlight = false;
                        waitingForAudio = false;
                        streamingPlayback = false;
                        activeStreamBuffer = null;
                        yield break;
                    }

                    StartStreamingPlayback(buffer, text, "Real TTS streaming playback started.");
                }

                if (cacheCompletedStream && buffer.RawByteCount > 0)
                {
                    if (!TryWritePcm16WavCacheFile(buffer.GetRawBytes(), sampleRate, 1, cachePath, out var writeFailure))
                        Debug.LogWarning($"[VirtualPartner] TTS stream cache warning: {writeFailure}", this);
                }

                requestInFlight = false;
                waitingForAudio = true;
                latestError = string.Empty;
                latestResult = new TtsResult(currentRequest, cached, string.Empty);
                lastMessage = "Real TTS stream received.";
                RefreshStreamingStatus();
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

        private void StartStreamingPlayback(StreamingPcmBuffer buffer, string text, string message)
        {
            if (buffer == null || audioSource == null)
                return;

            if (lastStreamingAttemptStartedPlayback)
                return;

            modeText = "RealTTS Stream";
            statusText = "Playing Stream";
            active = true;
            requestInFlight = false;
            waitingForAudio = true;
            streamingPlayback = true;
            streamingPausedForBuffer = false;
            streamingTailGraceRemaining = -1f;
            elapsed = 0f;
            duration = Mathf.Max(1f, streamClipMaxSeconds);
            currentRequest = CreateCurrentRequest(text);
            latestError = string.Empty;
            lastMessage = message;
            latestResult = new TtsResult(currentRequest, cached, string.Empty);

            var sampleRate = Mathf.Max(8000, buffer.SampleRate);
            var sampleCount = Mathf.Max(sampleRate, Mathf.CeilToInt(sampleRate * Mathf.Max(1f, streamClipMaxSeconds)));
            var clip = AudioClip.Create(
                $"TTSStream_{currentCharacterId}",
                sampleCount,
                buffer.Channels,
                sampleRate,
                true,
                buffer.Read);

            activeStreamBuffer = buffer;
            audioSource.clip = clip;
            StartAudioMouth();
            audioSource.Play();
            lastStreamingAttemptStartedPlayback = true;
            RefreshStreamingStatus();
            NotifySpeechPlaybackStarted();
        }

        private void RefreshStreamingStatus()
        {
            if (activeStreamBuffer == null)
            {
                streamingBufferedSeconds = 0f;
                streamingWrittenSeconds = 0f;
                streamingReceivedBytes = 0;
                return;
            }

            streamingBufferedSeconds = activeStreamBuffer.BufferedSeconds;
            streamingWrittenSeconds = activeStreamBuffer.WrittenSeconds;
            streamingReceivedBytes = activeStreamBuffer.ReceivedBytes;
        }

        private void UpdateStreamingPlayback(float frameDelta)
        {
            elapsed += frameDelta;
            RefreshStreamingStatus();

            if (speechMouthDriver != null)
                speechMouthDriver.ManualUpdate(frameDelta);

            if (activeStreamBuffer != null && !activeStreamBuffer.Completed)
            {
                var pauseThreshold = 0.08f;
                var resumeThreshold = Mathf.Max(pauseThreshold + 0.05f, streamResumeBufferSeconds);
                if (!streamingPausedForBuffer
                    && audioSource != null
                    && audioSource.isPlaying
                    && activeStreamBuffer.BufferedSeconds <= pauseThreshold)
                {
                    audioSource.Pause();
                    streamingPausedForBuffer = true;
                    streamingUnderrunCount++;
                    statusText = "Buffering Stream";
                    lastMessage = "TTS stream buffering.";
                    return;
                }

                if (streamingPausedForBuffer
                    && activeStreamBuffer.BufferedSeconds >= resumeThreshold
                    && audioSource != null)
                {
                    audioSource.UnPause();
                    streamingPausedForBuffer = false;
                    statusText = "Playing Stream";
                    lastMessage = "TTS stream resumed.";
                }
            }

            if (activeStreamBuffer != null && activeStreamBuffer.IsDrained)
            {
                if (streamingTailGraceRemaining < 0f)
                {
                    streamingTailGraceRemaining = Mathf.Max(0.05f, audioCompletionTailGraceSeconds);
                    statusText = "Finishing Stream";
                    lastMessage = "TTS stream tail draining.";
                    return;
                }

                streamingTailGraceRemaining -= frameDelta;
                if (streamingTailGraceRemaining > 0f)
                    return;

                MarkTerminal(StageActionStatus.Completed, "TTS streaming playback completed.");
                return;
            }

            if (!streamingPausedForBuffer && audioSource != null && !audioSource.isPlaying && elapsed > 0.05f)
                MarkTerminal(StageActionStatus.Completed, "TTS streaming playback stopped.");
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
            AbortActiveRequest();
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

        private bool ShouldUseStreaming(TtsHealthVoice voiceHealth, string text)
        {
            if (!streamRealTts || voiceHealth == null || !voiceHealth.streamingEnabled)
                return false;

            return CountNonWhitespaceCharacters(text) >= Mathf.Max(1, streamMinNonWhitespaceCharacters);
        }

        private static int CountNonWhitespaceCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    count++;
            }

            return count;
        }

        private int ResolveStreamingSampleRate(TtsHealthVoice voiceHealth)
        {
            var resolved = voiceHealth != null && voiceHealth.sampleRate > 0 ? voiceHealth.sampleRate : streamingSampleRate;
            return Mathf.Max(8000, resolved <= 0 ? 32000 : resolved);
        }

        private int ResolveStreamingMode(TtsHealthVoice voiceHealth)
        {
            var resolved = streamMode;
            if (voiceHealth != null && voiceHealth.streamingMode > 0)
                resolved = voiceHealth.streamingMode;
            return Mathf.Clamp(resolved <= 0 ? 1 : resolved, 1, 3);
        }

        private string GetStreamingProviderVersion(string baseVersion, int resolvedStreamMode)
        {
            var version = string.IsNullOrWhiteSpace(baseVersion) ? RealProviderVersionFallback : baseVersion.Trim();
            return $"{version}-raw-stream-m{Mathf.Clamp(resolvedStreamMode, 1, 3)}";
        }

        private string FormatStreamFailure(UnityWebRequest request, StreamingPcmBuffer buffer)
        {
            if (request == null)
                return "TTS streaming request failed.";

            var body = buffer != null ? DecodeUtf8(buffer.GetRawBytes()) : string.Empty;
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return $"TTS streaming request failed: HTTP {request.responseCode} {body}";

            return $"TTS streaming request failed: HTTP {request.responseCode} {request.error}";
        }

        private AudioClip CreateSilentClip(float clipDuration)
        {
            var sampleRate = Mathf.Max(8000, mockSampleRate);
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, clipDuration) * sampleRate));
            return AudioClip.Create($"MockTTS_{currentCharacterId}", sampleCount, 1, sampleRate, false);
        }

        private void StopAudio()
        {
            if (activeStreamBuffer != null)
                activeStreamBuffer.MarkCompleted();
            activeStreamBuffer = null;
            streamingPlayback = false;
            streamingPausedForBuffer = false;
            streamingTailGraceRemaining = -1f;
            streamingBufferedSeconds = 0f;
            streamingWrittenSeconds = 0f;
            streamingReceivedBytes = 0;

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

        private bool TryWritePcm16WavCacheFile(byte[] pcmBytes, int sampleRate, int channels, string targetPath, out string failureReason)
        {
            failureReason = string.Empty;
            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                failureReason = "TTS stream cache write failed: audio is empty.";
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = targetPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using (var stream = File.Create(tempPath))
                {
                    WritePcm16Wav(stream, pcmBytes, sampleRate, channels);
                }

                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);

                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"TTS stream cache write failed: {exception.Message}";
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

        private string BuildRealTtsRequestJson(string text, bool streaming, int resolvedStreamMode)
        {
            var builder = new StringBuilder(256 + (text != null ? text.Length : 0));
            builder.Append('{');
            AppendJson(builder, "characterId", currentCharacterId, false);
            AppendJson(builder, "voiceId", currentVoiceId, true);
            AppendJson(builder, "text", text, true);
            AppendJson(builder, "emotion", currentEmotion, true);
            builder.Append(",\"speed\":").Append(currentSpeed.ToString("0.###", CultureInfo.InvariantCulture));
            AppendJson(builder, "format", streaming ? "raw" : "wav", true);
            builder.Append(",\"stream\":").Append(streaming ? "true" : "false");
            if (streaming)
            {
                builder.Append(",\"streamingMode\":").Append(Mathf.Clamp(resolvedStreamMode, 1, 3));
                builder.Append(",\"sampleRate\":").Append(Mathf.Max(8000, streamingSampleRate));
            }

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

        private static void WritePcm16Wav(Stream stream, byte[] pcmBytes, int sampleRate, int channels)
        {
            var resolvedChannels = Mathf.Max(1, channels);
            var resolvedSampleRate = Mathf.Max(8000, sampleRate);
            var byteRate = resolvedSampleRate * resolvedChannels * 2;
            var blockAlign = resolvedChannels * 2;

            WriteAscii(stream, "RIFF");
            WriteInt32LE(stream, 36 + pcmBytes.Length);
            WriteAscii(stream, "WAVE");
            WriteAscii(stream, "fmt ");
            WriteInt32LE(stream, 16);
            WriteInt16LE(stream, 1);
            WriteInt16LE(stream, resolvedChannels);
            WriteInt32LE(stream, resolvedSampleRate);
            WriteInt32LE(stream, byteRate);
            WriteInt16LE(stream, blockAlign);
            WriteInt16LE(stream, 16);
            WriteAscii(stream, "data");
            WriteInt32LE(stream, pcmBytes.Length);
            stream.Write(pcmBytes, 0, pcmBytes.Length);
        }

        private static void WriteAscii(Stream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt16LE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
        }

        private static void WriteInt32LE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 24) & 0xff));
        }

        private static string DecodeUtf8(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
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

        private sealed class StreamingPcmBuffer
        {
            private readonly object gate = new object();
            private readonly MemoryStream rawStream = new MemoryStream();
            private float[] samples;
            private int readIndex;
            private int writeIndex;
            private int availableSamples;
            private int pendingByte = -1;
            private long receivedBytes;
            private long totalSamplesWritten;
            private long totalSamplesRead;
            private bool completed;

            public StreamingPcmBuffer(int sampleRate, int channels, int initialCapacitySamples)
            {
                SampleRate = Mathf.Max(8000, sampleRate);
                Channels = Mathf.Max(1, channels);
                samples = new float[Mathf.Max(1024, initialCapacitySamples)];
            }

            public int SampleRate { get; }
            public int Channels { get; }

            public long ReceivedBytes
            {
                get
                {
                    lock (gate)
                        return receivedBytes;
                }
            }

            public long RawByteCount
            {
                get
                {
                    lock (gate)
                        return rawStream.Length;
                }
            }

            public long TotalSamplesWritten
            {
                get
                {
                    lock (gate)
                        return totalSamplesWritten;
                }
            }

            public float BufferedSeconds
            {
                get
                {
                    lock (gate)
                        return availableSamples / (float)Mathf.Max(1, SampleRate);
                }
            }

            public float WrittenSeconds
            {
                get
                {
                    lock (gate)
                        return totalSamplesWritten / (float)Mathf.Max(1, SampleRate);
                }
            }

            public bool Completed
            {
                get
                {
                    lock (gate)
                        return completed;
                }
            }

            public bool IsDrained
            {
                get
                {
                    lock (gate)
                        return completed && availableSamples <= 0 && totalSamplesRead >= totalSamplesWritten;
                }
            }

            public void AppendPcm16(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return;

                lock (gate)
                {
                    var safeLength = Mathf.Min(dataLength, data.Length);
                    rawStream.Write(data, 0, safeLength);
                    receivedBytes += safeLength;
                    EnsureCapacity((safeLength + 1) / 2 + 1);

                    var offset = 0;
                    if (pendingByte >= 0 && offset < safeLength)
                    {
                        EnqueueSample(ToFloatSample(pendingByte, data[offset]));
                        pendingByte = -1;
                        offset++;
                    }

                    while (offset + 1 < safeLength)
                    {
                        EnqueueSample(ToFloatSample(data[offset], data[offset + 1]));
                        offset += 2;
                    }

                    if (offset < safeLength)
                        pendingByte = data[offset];
                }
            }

            public void Read(float[] data)
            {
                if (data == null)
                    return;

                lock (gate)
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        if (availableSamples > 0)
                        {
                            data[i] = samples[readIndex];
                            readIndex = (readIndex + 1) % samples.Length;
                            availableSamples--;
                            totalSamplesRead++;
                        }
                        else
                        {
                            data[i] = 0f;
                        }
                    }
                }
            }

            public void MarkCompleted()
            {
                lock (gate)
                    completed = true;
            }

            public byte[] GetRawBytes()
            {
                lock (gate)
                    return rawStream.ToArray();
            }

            private void EnsureCapacity(int samplesToAdd)
            {
                var free = samples.Length - availableSamples;
                if (free >= samplesToAdd)
                    return;

                var newLength = samples.Length;
                while (newLength - availableSamples < samplesToAdd)
                    newLength *= 2;

                var expanded = new float[newLength];
                for (var i = 0; i < availableSamples; i++)
                    expanded[i] = samples[(readIndex + i) % samples.Length];

                samples = expanded;
                readIndex = 0;
                writeIndex = availableSamples;
            }

            private void EnqueueSample(float value)
            {
                samples[writeIndex] = value;
                writeIndex = (writeIndex + 1) % samples.Length;
                availableSamples++;
                totalSamplesWritten++;
            }

            private static float ToFloatSample(int lowByte, int highByte)
            {
                var raw = (short)((lowByte & 0xff) | ((highByte & 0xff) << 8));
                return Mathf.Clamp(raw / 32768f, -1f, 1f);
            }
        }

        private sealed class StreamingPcmDownloadHandler : DownloadHandlerScript
        {
            public StreamingPcmDownloadHandler(StreamingPcmBuffer buffer)
                : base(new byte[4096])
            {
                Buffer = buffer;
            }

            public StreamingPcmBuffer Buffer { get; }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                Buffer?.AppendPcm16(data, dataLength);
                return true;
            }

            protected override void CompleteContent()
            {
                Buffer?.MarkCompleted();
            }
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
            public int sampleRate;
            public int channels;
            public bool streamingEnabled;
            public int streamingMode;
            public string streamMediaType;
        }
    }
}
