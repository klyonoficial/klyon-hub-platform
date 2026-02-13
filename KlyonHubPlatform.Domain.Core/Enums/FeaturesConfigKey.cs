namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração de funcionalidades (features).
    /// </summary>
    public enum FeaturesConfigKey
    {
        EnableCoworkingMenus
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class FeaturesConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<FeaturesConfigKey, string> Keys = new Dictionary<FeaturesConfigKey, string>
        {
            { FeaturesConfigKey.EnableCoworkingMenus, "Features.EnableCoworkingMenus" }
        };

        public static string ToConfigKey(this FeaturesConfigKey key) => Keys[key];
    }
}
