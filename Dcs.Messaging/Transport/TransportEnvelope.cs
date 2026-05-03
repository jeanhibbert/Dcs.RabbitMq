using ProtoBuf;
using System.Collections.Generic;

namespace Dcs.Messaging.Transport
{
    [ProtoContract]
    public sealed class TransportEnvelope
    {
        [ProtoMember(1)]
        public string EndpointAddress { get; set; }

        [ProtoMember(2)]
        public string SenderSessionId { get; set; }

        [ProtoMember(3)]
        public string TargetSessionId { get; set; }

        [ProtoMember(4)]
        public string Tag { get; set; }

        [ProtoMember(5)]
        public List<TransportPropertyValue> Properties { get; set; }

        [ProtoMember(6)]
        public byte[] Payload { get; set; }
    }
}
