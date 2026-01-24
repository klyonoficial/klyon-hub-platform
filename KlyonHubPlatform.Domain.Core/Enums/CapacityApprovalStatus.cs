namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status de aprovação de alteração de capacidade fiscal.
    /// </summary>
    public enum CapacityApprovalStatus
    {
        /// <summary>Nenhuma solicitação pendente</summary>
        None = 0,

        /// <summary>Aguardando aprovação administrativa</summary>
        Pending = 1,

        /// <summary>Aprovado pela plataforma</summary>
        Approved = 2,

        /// <summary>Reprovado pela plataforma</summary>
        Rejected = 3
    }
}
