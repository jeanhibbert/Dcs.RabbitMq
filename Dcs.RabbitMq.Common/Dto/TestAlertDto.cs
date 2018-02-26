using ProtoBuf;

namespace Dcs.RabbitMq.Common.Dto
{
    [ProtoContract]
    public class TestAlertDto
    {
        [ProtoMember(1)]
        public int MessageId { get; set; }
        [ProtoMember(2)]
        public string ServerName { get; set; }
        [ProtoMember(3)]
        public string Message { get; set; }
    }
}
