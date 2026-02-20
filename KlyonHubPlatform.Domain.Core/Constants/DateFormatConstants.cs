namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Formatos de data usados na aplicação (inputs HTML type="date", APIs, relatórios e exibição).
    /// </summary>
    public static class DateFormatConstants
    {
        /// <summary>Formato ISO para data (yyyy-MM-dd). Usado em inputs HTML date e em APIs.</summary>
        public const string DateOnly = "yyyy-MM-dd";

        /// <summary>Formato para data e hora (yyyy-MM-dd HH:mm).</summary>
        public const string DateAndTime = "yyyy-MM-dd HH:mm";

        /// <summary>Formato brasileiro para exibição de data (dd/MM/yyyy).</summary>
        public const string DisplayDateOnly = "dd/MM/yyyy";

        /// <summary>Formato brasileiro para exibição de data e hora (dd/MM/yyyy HH:mm).</summary>
        public const string DisplayDateAndTime = "dd/MM/yyyy HH:mm";
    }
}
