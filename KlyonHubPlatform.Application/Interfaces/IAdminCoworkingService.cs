using KlyonHubPlatform.Application.Commands;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço para ações admin sobre Coworking (atualização, etc.). Comandos retornam AdminCommandResult.
    /// </summary>
    public interface IAdminCoworkingService
    {
        Task<AdminCommandResult> UpdateCoworkingAsync(int id, UpdateCoworkingCommand command, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> ApproveCoworkingAsync(int id, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectCoworkingAsync(int id, string rejectionReason, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> ApproveDocumentAsync(int documentId, int userId, CancellationToken cancellationToken = default);
        Task<AdminCommandResult> RejectDocumentAsync(int documentId, string rejectionReason, int userId, CancellationToken cancellationToken = default);
    }
}
