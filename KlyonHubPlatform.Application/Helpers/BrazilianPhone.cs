namespace KlyonHubPlatform.Application.Helpers
{
    public static class BrazilianPhone
    {
        public const string InvalidMessage = "Informe um telefone válido com DDD.";

        public static bool IsValid(string? value)
        {
            var digits = BrazilianTaxId.Digits(value);
            return digits.Length is 10 or 11;
        }

        public static string? FormatForInput(string? value)
        {
            var digits = BrazilianTaxId.Digits(value);
            if (digits.Length == 10)
                return $"({digits[..2]}) {digits[2..6]}-{digits[6..]}";
            if (digits.Length == 11)
                return $"({digits[..2]}) {digits[2..7]}-{digits[7..]}";
            return value;
        }
    }
}
