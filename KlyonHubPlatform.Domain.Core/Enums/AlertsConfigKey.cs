namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração de alertas.
    /// </summary>
    public enum AlertsConfigKey
    {
        CommissionPendingDays
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class AlertsConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AlertsConfigKey, string> Keys = new Dictionary<AlertsConfigKey, string>
        {
            { AlertsConfigKey.CommissionPendingDays, "Alerts.CommissionPendingDays" }
        };

        public static string ToConfigKey(this AlertsConfigKey key) => Keys[key];
    }
}
