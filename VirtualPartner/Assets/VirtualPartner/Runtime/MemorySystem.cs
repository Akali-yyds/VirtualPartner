using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MemorySystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxMemoryPromptChars = 3000;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool processing;
        [SerializeField] private int queueCount;
        [SerializeField] private string statusText = "Not configured.";
        [SerializeField] private string lastMessage;
        [SerializeField] private string latestDecision;
        [SerializeField] private string latestWritePath;
        [SerializeField] private string latestRawMemoryJudgeResponse;
        [SerializeField] private string latestParseError;
        [SerializeField] private int loadedMemoryPromptChars;
        [SerializeField] private bool memoryPromptTruncated;

        private readonly Dictionary<int, MemoryTurnState> turns = new Dictionary<int, MemoryTurnState>();
        private readonly Queue<MemoryTurnState> queue = new Queue<MemoryTurnState>();
        private readonly MarkdownMemoryStore memoryStore = new MarkdownMemoryStore();
        private readonly MemoryJudgeClient judgeClient = new MemoryJudgeClient();

        private CharacterProfile defaultProfile;
        private ConversationRequestRegistry requestRegistry;
        private bool subscribedToRegistry;
        private Coroutine processRoutine;
        private MemoryTurnState latestTurn;
        private readonly List<int> finalizedTurnBuffer = new List<int>();

        public bool Initialized => initialized;
        public bool Processing => processing;
        public int QueueCount => queueCount;
        public string StatusText => statusText;
        public string LastMessage => lastMessage;
        public string LatestDecision => latestDecision;
        public string LatestWritePath => latestWritePath;
        public string LatestRawMemoryJudgeResponse => latestRawMemoryJudgeResponse;
        public string LatestParseError => latestParseError;
        public int LoadedMemoryPromptChars => loadedMemoryPromptChars;
        public bool MemoryPromptTruncated => memoryPromptTruncated;
        public int MaxMemoryPromptChars => Mathf.Max(0, maxMemoryPromptChars);
        public string ConfigStatus => judgeClient.ConfigStatus;
        public string ConfigPath => judgeClient.ConfigPath;
        public string MemoryRootPath => memoryStore.GetRootPath(GetDefaultCharacterId());

        public void Configure(CharacterProfile profile, ConversationRequestRegistry registry)
        {
            defaultProfile = profile;
            initialized = true;
            statusText = "Ready.";
            lastMessage = string.Empty;

            if (requestRegistry != null && subscribedToRegistry)
                requestRegistry.StatusChanged -= HandleRequestStatusChanged;
            requestRegistry = registry;
            subscribedToRegistry = false;
            if (requestRegistry != null)
            {
                requestRegistry.StatusChanged += HandleRequestStatusChanged;
                subscribedToRegistry = true;
            }

            var characterId = GetDefaultCharacterId();
            if (!string.IsNullOrWhiteSpace(characterId))
                memoryStore.EnsureCategoryFiles(characterId);

            judgeClient.ReloadConfig();
        }

        public bool ReloadJudgeConfig()
        {
            return judgeClient.ReloadConfig();
        }

        private void OnDisable()
        {
            if (requestRegistry != null && subscribedToRegistry)
            {
                requestRegistry.StatusChanged -= HandleRequestStatusChanged;
                subscribedToRegistry = false;
            }

            if (processRoutine != null)
            {
                StopCoroutine(processRoutine);
                processRoutine = null;
            }

            judgeClient.Abort();
            processing = false;
        }

        // Registry-driven lifecycle: terminal status decides memory enqueue vs drop.
        private void HandleRequestStatusChanged(ConversationRequest request)
        {
            if (request == null)
                return;

            switch (request.Status)
            {
                case RequestStatus.Finished:
                    FinalizeFinished(request.RequestId, request.CharacterId);
                    break;
                case RequestStatus.Failed:
                case RequestStatus.Canceled:
                case RequestStatus.Replaced:
                    DropTurn(request.RequestId, request.Status.ToString());
                    break;
            }
        }

        private void FinalizeFinished(int requestId, string characterId)
        {
            if (!turns.TryGetValue(requestId, out var state))
                return;

            state.CharacterId = NormalizeCharacterId(string.IsNullOrWhiteSpace(characterId) ? state.CharacterId : characterId);
            latestTurn = state;

            if (state.Dropped)
            {
                lastMessage = $"Request {requestId} memory skipped: dropped.";
                if (!state.Queued)
                    turns.Remove(requestId);
                return;
            }

            if (!state.HasSpeech)
            {
                lastMessage = $"Request {requestId} memory skipped: no speech.";
                turns.Remove(requestId);
                return;
            }

            Enqueue(state);
        }

        private void DropTurn(int requestId, string reason)
        {
            if (!turns.TryGetValue(requestId, out var state))
                return;

            state.Dropped = true;
            lastMessage = $"Request {requestId} memory dropped: {reason}.";
            if (!state.Queued)
                turns.Remove(requestId);
        }

        public void RegisterUserMessage(string characterId, int requestId, string userText)
        {
            if (requestId <= 0 || string.IsNullOrWhiteSpace(characterId))
                return;

            var state = new MemoryTurnState
            {
                CharacterId = NormalizeCharacterId(characterId),
                CharacterName = ResolveCharacterName(characterId),
                RequestId = requestId,
                UserText = userText == null ? string.Empty : userText.Trim()
            };

            turns[requestId] = state;
            latestTurn = state;
            statusText = $"Tracking request {requestId}.";
            lastMessage = statusText;
        }

        public void RecordSpeech(StagePlanSpeechEvent speech)
        {
            if (speech == null || speech.RequestId <= 0)
                return;

            if (!turns.TryGetValue(speech.RequestId, out var state))
                return;
            if (state.Dropped)
                return;
            if (requestRegistry != null && requestRegistry.IsCanceledOrReplaced(speech.RequestId))
                return;

            state.CharacterId = NormalizeCharacterId(string.IsNullOrWhiteSpace(speech.CharacterId) ? state.CharacterId : speech.CharacterId);
            state.Speeches.Add(speech.Text == null ? string.Empty : speech.Text.Trim());
            latestTurn = state;
            lastMessage = $"Recorded speech for request {speech.RequestId}.";
        }

        public string BuildPromptContext(string characterId)
        {
            var result = memoryStore.BuildPromptContext(characterId, MaxMemoryPromptChars);
            loadedMemoryPromptChars = result.CharacterCount;
            memoryPromptTruncated = result.Truncated;
            return result.Text;
        }

        public void ReloadMemory()
        {
            var characterId = GetDefaultCharacterId();
            memoryStore.EnsureCategoryFiles(characterId);
            BuildPromptContext(characterId);
            statusText = "Memory reloaded.";
            lastMessage = statusText;
        }

        public void ClearMemory(string characterId)
        {
            var normalized = NormalizeCharacterId(characterId);
            DropCharacterTurns(normalized);
            var fileCount = memoryStore.Clear(normalized);
            loadedMemoryPromptChars = 0;
            memoryPromptTruncated = false;
            latestDecision = "Memory cleared.";
            latestWritePath = memoryStore.GetRootPath(normalized);
            latestRawMemoryJudgeResponse = string.Empty;
            latestParseError = string.Empty;
            statusText = $"Memory cleared for {normalized}.";
            lastMessage = $"Cleared {fileCount} memory file(s) for {normalized}.";
        }

        public bool QueueLatestTurnForDebug()
        {
            if (latestTurn == null)
            {
                lastMessage = "No latest memory turn.";
                return false;
            }

            if (!latestTurn.HasSpeech)
            {
                lastMessage = "Latest memory turn has no speech.";
                return false;
            }

            latestTurn.Dropped = false;
            Enqueue(latestTurn);
            return true;
        }

        public void ClearLatestDecision()
        {
            latestDecision = string.Empty;
            latestWritePath = string.Empty;
            latestRawMemoryJudgeResponse = string.Empty;
            latestParseError = string.Empty;
            lastMessage = "Latest memory decision cleared.";
        }

        public void OpenMemoryFolder()
        {
            var path = MemoryRootPath;
            memoryStore.EnsureCategoryFiles(GetDefaultCharacterId());
            Application.OpenURL("file:///" + path.Replace("\\", "/"));
        }

        private void DropCharacterTurns(string characterId)
        {
            var normalized = NormalizeCharacterId(characterId);
            finalizedTurnBuffer.Clear();
            foreach (var pair in turns)
            {
                var state = pair.Value;
                if (state == null || !SameCharacter(state.CharacterId, normalized))
                    continue;

                state.Dropped = true;
                if (!state.Queued)
                    finalizedTurnBuffer.Add(pair.Key);
            }

            for (var i = 0; i < finalizedTurnBuffer.Count; i++)
                turns.Remove(finalizedTurnBuffer[i]);
        }

        private void Enqueue(MemoryTurnState state)
        {
            if (state == null || state.Queued)
                return;

            state.Queued = true;
            queue.Enqueue(state);
            queueCount = queue.Count;
            statusText = $"Queued memory judge for request {state.RequestId}.";
            lastMessage = statusText;

            if (processRoutine == null)
                processRoutine = StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            processing = true;
            while (queue.Count > 0)
            {
                queueCount = queue.Count;
                var state = queue.Dequeue();
                queueCount = queue.Count;
                if (state != null)
                    turns.Remove(state.RequestId);

                if (state == null || state.Dropped || !state.HasSpeech)
                {
                    latestDecision = "Skipped before judge.";
                    latestWritePath = string.Empty;
                    latestParseError = string.Empty;
                    lastMessage = state == null ? "Memory turn missing." : $"Request {state.RequestId} skipped before judge.";
                    continue;
                }

                statusText = $"Judging request {state.RequestId}.";
                latestDecision = statusText;
                var judgeRequest = new MemoryJudgeRequest
                {
                    CharacterId = state.CharacterId,
                    CharacterName = state.CharacterName,
                    RequestId = state.RequestId,
                    UserText = state.UserText,
                    CharacterSpeechText = state.GetSpeechText(),
                    ExistingMemoryContext = BuildPromptContext(state.CharacterId)
                };

                MemoryJudgeResult judgeResult = null;
                yield return StartCoroutine(judgeClient.Judge(judgeRequest, result => judgeResult = result));

                latestRawMemoryJudgeResponse = judgeResult != null ? judgeResult.RawResponse : string.Empty;
                latestParseError = judgeResult != null ? judgeResult.ParseError : "MemoryJudge returned no result.";

                if (state.Dropped)
                {
                    latestDecision = $"Request {state.RequestId} skipped after judge: dropped.";
                    latestWritePath = string.Empty;
                    lastMessage = latestDecision;
                    continue;
                }

                if (judgeResult == null || !judgeResult.Success)
                {
                    latestDecision = "MemoryJudge failed.";
                    latestWritePath = string.Empty;
                    lastMessage = judgeResult != null ? judgeResult.Error : "MemoryJudge returned no result.";
                    Debug.LogWarning($"[VirtualPartner] MemoryJudge failed: {lastMessage}", this);
                    continue;
                }

                if (!memoryStore.ValidateDecision(judgeResult.Decision, out var validationFailure))
                {
                    latestDecision = "MemoryJudge decision rejected.";
                    latestParseError = validationFailure;
                    latestWritePath = string.Empty;
                    lastMessage = validationFailure;
                    Debug.LogWarning($"[VirtualPartner] Memory decision rejected: {validationFailure}", this);
                    continue;
                }

                latestDecision = FormatDecision(judgeResult.Decision);
                if (!judgeResult.Decision.shouldRemember)
                {
                    latestWritePath = string.Empty;
                    lastMessage = "MemoryJudge decided not to remember.";
                    continue;
                }

                var writeResult = memoryStore.Append(state.CharacterId, state.RequestId, judgeResult.Decision);
                latestWritePath = writeResult.Path ?? string.Empty;
                lastMessage = writeResult.Message;
                if (writeResult.Wrote)
                    statusText = $"Memory written for request {state.RequestId}.";
                else if (writeResult.SkippedDuplicate)
                    statusText = $"Duplicate memory skipped for request {state.RequestId}.";
                else
                    statusText = "Memory not written.";

                BuildPromptContext(state.CharacterId);
            }

            processing = false;
            processRoutine = null;
            queueCount = 0;
            if (string.IsNullOrWhiteSpace(statusText) || statusText.StartsWith("Judging"))
                statusText = "Ready.";
        }

        private string ResolveCharacterName(string characterId)
        {
            if (CharacterRegistry.TryGet(characterId, out var context) && context != null && context.Profile != null)
                return context.Profile.DisplayName;

            if (defaultProfile != null && SameCharacter(defaultProfile.CharacterId, characterId))
                return defaultProfile.DisplayName;

            return characterId;
        }

        private string GetDefaultCharacterId()
        {
            return defaultProfile != null ? defaultProfile.CharacterId : "toki";
        }

        private static string FormatDecision(MemoryJudgeDecision decision)
        {
            if (decision == null)
                return string.Empty;

            if (!decision.shouldRemember)
                return "shouldRemember=false";

            return $"shouldRemember=true category={decision.category} importance={decision.importance} title={decision.title}";
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? "unknown" : characterId.Trim().ToLowerInvariant();
        }

        private static bool SameCharacter(string left, string right)
        {
            return string.Equals(
                NormalizeCharacterId(left),
                NormalizeCharacterId(right),
                System.StringComparison.OrdinalIgnoreCase);
        }

        private sealed class MemoryTurnState
        {
            public string CharacterId { get; set; }
            public string CharacterName { get; set; }
            public int RequestId { get; set; }
            public string UserText { get; set; }
            public bool Dropped { get; set; }
            public bool Queued { get; set; }
            public List<string> Speeches { get; } = new List<string>();
            public bool HasSpeech => Speeches.Count > 0;

            public string GetSpeechText()
            {
                var builder = new StringBuilder(256);
                for (var i = 0; i < Speeches.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(Speeches[i]))
                        continue;

                    if (builder.Length > 0)
                        builder.AppendLine();
                    builder.Append(Speeches[i].Trim());
                }

                return builder.ToString();
            }
        }
    }
}
