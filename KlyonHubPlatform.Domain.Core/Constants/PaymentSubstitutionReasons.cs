namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Legado: órfãos de troca de meio eram marcados Cancelled com este prefixo em FailureReason.
    /// Novos órfãos são excluídos fisicamente; webhooks ainda ignoram reabertura desses registros antigos.
    /// </summary>
    public static class PaymentSubstitutionReasons
    {
        public const string Prefix = "Substituido por pagamento confirmado";

        public static bool IsSubstitution(string? failureReason) =>
            !string.IsNullOrWhiteSpace(failureReason)
            && failureReason.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
