namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum PagBankConfigKey
    {
        UseSandbox,
        AccessToken,
        WebhookSecret
    }
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