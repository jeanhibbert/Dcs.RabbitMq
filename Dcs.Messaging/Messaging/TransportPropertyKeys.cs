namespace Dcs.Messaging
{
    public static class TransportPropertyKeys
    {
        public const string CorrelationId = "CorrelationId";
        public const string ErrorMessage = "ErrorMessage";
        public const string IsError = "IsError";
        public const string IsPersistent = "IsPersistent";
        public const string MessageKind = "MessageKind";
        public const string TimeToLiveMs = "TimeToLiveMs";
    }
}