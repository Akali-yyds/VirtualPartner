namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Lifecycle status of a single LLM conversation request (keyed by requestId).
    /// Ordering matters: everything at or after <see cref="Finished"/> is terminal.
    /// </summary>
    public enum RequestStatus
    {
        Pending,    // submitted, LLM in flight
        Playing,    // StagePlan playing (informational)
        Finished,   // completed normally
        Failed,     // LLM / validation failure
        Canceled,   // explicitly canceled (e.g. Clear Chat)
        Replaced    // superseded by a newer request for the same character
    }
}
