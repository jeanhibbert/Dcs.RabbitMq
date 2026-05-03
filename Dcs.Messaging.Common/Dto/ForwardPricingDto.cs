using ProtoBuf;

namespace Dcs.RabbitMq.Common.Dto
{
    [ProtoContract]
    public partial class ForwardPricingDto
    {
        public ForwardPricingDto() { }

        private CurrencyPairDto _CurrencyPair;
        [ProtoMember(1)]
        public CurrencyPairDto CurrencyPair
        {
            get { return _CurrencyPair; }
            set { _CurrencyPair = value; }
        }
        private double _BidRate;
        [ProtoMember(2)]
        public double BidRate
        {
            get { return _BidRate; }
            set { _BidRate = value; }
        }
        private double _AskRate;
        [ProtoMember(3)]
        public double AskRate
        {
            get { return _AskRate; }
            set { _AskRate = value; }
        }
        private bool _IsContra;
        [ProtoMember(4)]
        public bool IsContra
        {
            get { return _IsContra; }
            set { _IsContra = value; }
        }
    }
}
