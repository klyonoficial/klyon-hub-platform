using KlyonHubPlatform.Application.DTOs.ContractDocuments;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractDocumentsEnsureService
    {
        Task<ContractDocumentsEnsureResult> EnsureAsync(int contractId, CancellationToken cancellationToken = default);

        Task<ContractDocumentsEnsureResult> GetStatusAsync(int contractId, CancellationToken cancellationToken = default);
    }
}
