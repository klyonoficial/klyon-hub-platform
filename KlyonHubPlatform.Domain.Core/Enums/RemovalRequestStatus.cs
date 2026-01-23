namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da solicitação de remoção de serviço do parceiro
    /// </summary>
    public enum RemovalRequestStatus
    {
        /// <summary>Nenhuma solicitação pendente</summary>
        None = 0,

        /// <summary>Solicitação pendente de aprovação do administrador</summary>
        Pending = 1,

        /// <summary>Solicitação aprovada pelo administrador</summary>
        Approved = 2,

        /// <summary>Solicitação rejeitada pelo administrador</summary>
        Rejected = 3
    }
}
