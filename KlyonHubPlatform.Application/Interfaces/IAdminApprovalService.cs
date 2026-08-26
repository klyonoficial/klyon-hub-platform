using KlyonHubPlatform.Application.Commands;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço que executa comandos de aprovação/rejeição admin (preços no cartão).
    /// Retorna resultado para o controller aplicar TempData e redirect sem impactar o frontend.
    /// </summary>
    public interface IAdminApprovalService
    {
        Task<AdminCommandResult> ApproveCardPriceChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectCardPriceChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default);
    }
}
