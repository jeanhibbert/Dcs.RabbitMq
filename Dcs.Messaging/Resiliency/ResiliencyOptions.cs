using System;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Tunable resiliency parameters used by the default retry policy and
    /// connection supervisor. All values have sensible defaults so callers can
    /// rely on <see cref="Default"/> without specifying anything.
    /// </summary>
    public sealed class ResiliencyOptions
    {
        public static readonly ResiliencyOptions Default = new ResiliencyOptions();

        /// <summary>
        /// Initial delay between retry attempts. Defaults to 250 ms.
        /// </summary>
        public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Maximum delay between retry attempts. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Multiplier applied between retry attempts to grow the delay. Defaults to 2.0.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// When true (default) the policy adds a small random jitter to each
        /// delay to avoid synchronized reconnect storms.
        /// </summary>
        public bool UseJitter { get; set; } = true;

        /// <summary>
        /// Maximum capacity of the per-connection outbound buffer. Producers that
        /// exceed this limit get back <c>SendStatus.Dropped</c> rather than an
        /// exception. Defaults to 8192.
        /// </summary>
        public int OutboundQueueCapacity { get; set; } = 8192;

        /// <summary>
        /// When the outbound queue is full, this controls whether to drop the
        /// oldest message (true) to make room or refuse the new message (false).
        /// Defaults to false (refuse new).
        /// </summary>
        public bool DropOldestWhenQueueFull { get; set; } = false;
    }
}
