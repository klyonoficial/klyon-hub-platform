using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractHistoryService
    {
        Task RecordDocumentsUploadedAsync(
            int contractId,
            int? actorUserId,
            IEnumerable<ClientOnboardingDocumentType> documentTypes,
            CancellationToken cancellationToken = default);

        Task RecordProfileUpdatedForClientAsync(
            int clientId,
            int? actorUserId,
            CancellationToken cancellationToken = default);

        Task RecordDocumentsApprovedAsync(
            int contractId,
            int reviewerUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

        Task RecordDocumentsRejectedAsync(
            int contractId,
            int reviewerUserId,
            bool isAdmin,
            string reason,
            CancellationToken cancellationToken = default);

        Task RecordContractGeneratedAsync(
            int contractId,
            string contractNumber,
            string? description = null,
            CancellationToken cancellationToken = default);

        Task<List<ContractHistory>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
    }
}
