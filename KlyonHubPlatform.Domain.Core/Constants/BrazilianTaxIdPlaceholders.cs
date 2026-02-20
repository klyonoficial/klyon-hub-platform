namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Valores de CPF/CNPJ considerados inválidos ou placeholder (não devem ser usados em emissão fiscal).
    /// </summary>
    public static class BrazilianTaxIdPlaceholders
    {
        /// <summary>CPF inválido (11 dígitos repetidos).</summary>
        public const string CpfAllNines = "99999999999";

        /// <summary>CNPJ inválido (14 dígitos repetidos).</summary>
        public const string CnpjAllNines = "99999999999999";

        /// <summary>CPF inválido (11 zeros).</summary>
        public const string CpfAllZeros = "00000000000";

        /// <summary>CNPJ inválido (14 zeros).</summary>
        public const string CnpjAllZeros = "00000000000000";

        /// <summary>Verifica se o documento é um placeholder inválido (CPF ou CNPJ).</summary>
        public static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim();
            return v == CpfAllNines || v == CnpjAllNines || v == CpfAllZeros || v == CnpjAllZeros;
        }
    }
}
