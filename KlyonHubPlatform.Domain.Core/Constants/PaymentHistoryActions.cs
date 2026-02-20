namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Ações registradas no histórico de pagamento/contrato (PaymentHistory.Action).
    /// Usar estas constantes em vez de strings literais.
    /// </summary>
    public static class PaymentHistoryActions
    {
        public const string ContractCreated = "ContractCreated";
        public const string ReceiptUploaded = "ReceiptUploaded";
        public const string Cancelled = "Cancelled";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
