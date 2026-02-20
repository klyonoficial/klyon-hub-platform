namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum AlertsConfigKey
    {
        CommissionPendingDays
    }
    public static class AlertsConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AlertsConfigKey, string> Keys = new Dictionary<AlertsConfigKey, string>
        {
            { AlertsConfigKey.CommissionPendingDays, "Alerts.CommissionPendingDays" }
        };
        public static string ToConfigKey(this AlertsConfigKey key) => Keys[key];
    }
}