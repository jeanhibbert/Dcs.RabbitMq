namespace Dcs.RabbitMq.Messaging.Serialization
{
    public interface IBinarySerializer
    {
        byte[] Serialize<T>(T payload);
        T Deserialize<T>(byte[] data);
    }
}
