namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum TransferStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled,
        AwaitingInvoice
    }
}