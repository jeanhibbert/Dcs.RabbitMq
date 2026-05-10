using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging
{
    public interface IEndpoint
    {
        /// <summary>
        /// Best-effort send. Implementations should not throw on transport
        /// failures; callers that need to react to dropped messages should use
        /// <see cref="TrySend(IMessage, string)"/> instead.
        /// </summary>
        void Send(IMessage message, string sessionId = null);

        /// <summary>
        /// Allocation-free send that returns a structured outcome rather than
        /// throwing on transport failure. The default implementation calls
        /// <see cref="Send(IMessage, string)"/> and assumes success; transport
        /// implementations override this to surface buffered / dropped /
        /// no-target outcomes.
        /// </summary>
        SendResult TrySend(IMessage message, string sessionId = null)
        {
            Send(message, sessionId);
            return SendResult.Sent;
        }

        IMessageStream MessageStream { get; }
    }
}
