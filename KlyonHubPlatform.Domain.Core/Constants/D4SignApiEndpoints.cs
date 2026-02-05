namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// URLs base da API D4Sign por ambiente (v1).
    /// Documentação: https://docapi.d4sign.com.br/docs/introdução-a-api
    /// </summary>
    public static class D4SignApiEndpoints
    {
        /// <summary>Produção - assinaturas com validade jurídica.</summary>
        public const string ProductionBaseUrl = "https://secure.d4sign.com.br/api/v1";

        /// <summary>Sandbox - apenas para testes, sem validade jurídica.</summary>
        public const string SandboxBaseUrl = "https://sandbox.d4sign.com.br/api/v1";
    }
}
