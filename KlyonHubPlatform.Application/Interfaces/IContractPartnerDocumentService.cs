using KlyonHubPlatform.Application.DTOs.Contracts;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractPartnerDocumentService
    {
        Task<IReadOnlyList<ContractPartnerDocumentDto>> ListAvailableForContractAsync(
            int contractId,
            int clientId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

        Task<ContractPartnerDocumentFileDto?> GetFileForContractAsync(
            int contractId,
            int documentId,
            int clientId,
            bool isAdmin,
            CancellationToken cancellationToken = default);
    }
}
