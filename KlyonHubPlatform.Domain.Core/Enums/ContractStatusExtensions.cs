namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum ContractStatus.
    /// </summary>
    public static class ContractStatusExtensions
    {
        public static string ToDisplayString(this ContractStatus status) => status switch
        {
            ContractStatus.Active => DisplayStrings.Active,
            ContractStatus.Suspended => DisplayStrings.Suspended,
            ContractStatus.PendingPayment => DisplayStrings.PendingPayment,
            ContractStatus.PendingCancellation => DisplayStrings.PendingCancellation,
            ContractStatus.Cancelled => DisplayStrings.Cancelled,
            ContractStatus.Expired => DisplayStrings.Expired,
            _ => status.ToString()
        };

        /// <summary>Versão em minúsculas para uso em frases (ex: "contrato ativo").</summary>
        public static string ToDisplayStringLower(this ContractStatus status) => status switch
        {
            ContractStatus.Active => DisplayStrings.ActiveLower,
            ContractStatus.Suspended => DisplayStrings.SuspendedLower,
            ContractStatus.PendingPayment => DisplayStrings.PendingPaymentLower,
            ContractStatus.PendingCancellation => DisplayStrings.PendingCancellationLower,
            ContractStatus.Cancelled => DisplayStrings.CancelledLower,
            ContractStatus.Expired => DisplayStrings.ExpiredLower,
            _ => status.ToString().ToLowerInvariant()
        };

        public static class DisplayStrings
        {
            public const string Active = "Ativo";
            public const string Suspended = "Suspenso";
            public const string PendingPayment = "Aguardando Pagamento";
            public const string PendingCancellation = "Cancelamento Pendente";
            public const string Cancelled = "Cancelado";
            public const string Expired = "Expirado";
            public const string ActiveLower = "ativo";
            public const string SuspendedLower = "suspenso";
            public const string PendingPaymentLower = "pendente de pagamento";
            public const string PendingCancellationLower = "cancelamento pendente";
            public const string CancelledLower = "cancelado";
            public const string ExpiredLower = "expirado";
        }
    }
}
