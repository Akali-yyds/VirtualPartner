using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Single source of truth for conversation request lifecycle state, keyed by
    /// requestId. Controlled state machine: only Pending/Playing may enter a terminal
    /// status; terminal records are retained (bounded) so late-arriving events can
    /// still detect canceled/replaced requests, then evicted oldest-first past the cap.
    /// Main-thread only (driven by ManualUpdate / Unity event callbacks); no locking.
    /// </summary>
    public sealed class ConversationRequestRegistry
    {
        private const int RetentionCap = 256;

        private readonly Dictionary<int, ConversationRequest> requests = new Dictionary<int, ConversationRequest>();
        private readonly Queue<int> insertionOrder = new Queue<int>();
        private readonly List<int> batchBuffer = new List<int>();

        public event Action<ConversationRequest> StatusChanged;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var pair in requests)
                {
                    if (!pair.Value.IsTerminal)
                        count++;
                }

                return count;
            }
        }

        public int TrackedCount => requests.Count;

        public ConversationRequest Register(int requestId, string characterId, string turnId = "")
        {
            if (requestId <= 0)
                return null;

            var normalized = NormalizeCharacterId(characterId);
            var request = new ConversationRequest(requestId, normalized, NormalizeTurnId(turnId, requestId));
            requests[requestId] = request;
            insertionOrder.Enqueue(requestId);
            Prune();
            return request;
        }

        public bool TryGet(int requestId, out ConversationRequest request)
        {
            return requests.TryGetValue(requestId, out request);
        }

        public string GetCharacterId(int requestId)
        {
            return requests.TryGetValue(requestId, out var request) ? request.CharacterId : string.Empty;
        }

        public string GetTurnId(int requestId)
        {
            return requests.TryGetValue(requestId, out var request) ? request.TurnId : string.Empty;
        }

        public bool IsCanceledOrReplaced(int requestId)
        {
            return requests.TryGetValue(requestId, out var request) && request.IsCanceledOrReplaced;
        }

        /// <summary>
        /// Collects requestIds of all non-terminal (Pending/Playing) requests for the
        /// given character into <paramref name="results"/>.
        /// </summary>
        public void GetNonTerminalRequestIds(string characterId, List<int> results)
        {
            if (results == null)
                return;

            results.Clear();
            var normalized = NormalizeCharacterId(characterId);
            foreach (var pair in requests)
            {
                var request = pair.Value;
                if (request.IsTerminal)
                    continue;
                if (!string.Equals(request.CharacterId, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(pair.Key);
            }
        }

        public bool IsTerminal(int requestId)
        {
            return !requests.TryGetValue(requestId, out var request) || request.IsTerminal;
        }

        public int CountByStatus(RequestStatus status)
        {
            var count = 0;
            foreach (var pair in requests)
            {
                if (pair.Value.Status == status)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Controlled transition. Returns false (and warns) for unknown requestIds,
        /// terminal records, or illegal transitions, without mutating existing state.
        /// </summary>
        public bool TrySetStatus(int requestId, RequestStatus status)
        {
            if (!requests.TryGetValue(requestId, out var request))
            {
                Debug.LogWarning($"[VirtualPartner] RequestRegistry: unknown requestId {requestId} for status {status}.");
                return false;
            }

            if (!CanTransition(request.Status, status))
                return false;

            request.Status = status;
            StatusChanged?.Invoke(request);
            return true;
        }

        /// <summary>
        /// Marks every Pending/Playing request for the character that is older than
        /// (i.e. not equal to) newestRequestId as Replaced.
        /// </summary>
        public void MarkOlderPendingReplaced(string characterId, int newestRequestId)
        {
            var normalized = NormalizeCharacterId(characterId);
            batchBuffer.Clear();
            foreach (var pair in requests)
            {
                var request = pair.Value;
                if (request.RequestId == newestRequestId)
                    continue;
                if (request.IsTerminal)
                    continue;
                if (!string.Equals(request.CharacterId, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                batchBuffer.Add(pair.Key);
            }

            for (var i = 0; i < batchBuffer.Count; i++)
                TrySetStatus(batchBuffer[i], RequestStatus.Replaced);
        }

        /// <summary>
        /// Cancels every non-terminal request for the character.
        /// </summary>
        public void CancelCharacter(string characterId)
        {
            var normalized = NormalizeCharacterId(characterId);
            batchBuffer.Clear();
            foreach (var pair in requests)
            {
                var request = pair.Value;
                if (request.IsTerminal)
                    continue;
                if (!string.Equals(request.CharacterId, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                batchBuffer.Add(pair.Key);
            }

            for (var i = 0; i < batchBuffer.Count; i++)
                TrySetStatus(batchBuffer[i], RequestStatus.Canceled);
        }

        private static bool CanTransition(RequestStatus from, RequestStatus to)
        {
            // Terminal is final.
            if (from >= RequestStatus.Finished)
                return false;

            switch (to)
            {
                case RequestStatus.Playing:
                    return from == RequestStatus.Pending || from == RequestStatus.Playing;
                case RequestStatus.Finished:
                case RequestStatus.Failed:
                case RequestStatus.Canceled:
                case RequestStatus.Replaced:
                    return true; // from is non-terminal (checked above)
                default:
                    return false; // cannot transition back to Pending
            }
        }

        private void Prune()
        {
            var guard = 0;
            while (requests.Count > RetentionCap && insertionOrder.Count > 0 && guard++ < RetentionCap * 4)
            {
                var oldest = insertionOrder.Dequeue();
                if (!requests.TryGetValue(oldest, out var request))
                    continue;

                if (request.IsTerminal)
                    requests.Remove(oldest);
                else
                    insertionOrder.Enqueue(oldest); // keep active requests; revisit later
            }
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? "unknown" : characterId.Trim().ToLowerInvariant();
        }

        private static string NormalizeTurnId(string turnId, int requestId)
        {
            return string.IsNullOrWhiteSpace(turnId) ? $"request:{requestId}" : turnId.Trim();
        }
    }
}
