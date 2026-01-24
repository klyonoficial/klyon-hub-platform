namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status de análise do documento obrigatório do coworking.
    /// </summary>
    public enum CoworkingDocumentStatus
    {
        /// <summary>Em análise — aguardando revisão do admin</summary>
        Pending = 0,

        /// <summary>Aprovado — documento aceito</summary>
        Approved = 1,

        /// <summary>Rejeitado — documento recusado (ver RejectionReason)</summary>
        Rejected = 2
    }
}
