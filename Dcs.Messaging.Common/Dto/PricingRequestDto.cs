using ProtoBuf;

namespace Dcs.Messaging.Common.Dto
{
    [ProtoContract]
    public sealed class PricingRequestDto
    {
        [ProtoMember(1)]
        public CurrencyPairDto CurrencyPair { get; set; }

        [ProtoMember(2)]
        public string RequestedBy { get; set; }

        [ProtoMember(3)]
        public double SpotRate { get; set; }

        [ProtoMember(4)]
        public string Tenor { get; set; }
    }
}