using ProtoBuf;

namespace Dcs.RabbitMq.Common.Dto
{
    [ProtoContract]
    public partial class CurrencyPairDto
    {
        public CurrencyPairDto() { }

        private string _BaseCcy;
        [ProtoMember(1)]
        public string BaseCcy
        {
            get { return _BaseCcy; }
            set { _BaseCcy = value; }
        }
        private string _QuoteCcy;
        [ProtoMember(2)]
        public string QuoteCcy
        {
            get { return _QuoteCcy; }
            set { _QuoteCcy = value; }
        }
        private int _DecimalPlaces;
        [ProtoMember(3)]
        public int DecimalPlaces
        {
            get { return _DecimalPlaces; }
            set { _DecimalPlaces = value; }
        }

    }
}
