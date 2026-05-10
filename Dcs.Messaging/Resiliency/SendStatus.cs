namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Outcome of a single send attempt. Allows callers to react without
    /// catching exceptions in the hot path.
    /// </summary>
    public enum SendStatus
    {
        /// <summary>The message was written directly to the wire.</summary>
        Sent = 0,

        /// <summary>The message was successfully placed in the outbound buffer.</summary>
        Buffered = 1,

        /// <summary>No connection matched the requested target session ID.</summary>
        NoSuchTarget = 2,

        /// <summary>The outbound buffer was full and the message was rejected.</summary>
        QueueFull = 3,

        /// <summary>The session is being shut down and refused the message.</summary>
        ShuttingDown = 4,
    }
}
