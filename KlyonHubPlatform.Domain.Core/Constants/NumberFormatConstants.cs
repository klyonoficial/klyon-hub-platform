namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Formatos numéricos usados na aplicação (exibição de valores, relatórios).
    /// Correspondem aos formatos padrão do .NET: N = número com separadores, F = fixed-point.
    /// </summary>
    public static class NumberFormatConstants
    {
        /// <summary>Número com 2 casas decimais e separadores de milhar (ex.: 1.234,56). Usado para valores em R$.</summary>
        public const string CurrencyTwoDecimals = "N2";

        /// <summary>Número fixo com 1 casa decimal (ex.: 4,5). Usado para notas/médias.</summary>
        public const string OneDecimal = "F1";

        /// <summary>Número fixo com 2 casas decimais (ex.: 10,50). Usado para preços em dados/APIs.</summary>
        public const string TwoDecimals = "F2";
    }
}
