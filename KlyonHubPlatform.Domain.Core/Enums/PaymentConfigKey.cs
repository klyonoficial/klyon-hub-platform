namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum PaymentConfigKey
    {
        CreditCardFeePercentage,
        Provider,
        DefaultCurrency,
        /// <summary>Formas de pagamento ocultas (valores separados por vírgula). Ex.: PIX,PixAnnual oculta PIX único e PIX anual. Valores: PixAnnual, PIX, Card1x, CardMonthly.</summary>
        HiddenPaymentOptions,
        /// <summary>Percentual de acréscimo no cartão mensal sobre BasePrice. Ex.: 10 = mensal = BasePrice * 1.10 / 12.</summary>
        CardMonthlySurchargePercent
    }
    public static class PaymentConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<PaymentConfigKey, string> Keys = new Dictionary<PaymentConfigKey, string>
        {
            { PaymentConfigKey.CreditCardFeePercentage, "Payment.CreditCardFeePercentage" },
            { PaymentConfigKey.Provider, "Payment.Provider" },
            { PaymentConfigKey.DefaultCurrency, "Payment.DefaultCurrency" },
            { PaymentConfigKey.HiddenPaymentOptions, "Payment.HiddenPaymentOptions" },
            { PaymentConfigKey.CardMonthlySurchargePercent, "Payment.CardMonthlySurchargePercent" }
        };
        public static string ToConfigKey(this PaymentConfigKey key) => Keys[key];
    }
}