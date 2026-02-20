namespace KlyonHubPlatform.Domain.Core.Enums
{
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