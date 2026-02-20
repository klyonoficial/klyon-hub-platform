namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum PlatformConfigKey
    {
        CommissionPercentage
    }
    public static class PlatformConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<PlatformConfigKey, string> Keys = new Dictionary<PlatformConfigKey, string>
        {
            { PlatformConfigKey.CommissionPercentage, "Platform.CommissionPercentage" }
        };
        public static string ToConfigKey(this PlatformConfigKey key) => Keys[key];
    }
}