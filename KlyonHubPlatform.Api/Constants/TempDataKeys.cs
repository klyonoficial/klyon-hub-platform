namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Chaves usadas em TempData para mensagens e dados temporários.
    /// Para erro: prefira TempData[ErrorMessages.TempDataKey] (mesmo padrão de SuccessMessage.TempDataKey).
    /// Para info: TempData[TempDataKeys.Info], etc.
    /// </summary>
    public static class TempDataKeys
    {
        /// <summary>Chave para mensagens de erro. Prefira usar ErrorMessages.TempDataKey nos controllers.</summary>
        public const string Error = "Error";
        public const string Warning = "Warning";
        public const string Info = "Info";
        public const string ErrorDetails = "ErrorDetails";
        public const string UnsupportedStates = "UnsupportedStates";
        public const string QueryResult = "QueryResult";
        public const string ExecutedQuery = "ExecutedQuery";
        public const string VerifyOperatingHoursResult = "VerifyOperatingHoursResult";
        public const string ActiveTab = "ActiveTab";
        public const string PixCode = "PixCode";
        public const string PaymentId = "PaymentId";
    }
}
