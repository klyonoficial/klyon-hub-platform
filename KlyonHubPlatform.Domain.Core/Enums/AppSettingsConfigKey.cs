namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum AppSettingsConfigKey
    {
        BaseUrl
    }
    public static class AppSettingsConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AppSettingsConfigKey, string> Keys = new Dictionary<AppSettingsConfigKey, string>
        {
            { AppSettingsConfigKey.BaseUrl, "AppSettings.BaseUrl" }
        };
        public static string ToConfigKey(this AppSettingsConfigKey key) => Keys[key];
    }
}