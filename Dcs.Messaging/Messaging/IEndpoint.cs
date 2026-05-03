namespace Dcs.Messaging
{
    public interface IEndpoint
    {
        void Send(IMessage message, string sessionId = null);
        IMessageStream MessageStream { get; }
    }
}