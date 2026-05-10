using Dcs.Messaging.Resiliency;

namespace Dcs.Messaging.IntegrationTests.Resiliency;

/// <summary>
/// Behavioural tests for <see cref="PollyRetryPolicy"/>. Lives in the
/// integration suite because it exercises real wall-clock timing rather than a
/// fake clock.
/// </summary>
public sealed class PollyRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsTrueWhenOperationSucceedsImmediately()
    {
        var policy = new PollyRetryPolicy(TestResiliencyOptions());
        var result = await policy.ExecuteAsync(_ => Task.FromResult(true), CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesUntilOperationSucceeds()
    {
        var policy = new PollyRetryPolicy(TestResiliencyOptions());
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(attempts >= 3);
            },
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_AbsorbsThrownExceptionsAndRetries()
    {
        var policy = new PollyRetryPolicy(TestResiliencyOptions());
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("boom");
                }
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalseOnCancellation()
    {
        var policy = new PollyRetryPolicy(TestResiliencyOptions());
        using var cts = new CancellationTokenSource();

        var operationTask = policy.ExecuteAsync(
            async _ =>
            {
                await Task.Delay(50, CancellationToken.None);
                return false;
            },
            cts.Token);

        await Task.Delay(75);
        cts.Cancel();

        var result = await operationTask;
        Assert.False(result);
    }

    private static ResiliencyOptions TestResiliencyOptions() => new ResiliencyOptions
    {
        InitialBackoff = TimeSpan.FromMilliseconds(10),
        MaxBackoff = TimeSpan.FromMilliseconds(50),
        BackoffMultiplier = 2.0,
        UseJitter = false,
    };
}
