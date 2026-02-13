using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum PaymentStatus.
    /// </summary>
    public static class PaymentStatusExtensions
    {
        public static string ToDisplayString(this PaymentStatus status) => status switch
        {
            PaymentStatus.Pending => DisplayStrings.Pending,
            PaymentStatus.Processing => DisplayStrings.Processing,
            PaymentStatus.AwaitingValidation => DisplayStrings.AwaitingValidation,
            PaymentStatus.Paid => DisplayStrings.Paid,
            PaymentStatus.Failed => DisplayStrings.Failed,
            PaymentStatus.Refunded => DisplayStrings.Refunded,
            PaymentStatus.Cancelled => DisplayStrings.Cancelled,
            _ => status.ToString()
        };

        /// <summary>Versão curta (ex: "Aguardando" em vez de "Aguardando Pagamento").</summary>
        public static string ToDisplayStringShort(this PaymentStatus status) => status switch
        {
            PaymentStatus.Pending => DisplayStrings.PendingShort,
            PaymentStatus.Processing => DisplayStrings.Processing,
            PaymentStatus.AwaitingValidation => DisplayStrings.AwaitingValidation,
            PaymentStatus.Paid => DisplayStrings.Paid,
            PaymentStatus.Failed => DisplayStrings.Failed,
            PaymentStatus.Refunded => DisplayStrings.Refunded,
            PaymentStatus.Cancelled => DisplayStrings.Cancelled,
            _ => status.ToString()
        };

        public static class DisplayStrings
        {
            public const string Pending = "Aguardando Pagamento";
            public const string Processing = "Processando";
            public const string AwaitingValidation = "Aguardando Validação";
            public const string Paid = "Pago";
            public const string Failed = "Falhou";
            public const string Refunded = "Reembolsado";
            public const string Cancelled = "Cancelado";
            public const string PendingShort = "Aguardando";
        }
    }
}
