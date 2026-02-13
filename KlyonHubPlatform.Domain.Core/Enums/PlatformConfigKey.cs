namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração da plataforma.
    /// </summary>
    public enum PlatformConfigKey
    {
        CommissionPercentage
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class PlatformConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<PlatformConfigKey, string> Keys = new Dictionary<PlatformConfigKey, string>
        {
            { PlatformConfigKey.CommissionPercentage, "Platform.CommissionPercentage" }
        };

        public static string ToConfigKey(this PlatformConfigKey key) => Keys[key];
    }
}
