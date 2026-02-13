namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração gerais da aplicação (AppSettings).
    /// </summary>
    public enum AppSettingsConfigKey
    {
        BaseUrl
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class AppSettingsConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AppSettingsConfigKey, string> Keys = new Dictionary<AppSettingsConfigKey, string>
        {
            { AppSettingsConfigKey.BaseUrl, "AppSettings.BaseUrl" }
        };

        public static string ToConfigKey(this AppSettingsConfigKey key) => Keys[key];
    }
}
