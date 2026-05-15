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

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool active;
        [SerializeField] private float elapsed;
        [SerializeField] private float duration;
        [SerializeField] private int currentMouthIndex = -1;
        [SerializeField] private string currentPoseSet = "-";
        [SerializeField] private string lastMessage = "Not initialized.";

        private MouthPoseSet speakingPoseSet;
        private bool warnedMissingPoseSet;
        private float randomMouthTimer;

        public bool Initialized => initialized;
        public bool Active => active;
        public float Elapsed => elapsed;
        public float Duration => duration;
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
            elapsed = 0f;
            duration = Mathf.Max(0.01f, speechDuration);
            warnedMissingPoseSet = false;
            randomMouthTimer = 0f;

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
            elapsed = 0f;
            duration = 0f;
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

            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, deltaTime));
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

            currentMouthIndex = randomizeOpenMouthIndex
                ? GetRandomSpeechIndex(deltaTime)
                : speakingPoseSet.GetSpeechIndex(CalculateOpenness(elapsed));
            mouthTextureController.SetSpeechMouthIndex(currentMouthIndex);
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
                    lastMessage = $"Speech mouth started: {currentPoseSet}.";
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
            }
        }

        private float CalculateOpenness(float time)
        {
            var cycle = Mathf.Max(0.04f, mouthCycleSeconds);
            var phase = Mathf.Repeat(time, cycle) / cycle;
            return Mathf.Sin(phase * Mathf.PI);
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
