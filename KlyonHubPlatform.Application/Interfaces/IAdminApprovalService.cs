using KlyonHubPlatform.Application.Commands;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço que executa comandos de aprovação/rejeição admin (capacidade, max contracts, preços no cartão).
    /// Retorna resultado para o controller aplicar TempData e redirect sem impactar o frontend.
    /// </summary>
    public interface IAdminApprovalService
    {
        Task<AdminCommandResult> ApproveCapacityChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectCapacityChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> ApproveMaxContractsChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectMaxContractsChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> ApproveCardPriceChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectCardPriceChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default);
    }
}
