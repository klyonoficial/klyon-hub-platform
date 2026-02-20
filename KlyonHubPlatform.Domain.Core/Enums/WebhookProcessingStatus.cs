namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum WebhookProcessingStatus
    {
        Received = 0,
        Processed = 1,
        Ignored = 2,
        Failed = 3,
        PaymentNotFound = 4,
        InvalidSignature = 5
    }
}