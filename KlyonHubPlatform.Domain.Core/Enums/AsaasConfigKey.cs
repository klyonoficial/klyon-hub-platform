namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum AsaasConfigKey
    {
        ApiKey,
        UseSandbox,
        WebhookToken
    }
    public static class AsaasConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AsaasConfigKey, string> Keys = new Dictionary<AsaasConfigKey, string>
        {
            { AsaasConfigKey.ApiKey, "Asaas.ApiKey" },
            { AsaasConfigKey.UseSandbox, "Asaas.UseSandbox" },
            { AsaasConfigKey.WebhookToken, "Asaas.WebhookToken" }
        };
        public static string ToConfigKey(this AsaasConfigKey key) => Keys[key];
    }
    public static class AsaasErrorMessages
    {
        public static string ApiKeyNotConfigured => $"API Key Asaas não configurada. Configure em Configurações do Sistema > {AsaasConfigKey.ApiKey.ToConfigKey()}";
        public static string ApiKeyNotConfiguredShort => $"API Key Asaas não configurada. Configure {AsaasConfigKey.ApiKey.ToConfigKey()} nas Configurações do Sistema.";
    }
}