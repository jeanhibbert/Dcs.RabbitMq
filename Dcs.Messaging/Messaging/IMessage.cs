namespace Dcs.Messaging
{
    public interface IMessage
    {
        string SenderSessionId { get; }
        string Tag { get; }
        IMessageProperties Properties { get; }
        byte[] Payload { get; }
    }
}
