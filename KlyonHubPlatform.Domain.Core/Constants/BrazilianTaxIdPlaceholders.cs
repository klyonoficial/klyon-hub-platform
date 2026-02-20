namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class BrazilianTaxIdPlaceholders
    {
        public const string CpfAllNines = "99999999999";
        public const string CnpjAllNines = "99999999999999";
        public const string CpfAllZeros = "00000000000";
        public const string CnpjAllZeros = "00000000000000";
        public static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim();
            return v == CpfAllNines || v == CnpjAllNines || v == CpfAllZeros || v == CnpjAllZeros;
        }
    }
}