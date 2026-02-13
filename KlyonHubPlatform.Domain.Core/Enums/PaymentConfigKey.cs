namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração de pagamento.
    /// </summary>
    public enum PaymentConfigKey
    {
        CreditCardFeePercentage,
        Provider,
        DefaultCurrency
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class PaymentConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<PaymentConfigKey, string> Keys = new Dictionary<PaymentConfigKey, string>
        {
            { PaymentConfigKey.CreditCardFeePercentage, "Payment.CreditCardFeePercentage" },
            { PaymentConfigKey.Provider, "Payment.Provider" },
            { PaymentConfigKey.DefaultCurrency, "Payment.DefaultCurrency" }
        };

        public static string ToConfigKey(this PaymentConfigKey key) => Keys[key];
    }
}
