using Dcs.RabbitMq.Messaging.Transport;
using ProtoBuf;

namespace Dcs.RabbitMq.UnitTests.Transport;

public class TransportEnvelopeSerializationTests
{
    [Fact]
    public void Serialize_Deserialize_PreservesAllFields()
    {
        var original = new TransportEnvelope
        {
            EndpointAddress = "test/address",
            SenderSessionId = "sender-abc",
            TargetSessionId = "target-xyz",
            Tag = "myTag",
            Payload = new byte[] { 10, 20, 30, 40 },
            Properties = new List<TransportPropertyValue>
            {
                TransportPropertyValue.From("strKey", "strVal"),
                TransportPropertyValue.From("intKey", 42),
                TransportPropertyValue.From("boolKey", true),
                TransportPropertyValue.From("dblKey", 3.14),
            }
        };

        var restored = RoundTrip(original);

        Assert.Equal(original.EndpointAddress, restored.EndpointAddress);
        Assert.Equal(original.SenderSessionId, restored.SenderSessionId);
        Assert.Equal(original.TargetSessionId, restored.TargetSessionId);
        Assert.Equal(original.Tag, restored.Tag);
        Assert.Equal(original.Payload, restored.Payload);
        Assert.NotNull(restored.Properties);
        Assert.Equal(original.Properties.Count, restored.Properties.Count);
    }

    [Fact]
    public void Serialize_Deserialize_EmptyEnvelope()
    {
        var original = new TransportEnvelope();

        var restored = RoundTrip(original);

        Assert.Null(restored.EndpointAddress);
        Assert.Null(restored.SenderSessionId);
        Assert.Null(restored.TargetSessionId);
        Assert.Null(restored.Tag);
        Assert.Null(restored.Payload);
        Assert.Null(restored.Properties);
    }

    [Fact]
    public void Serialize_Deserialize_PropertyValues_PreserveKindsAndValues()
    {
        var original = new TransportEnvelope
        {
            EndpointAddress = "ep",
            Properties = new List<TransportPropertyValue>
            {
                TransportPropertyValue.From("b", false),
                TransportPropertyValue.From("d", 99.99),
                TransportPropertyValue.From("i", 7),
                TransportPropertyValue.From("s", "hello"),
            }
        };

        var restored = RoundTrip(original);

        Assert.Equal(4, restored.Properties.Count);
        for (int i = 0; i < original.Properties.Count; i++)
        {
            Assert.Equal(original.Properties[i].Key, restored.Properties[i].Key);
            Assert.Equal(original.Properties[i].Kind, restored.Properties[i].Kind);
            Assert.Equal(original.Properties[i].BoolValue, restored.Properties[i].BoolValue);
            Assert.Equal(original.Properties[i].DoubleValue, restored.Properties[i].DoubleValue);
            Assert.Equal(original.Properties[i].IntValue, restored.Properties[i].IntValue);
            Assert.Equal(original.Properties[i].StringValue, restored.Properties[i].StringValue);
        }
    }

    [Fact]
    public void Serialize_Deserialize_LargePayload()
    {
        var payload = new byte[8192];
        Random.Shared.NextBytes(payload);
        var original = new TransportEnvelope
        {
            EndpointAddress = "ep",
            Payload = payload
        };

        var restored = RoundTrip(original);

        Assert.Equal(payload, restored.Payload);
    }

    private static TransportEnvelope RoundTrip(TransportEnvelope envelope)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, envelope);
        stream.Position = 0;
        return Serializer.Deserialize<TransportEnvelope>(stream);
    }
}
