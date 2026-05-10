using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Default <see cref="IClock"/> backed by <see cref="DateTimeOffset.UtcNow"/>
    /// and <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay <= TimeSpan.Zero)
            {
                return Task.CompletedTask;
            }

            return Task.Delay(delay, cancellationToken);
        }
    }
}
