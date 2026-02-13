namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum PaymentMethod.
    /// </summary>
    public static class PaymentMethodExtensions
    {
        public static string ToDisplayString(this PaymentMethod method) => method switch
        {
            PaymentMethod.PIX => DisplayStrings.Pix,
            PaymentMethod.CreditCard => DisplayStrings.CreditCard,
            PaymentMethod.BankTransfer => DisplayStrings.BankTransfer,
            _ => method.ToString()
        };

        public static class DisplayStrings
        {
            public const string Pix = "PIX";
            public const string CreditCard = "Cartão";
            public const string BankTransfer = "Boleto";
        }
    }
}
