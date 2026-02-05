namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// URLs base da API Clicksign (v3 Envelope) por ambiente.
    /// Documentação: https://developers.clicksign.com/docs
    /// Rate limit: https://developers.clicksign.com/docs/limite-de-requisicoes
    /// </summary>
    public static class ClicksignApiEndpoints
    {
        /// <summary>Produção - assinaturas com validade jurídica. Limite: 50 req/10s.</summary>
        public const string ProductionBaseUrl = "https://app.clicksign.com/api/v3";

        /// <summary>Sandbox - apenas para testes. Limite: 20 req/10s.</summary>
        public const string SandboxBaseUrl = "https://sandbox.clicksign.com/api/v3";

        /// <summary>Path relativo para criação de envelopes.</summary>
        public const string Envelopes = "envelopes";

        /// <summary>Path relativo para documentos de um envelope (uso: envelopes/{id}/documents).</summary>
        public const string Documents = "documents";

        /// <summary>Path relativo para signatários de um envelope (uso: envelopes/{id}/signers).</summary>
        public const string Signers = "signers";

        /// <summary>Path relativo para requisitos de qualificação (uso: documents/{id}/requirements/qualification).</summary>
        public const string RequirementsQualification = "requirements/qualification";

        /// <summary>Path relativo para requisitos de autenticação (uso: documents/{id}/requirements/authentication).</summary>
        public const string RequirementsAuthentication = "requirements/authentication";

        /// <summary>Path relativo para notificação de signatário (uso: signers/{id}/notify).</summary>
        public const string Notify = "notify";
    }
}
