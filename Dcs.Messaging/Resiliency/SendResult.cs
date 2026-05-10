namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Outcome of a send attempt. <see cref="readonly struct"/> so callers don't
    /// allocate when checking the result on the hot path.
    /// </summary>
    public readonly struct SendResult
    {
        public SendResult(SendStatus status)
        {
            Status = status;
        }

        public SendStatus Status { get; }

        public bool IsSuccess => Status == SendStatus.Sent || Status == SendStatus.Buffered;

        public static SendResult Sent => new SendResult(SendStatus.Sent);

        public static SendResult Buffered => new SendResult(SendStatus.Buffered);

        public static SendResult NoSuchTarget => new SendResult(SendStatus.NoSuchTarget);

        public static SendResult QueueFull => new SendResult(SendStatus.QueueFull);

        public static SendResult ShuttingDown => new SendResult(SendStatus.ShuttingDown);
    }
}
