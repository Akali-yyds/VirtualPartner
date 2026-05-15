using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SpeechMouthDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MouthTextureController mouthTextureController;
        [SerializeField] private ExpressionActionExecutor expressionActionExecutor;

        [Header("Fallback Duration")]
        [SerializeField] private float minDuration = 1f;
        [SerializeField] private float maxDuration = 6f;
        [SerializeField] private float secondsPerCharacter = 0.08f;
        [SerializeField] private float mouthCycleSeconds = 0.18f;
        [SerializeField] private bool randomizeOpenMouthIndex;

        [Header("Audio RMS")]
        [SerializeField] private int rmsSampleSize = 256;
        [SerializeField] private float rmsGain = 8f;
        [SerializeField] private float rmsSmoothing = 14f;
        [SerializeField] private float smallThreshold = 0.08f;
        [SerializeField] private float midThreshold = 0.22f;
        [SerializeField] private float largeThreshold = 0.42f;
        [SerializeField] private float minMouthHoldSeconds = 0.08f;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool active;
        [SerializeField] private bool audioRmsMode;
        [SerializeField] private float elapsed;
        [SerializeField] private float duration;
        [SerializeField] private float currentRms;
        [SerializeField] private float smoothedOpenness;
        [SerializeField] private int currentMouthIndex = -1;
        [SerializeField] private string currentPoseSet = "-";
        [SerializeField] private string lastMessage = "Not initialized.";

        private MouthPoseSet speakingPoseSet;
        private AudioSource rmsAudioSource;
        private float[] rmsSamples;
        private bool warnedMissingPoseSet;
        private float randomMouthTimer;
        private float mouthHoldTimer;

        public bool Initialized => initialized;
        public bool Active => active;
        public bool AudioRmsMode => audioRmsMode;
        public float Elapsed => elapsed;
        public float Duration => duration;
        public float CurrentRms => currentRms;
        public float SmoothedOpenness => smoothedOpenness;
        public int CurrentMouthIndex => currentMouthIndex;
        public string CurrentPoseSet => currentPoseSet;
        public string LastMessage => lastMessage;
        public float MinDuration => minDuration;
        public float MaxDuration => maxDuration;
        public float SecondsPerCharacter => secondsPerCharacter;
        public bool RandomizeOpenMouthIndex => randomizeOpenMouthIndex;

        public void Configure(MouthTextureController mouthController, ExpressionActionExecutor expressionExecutor)
        {
            mouthTextureController = mouthController;
            expressionActionExecutor = expressionExecutor;
            initialized = ValidateReferences();
        }

        public float EstimateDuration(string text)
        {
            var charCount = CountNonWhitespaceCharacters(text);
            var estimated = charCount * Mathf.Max(0.001f, secondsPerCharacter);
            return Mathf.Clamp(estimated, Mathf.Max(0.01f, minDuration), Mathf.Max(minDuration, maxDuration));
        }

        public bool StartSpeech(string text, float speechDuration)
        {
            if (!initialized && !ValidateReferences())
                return false;

            active = true;
            audioRmsMode = false;
            rmsAudioSource = null;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, speechDuration);
            currentRms = 0f;
            smoothedOpenness = 0f;
            warnedMissingPoseSet = false;
            randomMouthTimer = 0f;
            mouthHoldTimer = 0f;

            ResolveSpeakingPoseSet();
            ManualUpdate(0f);
            return true;
        }

        public bool StartSpeechFromAudio(AudioSource audioSource, float speechDuration)
        {
            if (!initialized && !ValidateReferences())
                return false;
            if (audioSource == null)
                return Fail("AudioSource reference is missing for RMS mouth.");

            active = true;
            audioRmsMode = true;
            rmsAudioSource = audioSource;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, speechDuration);
            currentRms = 0f;
            smoothedOpenness = 0f;
            warnedMissingPoseSet = false;
            randomMouthTimer = 0f;
            mouthHoldTimer = 0f;
            EnsureRmsBuffer();

            ResolveSpeakingPoseSet();
            ManualUpdate(0f);
            return true;
        }

        public void RefreshSpeakingPoseSet()
        {
            if (!active)
                return;

            ResolveSpeakingPoseSet();
            ManualUpdate(0f);
        }

        public void StopSpeech()
        {
            active = false;
            audioRmsMode = false;
            rmsAudioSource = null;
            elapsed = 0f;
            duration = 0f;
            currentRms = 0f;
            smoothedOpenness = 0f;
            currentMouthIndex = -1;
            currentPoseSet = "-";
            speakingPoseSet = null;

            if (mouthTextureController != null)
                mouthTextureController.ClearSpeechMouth();

            lastMessage = "Speech mouth stopped.";
        }

        public void ManualUpdate(float deltaTime)
        {
            if (!active)
                return;

            var frameDelta = Mathf.Max(0f, deltaTime);
            elapsed = Mathf.Min(duration, elapsed + frameDelta);
            if (speakingPoseSet == null)
            {
                if (!warnedMissingPoseSet)
                {
                    warnedMissingPoseSet = true;
                    Debug.LogWarning($"[VirtualPartner] SpeechMouthDriver: {lastMessage}", this);
                }

                if (mouthTextureController != null)
                    mouthTextureController.ClearSpeechMouth();
                return;
            }

            var targetIndex = audioRmsMode
                ? GetAudioRmsSpeechIndex(frameDelta)
                : GetTextSpeechIndex(frameDelta);
            currentMouthIndex = ApplyMinimumHold(targetIndex, frameDelta);
            mouthTextureController.SetSpeechMouthIndex(currentMouthIndex);
        }

        private int GetTextSpeechIndex(float deltaTime)
        {
            if (randomizeOpenMouthIndex)
                return GetRandomSpeechIndex(deltaTime);

            return speakingPoseSet.GetSpeechIndex(CalculateOpenness(elapsed));
        }

        private int GetAudioRmsSpeechIndex(float deltaTime)
        {
            if (rmsAudioSource == null)
                return speakingPoseSet.Closed;

            EnsureRmsBuffer();
            rmsAudioSource.GetOutputData(rmsSamples, 0);

            var sum = 0f;
            for (var i = 0; i < rmsSamples.Length; i++)
                sum += rmsSamples[i] * rmsSamples[i];

            currentRms = Mathf.Sqrt(sum / Mathf.Max(1, rmsSamples.Length));
            var target = Mathf.Clamp01(currentRms * Mathf.Max(0.01f, rmsGain));
            var smoothing = 1f - Mathf.Exp(-Mathf.Max(0.01f, rmsSmoothing) * Mathf.Max(0.001f, deltaTime));
            smoothedOpenness = Mathf.Lerp(smoothedOpenness, target, smoothing);
            return speakingPoseSet.GetSpeechIndex(
                smoothedOpenness,
                smallThreshold,
                midThreshold,
                largeThreshold);
        }

        private int ApplyMinimumHold(int targetIndex, float deltaTime)
        {
            if (targetIndex == currentMouthIndex)
            {
                mouthHoldTimer += deltaTime;
                return currentMouthIndex;
            }

            if (currentMouthIndex >= -1 && mouthHoldTimer < Mathf.Max(0f, minMouthHoldSeconds))
            {
                mouthHoldTimer += deltaTime;
                return currentMouthIndex;
            }

            mouthHoldTimer = 0f;
            return targetIndex;
        }

        private int GetRandomSpeechIndex(float deltaTime)
        {
            if (currentMouthIndex < 0)
            {
                randomMouthTimer = Mathf.Max(0.04f, mouthCycleSeconds);
                return speakingPoseSet.GetRandomOpenIndex();
            }

            randomMouthTimer -= Mathf.Max(0f, deltaTime);
            if (randomMouthTimer > 0f)
                return currentMouthIndex;

            randomMouthTimer = Mathf.Max(0.04f, mouthCycleSeconds);
            return speakingPoseSet.GetRandomOpenIndex();
        }

        private void ResolveSpeakingPoseSet()
        {
            var previousPoseSet = speakingPoseSet;
            if (expressionActionExecutor != null
                && expressionActionExecutor.TryGetCurrentSpeakingPoseSet(out speakingPoseSet, out var poseMessage))
            {
                currentPoseSet = speakingPoseSet.PoseName;
                if (!string.IsNullOrWhiteSpace(poseMessage))
                    lastMessage = poseMessage;
                else
                    lastMessage = audioRmsMode
                        ? $"Audio RMS speech mouth started: {currentPoseSet}."
                        : $"Speech mouth started: {currentPoseSet}.";
            }
            else
            {
                speakingPoseSet = null;
                currentPoseSet = "-";
                lastMessage = expressionActionExecutor != null ? expressionActionExecutor.LastMessage : "No speaking mouth set.";
            }

            if (!ReferenceEquals(previousPoseSet, speakingPoseSet))
            {
                currentMouthIndex = -1;
                randomMouthTimer = 0f;
                mouthHoldTimer = Mathf.Max(0f, minMouthHoldSeconds);
            }
        }

        private float CalculateOpenness(float time)
        {
            var cycle = Mathf.Max(0.04f, mouthCycleSeconds);
            var phase = Mathf.Repeat(time, cycle) / cycle;
            return Mathf.Sin(phase * Mathf.PI);
        }

        private void EnsureRmsBuffer()
        {
            var sampleCount = Mathf.Clamp(rmsSampleSize, 64, 2048);
            if (rmsSamples == null || rmsSamples.Length != sampleCount)
                rmsSamples = new float[sampleCount];
        }

        private bool ValidateReferences()
        {
            if (mouthTextureController == null)
                return Fail("MouthTextureController reference is missing.");
            if (expressionActionExecutor == null)
                return Fail("ExpressionActionExecutor reference is missing.");

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
    }
}
