using ProtoBuf;
using System.IO;

namespace Dcs.Messaging.Serialization
{
    public sealed class ProtobufNetBinarySerializer : IBinarySerializer
    {
        public T Deserialize<T>(byte[] data)
        {
            using var stream = new MemoryStream(data, writable: false);
            return Serializer.Deserialize<T>(stream);
        }

        public byte[] Serialize<T>(T payload)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, payload);
            return stream.ToArray();
        }
    }
}