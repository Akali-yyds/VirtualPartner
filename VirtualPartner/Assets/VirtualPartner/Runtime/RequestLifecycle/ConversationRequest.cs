namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Single source-of-truth record for one conversation request's lifecycle.
    /// Owned by <see cref="ConversationRequestRegistry"/>; status is mutated only
    /// through the registry's controlled transitions.
    /// </summary>
    public sealed class ConversationRequest
    {
        public ConversationRequest(int requestId, string characterId, string turnId)
        {
            RequestId = requestId;
            CharacterId = characterId ?? string.Empty;
            TurnId = turnId ?? string.Empty;
            Status = RequestStatus.Pending;
        }

        public int RequestId { get; }
        public string CharacterId { get; }
        public string TurnId { get; }
        public RequestStatus Status { get; internal set; }

        public bool IsTerminal => Status >= RequestStatus.Finished;
        public bool IsCanceledOrReplaced => Status == RequestStatus.Canceled || Status == RequestStatus.Replaced;
    }
}
