namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Valores booleanos representados como string (ex.: config, query string, form).
    /// Use estas constantes em todo o código em vez de literais "true"/"false".
    /// </summary>
    public static class StringBooleanValue
    {
        /// <summary>Valor string para verdadeiro.</summary>
        public const string True = "true";

        /// <summary>Valor string para falso.</summary>
        public const string False = "false";

        /// <summary>
        /// Indica se o valor é considerado "true" (ignora maiúsculas/minúsculas e espaços).
        /// </summary>
        public static bool IsTrue(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value.Trim(), True, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indica se o valor é considerado "false" (ignora maiúsculas/minúsculas e espaços).
        /// </summary>
        public static bool IsFalse(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value.Trim(), False, System.StringComparison.OrdinalIgnoreCase);
    }
}
