using System;
using UnityEngine;

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
        [Header("Mock ASR")]
        [SerializeField] private bool mockAsrEnabled = true;
        [SerializeField] private bool forceMockFailure;
        [SerializeField] private bool asrUnavailable;
        [SerializeField] private AsrResultMode resultMode = AsrResultMode.FillInputOnly;
        [SerializeField, TextArea(2, 4)] private string mockText = "We can continue with voice input.";
        [SerializeField] private float listeningSeconds = 0.7f;
        [SerializeField] private float recognizingSeconds = 0.8f;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool active;
        [SerializeField] private AsrRecognitionStatus status = AsrRecognitionStatus.Idle;
        [SerializeField] private string currentSessionId = "-";
        [SerializeField] private string latestText = "-";
        [SerializeField] private string latestError;
        [SerializeField] private string lastMessage = "Not initialized.";
        [SerializeField] private float elapsed;

        private int sessionSequence;
        private int activeSessionToken;

        public bool Initialized => initialized;
        public bool Active => active;
        public AsrRecognitionStatus Status => status;
        public AsrResultMode ResultMode => resultMode;
        public string CurrentSessionId => currentSessionId;
        public string LatestText => latestText;
        public string LatestError => latestError;
        public string LastMessage => lastMessage;
        public float Elapsed => elapsed;
        public float ListeningSeconds => Mathf.Max(0.01f, listeningSeconds);
        public float RecognizingSeconds => Mathf.Max(0.01f, recognizingSeconds);
        public bool MockAsrEnabled => mockAsrEnabled;
        public bool ForceMockFailure => forceMockFailure;
        public bool AsrUnavailable => asrUnavailable;
        public string MockText => mockText;

        public event Action<AsrRecognitionResult> RecognitionFinished;

        public void Configure()
        {
            initialized = true;
            if (status == AsrRecognitionStatus.Idle)
                lastMessage = "Ready.";
        }

        public bool StartMockRecognition(out string failureReason)
        {
            failureReason = string.Empty;
            if (!initialized)
                Configure();

            if (active)
            {
                failureReason = "ASR session is already active.";
                lastMessage = failureReason;
                return false;
            }

            sessionSequence++;
            activeSessionToken = sessionSequence;
            currentSessionId = $"asr_{sessionSequence}";
            active = true;
            elapsed = 0f;
            latestText = "-";
            latestError = string.Empty;
            status = AsrRecognitionStatus.Listening;
            lastMessage = "ASR listening.";

            if (asrUnavailable)
            {
                CompleteSession(activeSessionToken, AsrRecognitionStatus.Error, string.Empty, "ASR unavailable.");
                return true;
            }

            if (!mockAsrEnabled)
            {
                CompleteSession(activeSessionToken, AsrRecognitionStatus.Error, string.Empty, "MockASR disabled.");
                return true;
            }

            return true;
        }

        public void ManualUpdate(float deltaTime)
        {
            if (!active)
                return;

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

            var canceledSessionId = currentSessionId;
            active = false;
            activeSessionToken = 0;
            status = AsrRecognitionStatus.Canceled;
            latestError = string.Empty;
            lastMessage = "ASR canceled.";
            RecognitionFinished?.Invoke(new AsrRecognitionResult(
                canceledSessionId,
                status,
                string.Empty,
                string.Empty,
                resultMode));
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

        private void CompleteSession(int sessionToken, AsrRecognitionStatus finalStatus, string text, string error)
        {
            if (sessionToken != activeSessionToken || !active)
                return;

            var completedSessionId = currentSessionId;
            active = false;
            activeSessionToken = 0;
            status = finalStatus;
            latestText = string.IsNullOrWhiteSpace(text) ? "-" : text;
            latestError = error ?? string.Empty;

            switch (finalStatus)
            {
                case AsrRecognitionStatus.Done:
                    lastMessage = string.IsNullOrWhiteSpace(text) ? "No speech recognized." : "ASR completed.";
                    break;
                case AsrRecognitionStatus.Error:
                    lastMessage = string.IsNullOrWhiteSpace(error) ? "ASR failed." : error;
                    Debug.LogWarning($"[VirtualPartner] ASR warning: {lastMessage}", this);
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
    }
}
