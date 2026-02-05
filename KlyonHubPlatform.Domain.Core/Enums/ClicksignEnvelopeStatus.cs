namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status do envelope na API Clicksign (JSON API attribute "status").
    /// Documentação: https://developers.clicksign.com/docs
    /// </summary>
    public enum ClicksignEnvelopeStatus
    {
        /// <summary>Rascunho - permite edição. Valor na API: "draft".</summary>
        Draft,

        /// <summary>Em andamento - signatários podem assinar. Valor na API: "running".</summary>
        Running,

        /// <summary>Finalizado. Valor na API: "closed".</summary>
        Closed,

        /// <summary>Cancelado. Valor na API: "canceled".</summary>
        Canceled
    }
}
