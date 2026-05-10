using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.IntegrationTests.Infrastructure;

/// <summary>
/// Resiliency knobs tuned for fast tests: short backoff so reconnect happens
/// inside test timeouts, and small queue capacity so queue-full tests don't
/// need huge volumes.
/// </summary>
internal static class TestResiliencyOptions
{
    public static ResiliencyOptions FastReconnect() => new ResiliencyOptions
    {
        InitialBackoff = TimeSpan.FromMilliseconds(50),
        MaxBackoff = TimeSpan.FromMilliseconds(500),
        BackoffMultiplier = 2.0,
        UseJitter = false,
        OutboundQueueCapacity = 256,
    };

    public static ResiliencyOptions TinyQueue(int capacity) => new ResiliencyOptions
    {
        InitialBackoff = TimeSpan.FromMilliseconds(50),
        MaxBackoff = TimeSpan.FromMilliseconds(500),
        BackoffMultiplier = 2.0,
        UseJitter = false,
        OutboundQueueCapacity = capacity,
    };
}
