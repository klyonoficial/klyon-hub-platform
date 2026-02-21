namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>Valores de filtro de status de pagamento (nome do enum como string, usado em query/forms).</summary>
    public static class PaymentStatusFilterValues
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string AwaitingValidation = "AwaitingValidation";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Refunded = "Refunded";
    }
}
