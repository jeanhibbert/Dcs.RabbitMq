namespace Dcs.RabbitMq.Messaging.Messaging
{
    public interface IEndpoint
    {
        void Send(IMessage message, string sessionId = null);
        IMessageStream MessageStream { get; }
    }
}