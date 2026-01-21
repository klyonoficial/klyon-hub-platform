namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status de aprovação do cadastro do coworking.
    /// Em análise (no painel do coworking) = UnderReview = Aguardando aprovação (no admin).
    /// </summary>
    public enum CoworkingApprovalStatus
    {
        /// <summary>Em análise (coworking) / Aguardando aprovação (admin)</summary>
        UnderReview = 0,

        /// <summary>Aguardando ajuste — reprovado, com justificativa ou itens a corrigir</summary>
        AwaitingAdjustment = 1,

        /// <summary>Aprovado — disponível para aluguel no marketplace</summary>
        Approved = 2
    }
}
