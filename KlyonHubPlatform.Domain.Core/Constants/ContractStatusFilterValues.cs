namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>Valores de filtro de status de contrato (nome do enum como string).</summary>
    public static class ContractStatusFilterValues
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string PendingPayment = "PendingPayment";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
        public const string PendingCancellation = "PendingCancellation";
    }
}
