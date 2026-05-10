namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Observable connection state for client-mode sessions. Server-mode
    /// sessions accept many connections concurrently and do not expose a single
    /// state value.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>The session has not yet established a connection.</summary>
        Initializing = 0,

        /// <summary>The session is actively trying to (re)connect.</summary>
        Connecting = 1,

        /// <summary>The session has a healthy connection to the peer.</summary>
        Connected = 2,

        /// <summary>The previous connection was lost; supervisor will reconnect.</summary>
        Disconnected = 3,

        /// <summary>The session has been disposed and will not reconnect.</summary>
        Closed = 4,
    }
}
