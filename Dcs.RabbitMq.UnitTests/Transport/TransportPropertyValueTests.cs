using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.Transport;

namespace Dcs.RabbitMq.UnitTests.Transport;

public class TransportPropertyValueTests
{
    [Fact]
    public void From_BoolValue_SetsKindAndBoolValue()
    {
        var pv = TransportPropertyValue.From("enabled", true);

        Assert.Equal("enabled", pv.Key);
        Assert.Equal("bool", pv.Kind);
        Assert.Equal("True", pv.BoolValue);
    }

    [Fact]
    public void From_DoubleValue_SetsKindAndDoubleValue()
    {
        var pv = TransportPropertyValue.From("rate", 1.2345);

        Assert.Equal("rate", pv.Key);
        Assert.Equal("double", pv.Kind);
        Assert.Equal("1.2345", pv.DoubleValue);
    }

    [Fact]
    public void From_IntValue_SetsKindAndIntValue()
    {
        var pv = TransportPropertyValue.From("count", 42);

        Assert.Equal("count", pv.Key);
        Assert.Equal("int", pv.Kind);
        Assert.Equal("42", pv.IntValue);
    }

    [Fact]
    public void From_StringValue_SetsKindAndStringValue()
    {
        var pv = TransportPropertyValue.From("name", "hello");

        Assert.Equal("name", pv.Key);
        Assert.Equal("string", pv.Kind);
        Assert.Equal("hello", pv.StringValue);
    }

    [Fact]
    public void ApplyTo_BoolValue_SetsPropertyOnMessageProperties()
    {
        var pv = TransportPropertyValue.From("flag", true);
        var props = new MessageProperties();

        pv.ApplyTo(props);

        Assert.True(props.GetBoolean("flag"));
    }

    [Fact]
    public void ApplyTo_DoubleValue_SetsPropertyOnMessageProperties()
    {
        var pv = TransportPropertyValue.From("rate", 3.14);
        var props = new MessageProperties();

        pv.ApplyTo(props);

        Assert.Equal(3.14, props.GetDouble("rate"), 5);
    }

    [Fact]
    public void ApplyTo_IntValue_SetsPropertyOnMessageProperties()
    {
        var pv = TransportPropertyValue.From("count", 99);
        var props = new MessageProperties();

        pv.ApplyTo(props);

        Assert.Equal(99, props.GetInt32("count"));
    }

    [Fact]
    public void ApplyTo_StringValue_SetsPropertyOnMessageProperties()
    {
        var pv = TransportPropertyValue.From("label", "test");
        var props = new MessageProperties();

        pv.ApplyTo(props);

        Assert.Equal("test", props.GetString("label"));
    }

    [Fact]
    public void RoundTrip_From_ApplyTo_PreservesAllKinds()
    {
        var props = new MessageProperties();
        props.Set("b", true);
        props.Set("d", 2.5);
        props.Set("i", 7);
        props.Set("s", "abc");

        var restored = new MessageProperties();
        foreach (var pair in props.ToDictionary())
        {
            TransportPropertyValue.From(pair.Key, pair.Value).ApplyTo(restored);
        }

        Assert.True(restored.GetBoolean("b"));
        Assert.Equal(2.5, restored.GetDouble("d"), 5);
        Assert.Equal(7, restored.GetInt32("i"));
        Assert.Equal("abc", restored.GetString("s"));
    }
}
