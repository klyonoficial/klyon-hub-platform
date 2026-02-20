namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Nomes de headers HTTP (request e response). Usar em vez de strings literais em Request.Headers["..."] e Response.Headers[...].
    /// Para headers padrão do .NET pode-se usar também Microsoft.Net.Http.Headers.HeaderNames onde disponível.
    /// </summary>
    public static class HttpHeaderNames
    {
        // ----- Request headers -----
        /// <summary>Header User-Agent (cliente/navegador).</summary>
        public const string UserAgent = "User-Agent";

        /// <summary>Header enviado por AJAX (jQuery, fetch, etc.). Valor esperado: <see cref="XRequestedWithValue"/>.</summary>
        public const string XRequestedWith = "X-Requested-With";

        /// <summary>Valor do header X-Requested-With quando a requisição é AJAX.</summary>
        public const string XRequestedWithValue = "XMLHttpRequest";

        /// <summary>Header de autenticação do webhook Asaas.</summary>
        public const string AsaasAccessToken = "asaas-access-token";

        /// <summary>Token de autenticidade do webhook PagBank (alternativo).</summary>
        public const string PagBankAuthenticityToken = "x-authenticity-token";

        /// <summary>Assinatura do webhook PagBank.</summary>
        public const string PagBankSignature = "X-PagSeguro-Signature";

        // ----- Response headers -----
        /// <summary>Header Cache-Control.</summary>
        public const string CacheControl = "Cache-Control";

        /// <summary>Header Pragma (compatibilidade HTTP/1.0).</summary>
        public const string Pragma = "Pragma";

        /// <summary>Header Expires.</summary>
        public const string Expires = "Expires";

        /// <summary>Header Content-Disposition (ex.: inline; filename="...").</summary>
        public const string ContentDisposition = "Content-Disposition";
    }
}
