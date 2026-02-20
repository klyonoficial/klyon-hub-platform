namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum PaymentStatus
    {
        Pending,
        Processing,
        AwaitingValidation,
        Paid,
        Failed,
        Refunded,
        Cancelled
    }
}