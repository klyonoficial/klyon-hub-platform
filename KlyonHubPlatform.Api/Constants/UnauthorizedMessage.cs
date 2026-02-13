namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Mensagens fixas para respostas Unauthorized (401) da API.
    /// Centraliza textos usados em return Unauthorized("..."), etc.
    /// </summary>
    public static class UnauthorizedMessage
    {
        // ----- Webhook -----
        public const string InvalidSignature = "Assinatura inválida";
    }
}
