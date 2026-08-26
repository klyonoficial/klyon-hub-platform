namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Converte respostas de erro do Asaas (tokenização/cobrança com cartão) em mensagens amigáveis em PT-BR.
    /// </summary>
    public static class AsaasCreditCardErrorMapper
    {
        public const string CardValidationFailed =
            "Não foi possível validar este cartão. Os dados podem estar incorretos ou o cartão foi recusado pelo emissor. Confira número, validade e CVV, ou use outro cartão.";

        public const string PaymentDeclined =
            "Pagamento recusado pelo emissor do cartão. Confira os dados ou tente outro cartão.";

        private static readonly HashSet<string> CardErrorCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "invalid_action",
            "invalid_creditCard",
            "invalid_credit_card",
            "credit_card",
            "creditCard"
        };

        public static string ToUserFriendlyMessage(string? rawMessage, string? asaasErrorCode = null)
        {
            var code = (asaasErrorCode ?? "").Trim();
            var message = (rawMessage ?? "").Trim();

            if (IsKnownCardRejection(code, message))
                return CardValidationFailed;

            if (LooksLikeCardError(message))
                return CardValidationFailed;

            if (!string.IsNullOrWhiteSpace(message))
                return message;

            return CardValidationFailed;
        }

        private static bool IsKnownCardRejection(string code, string message)
        {
            if (CardErrorCodes.Contains(code))
                return true;

            if (message.Contains("Transação não autorizada", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Transacao nao autorizada", StringComparison.OrdinalIgnoreCase))
                return true;

            if (message.Contains("cartão de crédito", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Verifique os dados", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool LooksLikeCardError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.Contains("cartão", StringComparison.OrdinalIgnoreCase)
                || message.Contains("cartao", StringComparison.OrdinalIgnoreCase)
                || message.Contains("credit card", StringComparison.OrdinalIgnoreCase)
                || message.Contains("tokeniz", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CVV", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ccv", StringComparison.OrdinalIgnoreCase)
                || message.Contains("não autorizada", StringComparison.OrdinalIgnoreCase)
                || message.Contains("nao autorizada", StringComparison.OrdinalIgnoreCase)
                || message.Contains("recusad", StringComparison.OrdinalIgnoreCase);
        }
    }
}
