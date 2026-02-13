namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração do PagBank (PagSeguro).
    /// </summary>
    public enum PagBankConfigKey
    {
        UseSandbox,
        AccessToken,
        WebhookSecret
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class PagBankConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<PagBankConfigKey, string> Keys = new Dictionary<PagBankConfigKey, string>
        {
            { PagBankConfigKey.UseSandbox, "PagBank.UseSandbox" },
            { PagBankConfigKey.AccessToken, "PagBank.AccessToken" },
            { PagBankConfigKey.WebhookSecret, "PagBank.WebhookSecret" }
        };

        public static string ToConfigKey(this PagBankConfigKey key) => Keys[key];
    }
}
