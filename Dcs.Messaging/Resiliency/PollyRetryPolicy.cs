using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Default <see cref="IRetryPolicy"/> backed by a Polly v8 resilience
    /// pipeline. Retries forever (until cancellation) using exponential backoff
    /// with optional jitter. Exceptions thrown by the operation are absorbed and
    /// treated as failed attempts so callers do not pay an exception-throw cost
    /// across the API boundary.
    /// </summary>
    public sealed class PollyRetryPolicy : IRetryPolicy
    {
        private readonly ResiliencePipeline _pipeline;

        public PollyRetryPolicy(ResiliencyOptions options)
        {
            if (options == null)
            {
                options = ResiliencyOptions.Default;
            }

            _pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = int.MaxValue,
                    Delay = options.InitialBackoff,
                    MaxDelay = options.MaxBackoff,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = options.UseJitter,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
                })
                .Build();
        }

        public async Task<bool> ExecuteAsync(
            Func<CancellationToken, Task<bool>> operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                return false;
            }

            try
            {
                var succeeded = false;
                await _pipeline.ExecuteAsync(
                    async token =>
                    {
                        var result = await operation(token).ConfigureAwait(false);
                        if (!result)
                        {
                            throw new TransientOperationFailureException();
                        }

                        succeeded = true;
                    },
                    cancellationToken).ConfigureAwait(false);
                return succeeded;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private sealed class TransientOperationFailureException : Exception
        {
        }
    }
}
