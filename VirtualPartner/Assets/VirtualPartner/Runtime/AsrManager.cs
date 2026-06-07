using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualPartner.Runtime
{
    public enum AsrRecognitionStatus
    {
        Idle,
        Listening,
        Recognizing,
        Done,
        Error,
        Canceled
    }

    public enum AsrResultMode
    {
        FillInputOnly,
        AutoSendToLlm
    }

    public enum AsrProviderMode
    {
        RealService,
        Mock
    }

    public sealed class AsrRecognitionResult
    {
        public AsrRecognitionResult(
            string sessionId,
            AsrRecognitionStatus status,
            string text,
            string error,
            AsrResultMode resultMode)
        {
            SessionId = sessionId ?? string.Empty;
            Status = status;
            Text = text ?? string.Empty;
            Error = error ?? string.Empty;
            ResultMode = resultMode;
        }

        public string SessionId { get; }
        public AsrRecognitionStatus Status { get; }
        public string Text { get; }
        public string Error { get; }
        public AsrResultMode ResultMode { get; }
    }

    [DisallowMultipleComponent]
    public sealed class AsrManager : MonoBehaviour
    {
        [Header("Provider")]
        [SerializeField] private AsrProviderMode providerMode = AsrProviderMode.RealService;
        [SerializeField] private AsrResultMode resultMode = AsrResultMode.FillInputOnly;
        [SerializeField] private bool asrUnavailable;

        [Header("Real ASR Service")]
        [SerializeField] private string serviceUrl = "http://127.0.0.1:8766";
        [SerializeField] private float statusPollIntervalSeconds = 0.25f;
        [SerializeField] private float asrSessionTimeoutSeconds = 30f;
        [SerializeField] private int requestTimeoutSeconds = 10;

        [Header("Mock ASR")]
        [SerializeField] private bool mockAsrEnabled = true;
        [SerializeField] private bool forceMockFailure;
        [SerializeField, TextArea(2, 4)] private string mockText = "We can continue with voice input.";
        [SerializeField] private float listeningSeconds = 0.7f;
        [SerializeField] private float recognizingSeconds = 0.8f;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool active;
        [SerializeField] private AsrRecognitionStatus status = AsrRecognitionStatus.Idle;
        [SerializeField] private string unitySessionToken = "-";
        [SerializeField] private string serverSessionId = "-";
        [SerializeField] private string latestText = "-";
        [SerializeField] private string latestError;
        [SerializeField] private string lastMessage = "Not initialized.";
        [SerializeField] private string healthStatusText = "Not checked.";
        [SerializeField] private string engineStatusText = "-";
        [SerializeField] private string modelStatusText = "-";
        [SerializeField] private string vadStatusText = "-";
        [SerializeField] private string microphoneStatusText = "-";
        [SerializeField] private string audioInputStatusText = "-";
        [SerializeField] private string serviceStatusText = "-";
        [SerializeField] private string runtimeStatusText = "-";
        [SerializeField] private float latestRms;
        [SerializeField] private float peakRms;
        [SerializeField] private bool speechDetected;
        [SerializeField] private float elapsed;

        private int sessionSequence;
        private int activeSessionToken;
        private float sessionRealtimeStartedAt;
        private bool activeMockSession;
        private Coroutine activeRoutine;
        private Coroutine healthRoutine;
        private UnityWebRequest activeRequest;

        public bool Initialized => initialized;
        public bool Active => active;
        public AsrRecognitionStatus Status => status;
        public AsrProviderMode ProviderMode => providerMode;
        public AsrResultMode ResultMode => resultMode;
        public string CurrentSessionId => unitySessionToken;
        public string UnitySessionToken => unitySessionToken;
        public string ServerSessionId => serverSessionId;
        public string LatestText => latestText;
        public string LatestError => latestError;
        public string LastMessage => lastMessage;
        public string HealthStatusText => healthStatusText;
        public string EngineStatusText => engineStatusText;
        public string ModelStatusText => modelStatusText;
        public string VadStatusText => vadStatusText;
        public string MicrophoneStatusText => microphoneStatusText;
        public string AudioInputStatusText => audioInputStatusText;
        public string ServiceStatusText => serviceStatusText;
        public string RuntimeStatusText => runtimeStatusText;
        public float LatestRms => latestRms;
        public float PeakRms => peakRms;
        public bool SpeechDetected => speechDetected;
        public float Elapsed => elapsed;
        public float ListeningSeconds => Mathf.Max(0.01f, listeningSeconds);
        public float RecognizingSeconds => Mathf.Max(0.01f, recognizingSeconds);
        public bool MockAsrEnabled => mockAsrEnabled;
        public bool ForceMockFailure => forceMockFailure;
        public bool AsrUnavailable => asrUnavailable;
        public string MockText => mockText;
        public string ServiceUrl => NormalizeServiceUrl();
        public float StatusPollIntervalSeconds => Mathf.Max(0.05f, statusPollIntervalSeconds);
        public float AsrSessionTimeoutSeconds => Mathf.Max(1f, asrSessionTimeoutSeconds);
        public int RequestTimeoutSeconds => Mathf.Max(1, requestTimeoutSeconds);

        public event Action<AsrRecognitionResult> RecognitionFinished;

        public void Configure()
        {
            initialized = true;
            if (status == AsrRecognitionStatus.Idle)
                lastMessage = "Ready.";
        }

        public bool StartRecognition(out string failureReason)
        {
            return providerMode == AsrProviderMode.Mock
                ? StartMockRecognition(out failureReason)
                : StartRealRecognition(out failureReason);
        }

        public bool StartRealRecognition(out string failureReason)
        {
            failureReason = string.Empty;
            if (!BeginSession(out var sessionToken, out failureReason))
                return false;

            if (asrUnavailable)
            {
                CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, "ASR unavailable.");
                return true;
            }

            status = AsrRecognitionStatus.Listening;
            serviceStatusText = "Starting";
            activeMockSession = false;
            lastMessage = "ASR service start requested.";
            activeRoutine = StartCoroutine(RealRecognitionRoutine(sessionToken));
            return true;
        }

        public bool StartMockRecognition(out string failureReason)
        {
            failureReason = string.Empty;
            if (!BeginSession(out var sessionToken, out failureReason))
                return false;

            serverSessionId = "-";
            status = AsrRecognitionStatus.Listening;
            serviceStatusText = "Mock";
            activeMockSession = true;
            lastMessage = "ASR listening.";

            if (asrUnavailable)
            {
                CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, "ASR unavailable.");
                return true;
            }

            if (!mockAsrEnabled)
            {
                CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, "MockASR disabled.");
                return true;
            }

            return true;
        }

        public void ManualUpdate(float deltaTime)
        {
            if (!active)
                return;

            if (!activeMockSession)
            {
                elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - sessionRealtimeStartedAt);
                return;
            }

            var frameDelta = Mathf.Max(0f, deltaTime);
            elapsed += frameDelta;

            var listeningDuration = ListeningSeconds;
            if (status == AsrRecognitionStatus.Listening && elapsed >= listeningDuration)
            {
                status = AsrRecognitionStatus.Recognizing;
                lastMessage = "ASR recognizing.";
            }

            if (status != AsrRecognitionStatus.Recognizing || elapsed < listeningDuration + RecognizingSeconds)
                return;

            if (forceMockFailure)
                CompleteSession(activeSessionToken, AsrRecognitionStatus.Error, string.Empty, "MockASR forced failure.");
            else
                CompleteSession(activeSessionToken, AsrRecognitionStatus.Done, mockText, string.Empty);
        }

        public void CancelRecognition()
        {
            if (!active)
            {
                status = AsrRecognitionStatus.Canceled;
                lastMessage = "ASR canceled.";
                return;
            }

            var canceledSessionId = unitySessionToken;
            var canceledServerSession = serverSessionId;
            var hadServerSession = !string.IsNullOrWhiteSpace(canceledServerSession) && canceledServerSession != "-";

            InvalidateActiveSession();
            active = false;
            status = AsrRecognitionStatus.Canceled;
            latestError = string.Empty;
            serviceStatusText = "Canceled";
            lastMessage = "ASR canceled.";

            if (hadServerSession && isActiveAndEnabled)
                StartCoroutine(CancelServerSessionRoutine(canceledServerSession));

            RecognitionFinished?.Invoke(new AsrRecognitionResult(
                canceledSessionId,
                status,
                string.Empty,
                string.Empty,
                resultMode));
        }

        public void RequestHealthCheck()
        {
            if (!initialized)
                Configure();

            if (healthRoutine != null)
                StopCoroutine(healthRoutine);
            healthRoutine = StartCoroutine(HealthCheckRoutine());
        }

        public void SetProviderMode(AsrProviderMode mode)
        {
            if (active)
                return;
            providerMode = mode;
        }

        public void SetResultMode(AsrResultMode mode)
        {
            resultMode = mode;
        }

        public void SetMockFailureMode(bool shouldFail)
        {
            forceMockFailure = shouldFail;
        }

        public void SetUnavailable(bool unavailable)
        {
            asrUnavailable = unavailable;
        }

        public void SetMockText(string text)
        {
            mockText = text ?? string.Empty;
        }

        private bool BeginSession(out int sessionToken, out string failureReason)
        {
            failureReason = string.Empty;
            sessionToken = 0;
            if (!initialized)
                Configure();

            if (active)
            {
                failureReason = "ASR session is already active.";
                lastMessage = failureReason;
                return false;
            }

            sessionSequence++;
            sessionToken = sessionSequence;
            activeSessionToken = sessionToken;
            sessionRealtimeStartedAt = Time.realtimeSinceStartup;
            activeMockSession = false;
            unitySessionToken = $"unity_asr_{sessionSequence.ToString(CultureInfo.InvariantCulture)}";
            serverSessionId = "-";
            active = true;
            elapsed = 0f;
            latestText = "-";
            latestError = string.Empty;
            serviceStatusText = "-";
            latestRms = 0f;
            peakRms = 0f;
            speechDetected = false;
            status = AsrRecognitionStatus.Listening;
            return true;
        }

        private IEnumerator RealRecognitionRoutine(int sessionToken)
        {
            using (var request = CreateJsonRequest("/asr/start", "{}"))
            {
                activeRequest = request;
                yield return request.SendWebRequest();

                if (!IsSessionCurrent(sessionToken))
                    yield break;
                activeRequest = null;

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, FormatHttpFailure(request, body));
                    yield break;
                }

                var response = ParseJson<AsrStartResponse>(body);
                if (response == null || !response.success)
                {
                    var message = response != null && !string.IsNullOrWhiteSpace(response.message)
                        ? response.message
                        : "ASR service start failed.";
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, message);
                    yield break;
                }

                serverSessionId = string.IsNullOrWhiteSpace(response.sessionId) ? "-" : response.sessionId;
                if (serverSessionId == "-")
                {
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, "ASR service returned empty sessionId.");
                    yield break;
                }

                status = AsrRecognitionStatus.Listening;
                serviceStatusText = "Listening";
                lastMessage = "ASR service listening.";
            }

            while (IsSessionCurrent(sessionToken))
            {
                elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - sessionRealtimeStartedAt);
                if (elapsed >= AsrSessionTimeoutSeconds)
                {
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, "ASR session timed out.");
                    yield break;
                }

                yield return new WaitForSeconds(StatusPollIntervalSeconds);

                if (!IsSessionCurrent(sessionToken))
                    yield break;

                yield return PollStatusRoutine(sessionToken);
            }
        }

        private IEnumerator PollStatusRoutine(int sessionToken)
        {
            var path = "/asr/status?sessionId=" + Uri.EscapeDataString(serverSessionId);
            using (var request = UnityWebRequest.Get(BuildUrl(path)))
            {
                activeRequest = request;
                request.timeout = RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (!IsSessionCurrent(sessionToken))
                    yield break;
                activeRequest = null;

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, FormatHttpFailure(request, body));
                    yield break;
                }

                var response = ParseJson<AsrStatusResponse>(body);
                if (response == null || !response.success)
                {
                    var message = response != null && !string.IsNullOrWhiteSpace(response.error)
                        ? response.error
                        : response != null && !string.IsNullOrWhiteSpace(response.message)
                            ? response.message
                            : "ASR status failed.";
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, message);
                    yield break;
                }

                ApplyStatusResponse(sessionToken, response);
            }
        }

        private IEnumerator HealthCheckRoutine()
        {
            healthStatusText = "Checking...";
            using (var request = UnityWebRequest.Get(BuildUrl("/health")))
            {
                request.timeout = RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    healthStatusText = FormatHttpFailure(request, request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                    healthRoutine = null;
                    yield break;
                }

                var response = ParseJson<AsrHealthResponse>(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                ApplyHealthResponse(response);
                healthRoutine = null;
            }
        }

        private IEnumerator CancelServerSessionRoutine(string sessionId)
        {
            var body = "{\"sessionId\":\"" + JsonTextUtility.Escape(sessionId) + "\"}";
            using (var request = CreateJsonRequest("/asr/cancel", body))
            {
                yield return request.SendWebRequest();
            }
        }

        private void ApplyStatusResponse(int sessionToken, AsrStatusResponse response)
        {
            if (!IsSessionCurrent(sessionToken))
                return;

            var remoteStatus = (response.status ?? string.Empty).Trim().ToLowerInvariant();
            serviceStatusText = string.IsNullOrWhiteSpace(response.status) ? "-" : response.status;
            if (response.duration > 0f)
                elapsed = response.duration;
            latestRms = response.latestRms;
            peakRms = Mathf.Max(peakRms, response.peakRms);
            speechDetected = speechDetected || response.speechDetected;

            switch (remoteStatus)
            {
                case "idle":
                case "listening":
                case "speaking":
                    status = AsrRecognitionStatus.Listening;
                    lastMessage = "ASR service listening.";
                    break;
                case "recognizing":
                    status = AsrRecognitionStatus.Recognizing;
                    lastMessage = "ASR service recognizing.";
                    break;
                case "done":
                    CompleteSession(sessionToken, AsrRecognitionStatus.Done, response.text, string.Empty);
                    break;
                case "failed":
                case "error":
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, response.error);
                    break;
                case "canceled":
                    CompleteSession(sessionToken, AsrRecognitionStatus.Canceled, string.Empty, string.Empty);
                    break;
                default:
                    CompleteSession(sessionToken, AsrRecognitionStatus.Error, string.Empty, $"Unknown ASR status '{response.status}'.");
                    break;
            }
        }

        private void ApplyHealthResponse(AsrHealthResponse response)
        {
            if (response == null)
            {
                healthStatusText = "Health parse failed.";
                return;
            }

            engineStatusText = FormatPart(response.engine);
            modelStatusText = FormatPart(response.model);
            vadStatusText = FormatPart(response.vad);
            microphoneStatusText = FormatPart(response.microphone);
            audioInputStatusText = FormatAudioInput(response.audioInput);
            runtimeStatusText = FormatRuntime(response.runtime);
            serviceStatusText = string.IsNullOrWhiteSpace(response.currentStatus) ? serviceStatusText : response.currentStatus;
            latestRms = response.latestRms;
            peakRms = response.peakRms;
            speechDetected = response.speechDetected;
            healthStatusText = response.success ? "ASR health ok." : $"ASR health degraded: {response.message}";
            lastMessage = healthStatusText;
        }

        private void CompleteSession(int sessionToken, AsrRecognitionStatus finalStatus, string text, string error)
        {
            if (sessionToken != activeSessionToken || !active)
                return;

            var completedSessionId = unitySessionToken;
            active = false;
            activeSessionToken = 0;
            activeMockSession = false;
            status = finalStatus;
            latestText = string.IsNullOrWhiteSpace(text) ? "-" : text;
            latestError = error ?? string.Empty;

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest = null;
            }

            switch (finalStatus)
            {
                case AsrRecognitionStatus.Done:
                    lastMessage = string.IsNullOrWhiteSpace(text) ? "No speech recognized." : "ASR completed.";
                    break;
                case AsrRecognitionStatus.Error:
                    lastMessage = string.IsNullOrWhiteSpace(error) ? "ASR failed." : error;
                    Debug.LogWarning($"[VirtualPartner] ASR warning: {lastMessage}", this);
                    break;
                case AsrRecognitionStatus.Canceled:
                    lastMessage = "ASR canceled.";
                    break;
                default:
                    lastMessage = finalStatus.ToString();
                    break;
            }

            RecognitionFinished?.Invoke(new AsrRecognitionResult(
                completedSessionId,
                finalStatus,
                text,
                latestError,
                resultMode));
        }

        private void InvalidateActiveSession()
        {
            activeSessionToken = 0;
            activeMockSession = false;
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest = null;
            }
        }

        private bool IsSessionCurrent(int sessionToken)
        {
            return sessionToken == activeSessionToken && active;
        }

        private UnityWebRequest CreateJsonRequest(string path, string json)
        {
            var request = new UnityWebRequest(BuildUrl(path), "POST");
            request.timeout = RequestTimeoutSeconds;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? "{}"));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private string FormatHttpFailure(UnityWebRequest request, string body)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                var response = ParseJson<AsrErrorResponse>(body);
                if (response != null && !string.IsNullOrWhiteSpace(response.message))
                    return $"ASR request failed: HTTP {request.responseCode} {response.message}";
                if (response != null && !string.IsNullOrWhiteSpace(response.error))
                    return $"ASR request failed: HTTP {request.responseCode} {response.error}";
            }

            if (!string.IsNullOrWhiteSpace(body))
                return $"ASR request failed: HTTP {request.responseCode} {body}";
            return $"ASR request failed: HTTP {request.responseCode} {request.error}";
        }

        private T ParseJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                latestError = $"ASR JSON parse failed: {exception.Message}";
                return null;
            }
        }

        private string FormatPart(AsrHealthPart part)
        {
            if (part == null)
                return "-";
            var name = string.IsNullOrWhiteSpace(part.name) ? string.Empty : $"{part.name} ";
            return $"{name}{(part.ok ? "ok" : "failed")}: {part.message}";
        }

        private string FormatRuntime(AsrRuntimeHealth runtime)
        {
            if (runtime == null)
                return "-";
            var state = runtime.ready ? "ready" : runtime.warming ? "warming" : "not ready";
            return $"{state}: {runtime.message} ({runtime.warmupSeconds:0.00}s)";
        }

        private string FormatAudioInput(AsrAudioInputHealth audioInput)
        {
            if (audioInput == null)
                return "-";
            var state = audioInput.ready ? "ready" : audioInput.warming ? "warming" : "not ready";
            return $"{state}: {audioInput.message}";
        }

        private string NormalizeServiceUrl()
        {
            return string.IsNullOrWhiteSpace(serviceUrl)
                ? "http://127.0.0.1:8766"
                : serviceUrl.Trim().TrimEnd('/');
        }

        private string BuildUrl(string path)
        {
            return NormalizeServiceUrl() + path;
        }

        [Serializable]
        private sealed class AsrStartResponse
        {
            public bool success;
            public string sessionId;
            public string message;
            public bool busy;
        }

        [Serializable]
        private sealed class AsrErrorResponse
        {
            public bool success;
            public string message;
            public string error;
        }

        [Serializable]
        private sealed class AsrStatusResponse
        {
            public bool success;
            public string sessionId;
            public string status;
            public string text;
            public float duration;
            public string engine;
            public string error;
            public string message;
            public float latestRms;
            public float peakRms;
            public bool speechDetected;
        }

        [Serializable]
        private sealed class AsrHealthResponse
        {
            public bool success;
            public string message;
            public AsrHealthPart wrapper;
            public AsrHealthPart engine;
            public AsrHealthPart model;
            public AsrHealthPart vad;
            public AsrHealthPart microphone;
            public AsrAudioInputHealth audioInput;
            public AsrRuntimeHealth runtime;
            public string activeSession;
            public string currentStatus;
            public float latestRms;
            public float peakRms;
            public bool speechDetected;
        }

        [Serializable]
        private sealed class AsrHealthPart
        {
            public bool ok;
            public string name;
            public string message;
        }

        [Serializable]
        private sealed class AsrRuntimeHealth
        {
            public bool ready;
            public bool warming;
            public string message;
            public float warmupSeconds;
        }

        [Serializable]
        private sealed class AsrAudioInputHealth
        {
            public bool ready;
            public bool warming;
            public string message;
        }
    }
}
