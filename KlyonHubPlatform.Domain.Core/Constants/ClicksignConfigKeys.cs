namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves de configuração do sistema para integração Clicksign (assinatura eletrônica).
    /// Documentação: https://developers.clicksign.com/docs/primeiros-passos
    /// </summary>
    public static class ClicksignConfigKeys
    {
        /// <summary>Access Token da API (Configurações > API na conta Clicksign). Obrigatório.</summary>
        public const string AccessToken = "Clicksign.AccessToken";

        /// <summary>Usar ambiente sandbox (true) ou produção (false). Para testes use true.</summary>
        public const string UseSandbox = "Clicksign.UseSandbox";
    }
}
