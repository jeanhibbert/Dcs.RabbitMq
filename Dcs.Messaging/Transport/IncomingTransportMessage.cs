using Dcs.Messaging;

namespace Dcs.Messaging.Transport
{
    internal sealed class IncomingTransportMessage
    {
        public IncomingTransportMessage(string endpointAddress, string targetSessionId, IMessage message)
        {
            EndpointAddress = endpointAddress;
            TargetSessionId = targetSessionId;
            Message = message;
        }

        public string EndpointAddress { get; }

        public IMessage Message { get; }

        public string TargetSessionId { get; }
    }
}
