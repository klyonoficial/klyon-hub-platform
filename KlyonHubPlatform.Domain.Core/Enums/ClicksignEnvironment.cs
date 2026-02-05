namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Ambiente da API Clicksign. Sandbox não possui validade jurídica; use para testes.
    /// </summary>
    public enum ClicksignEnvironment
    {
        /// <summary>Ambiente de desenvolvimento (sem validade jurídica). Limite: 20 req/10s.</summary>
        Sandbox = 0,

        /// <summary>Ambiente de produção (validade jurídica). Limite: 50 req/10s.</summary>
        Production = 1
    }
}
