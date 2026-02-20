namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Constantes de exibição para valores desconhecidos ou padrão em enums.
    /// </summary>
    public static class EnumDisplayConstants
    {
        public const string Unknown = "Desconhecido";
        /// <summary>Exibição para valor ausente (mesmo que <see cref="DefaultDisplayStrings.EmptyPlaceholder"/>).</summary>
        public const string None = DefaultDisplayStrings.EmptyPlaceholder;
    }
}
