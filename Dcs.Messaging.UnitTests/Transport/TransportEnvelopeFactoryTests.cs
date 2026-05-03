using Dcs.Messaging;
using Dcs.Messaging.Transport;
using System.Text;

namespace Dcs.Messaging.UnitTests.Transport;

public class TransportEnvelopeFactoryTests
{
    [Fact]
    public void FromMessage_PreservesEndpointAddress()
    {
        var message = CreateMessage("sender1", "tag1", Encoding.UTF8.GetBytes("hello"));

        var envelope = TransportEnvelopeFactory.FromMessage("test/endpoint", message, null);

        Assert.Equal("test/endpoint", envelope.EndpointAddress);
    }

    [Fact]
    public void FromMessage_PreservesSenderSessionId()
    {
        var message = CreateMessage("sender1", "tag1", new byte[] { 1, 2, 3 });

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, null);

        Assert.Equal("sender1", envelope.SenderSessionId);
    }

    [Fact]
    public void FromMessage_PreservesTag()
    {
        var message = CreateMessage("s", "myTag", Array.Empty<byte>());

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, null);

        Assert.Equal("myTag", envelope.Tag);
    }

    [Fact]
    public void FromMessage_PreservesPayload()
    {
        var payload = new byte[] { 10, 20, 30 };
        var message = CreateMessage("s", "t", payload);

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, null);

        Assert.Equal(payload, envelope.Payload);
    }

    [Fact]
    public void FromMessage_SetsTargetSessionId()
    {
        var message = CreateMessage("s", "t", Array.Empty<byte>());

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, "target123");

        Assert.Equal("target123", envelope.TargetSessionId);
    }

    [Fact]
    public void FromMessage_NullTargetSessionId_SetsEmptyString()
    {
        var message = CreateMessage("s", "t", Array.Empty<byte>());

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, null);

        Assert.Equal(string.Empty, envelope.TargetSessionId);
    }

    [Fact]
    public void FromMessage_PreservesProperties()
    {
        var props = new MessageProperties();
        props.Set("key1", "value1");
        props.Set("key2", 42);
        var message = new Message("s", "t", props, Array.Empty<byte>());

        var envelope = TransportEnvelopeFactory.FromMessage("ep", message, null);

        Assert.NotNull(envelope.Properties);
        Assert.Equal(2, envelope.Properties.Count);
    }

    [Fact]
    public void ToMessage_PreservesAllFields()
    {
        var envelope = new TransportEnvelope
        {
            EndpointAddress = "test/ep",
            SenderSessionId = "sender1",
            TargetSessionId = "target1",
            Tag = "tag1",
            Payload = new byte[] { 1, 2, 3 },
            Properties = new List<TransportPropertyValue>
            {
                TransportPropertyValue.From("name", "value"),
            }
        };

        var message = TransportEnvelopeFactory.ToMessage(envelope);

        Assert.Equal("sender1", message.SenderSessionId);
        Assert.Equal("tag1", message.Tag);
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Payload);
        Assert.Equal("value", message.Properties.GetString("name"));
    }

    [Fact]
    public void RoundTrip_FromMessage_ToMessage_PreservesData()
    {
        var properties = new MessageProperties();
        properties.Set("strProp", "hello");
        properties.Set("intProp", 99);
        properties.Set("boolProp", true);
        properties.Set("dblProp", 1.5);
        var payload = Encoding.UTF8.GetBytes("test payload");
        var original = new Message("session1", "tagA", properties, payload);

        var envelope = TransportEnvelopeFactory.FromMessage("test/endpoint", original, "targetSession");
        var restored = TransportEnvelopeFactory.ToMessage(envelope);

        Assert.Equal("session1", restored.SenderSessionId);
        Assert.Equal("tagA", restored.Tag);
        Assert.Equal(payload, restored.Payload);
        Assert.Equal("hello", restored.Properties.GetString("strProp"));
        Assert.Equal(99, restored.Properties.GetInt32("intProp"));
        Assert.True(restored.Properties.GetBoolean("boolProp"));
        Assert.Equal(1.5, restored.Properties.GetDouble("dblProp"), 5);
    }

    [Fact]
    public void ToMessage_NullProperties_DoesNotThrow()
    {
        var envelope = new TransportEnvelope
        {
            EndpointAddress = "ep",
            SenderSessionId = "s",
            Tag = "t",
            Payload = Array.Empty<byte>(),
            Properties = null
        };

        var message = TransportEnvelopeFactory.ToMessage(envelope);

        Assert.NotNull(message);
        Assert.Equal("s", message.SenderSessionId);
    }

    [Fact]
    public void ToMessage_NullSenderAndTag_DefaultsToEmpty()
    {
        var envelope = new TransportEnvelope
        {
            EndpointAddress = "ep",
            SenderSessionId = null,
            Tag = null,
            Payload = null,
            Properties = null
        };

        var message = TransportEnvelopeFactory.ToMessage(envelope);

        Assert.Equal(string.Empty, message.SenderSessionId);
        Assert.Equal(string.Empty, message.Tag);
        Assert.NotNull(message.Payload);
        Assert.Empty(message.Payload);
    }

    private static Message CreateMessage(string sender, string tag, byte[] payload)
    {
        return new Message(sender, tag, new MessageProperties(), payload);
    }
}
