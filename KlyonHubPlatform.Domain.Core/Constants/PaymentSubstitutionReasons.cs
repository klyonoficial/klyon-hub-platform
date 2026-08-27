namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Motivos gravados em <c>Payment.FailureReason</c> quando uma cobrança Pending/Failed
    /// é cancelada por ter sido substituída por outro pagamento já confirmado (ex.: PIX órfão após cartão Pago).
    /// Webhooks não devem reabrir pagamentos com este prefixo.
    /// </summary>
    public static class PaymentSubstitutionReasons
    {
        public const string Prefix = "Substituido por pagamento confirmado";

        public static string ForPaidSibling(int paidPaymentId) =>
            $"{Prefix} (payment {paidPaymentId})";

        public static bool IsSubstitution(string? failureReason) =>
            !string.IsNullOrWhiteSpace(failureReason)
            && failureReason.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
