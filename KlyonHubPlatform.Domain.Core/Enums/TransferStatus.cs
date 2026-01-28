namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum TransferStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled,
        /// <summary>Aprovado pelo admin; aguardando NF do parceiro para executar repasse.</summary>
        AwaitingInvoice
    }
}
