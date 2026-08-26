using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAddressDeclarationService
    {
        Task<AddressDeclaration> GenerateDeclarationAsync(int contractId, bool regenerate = false);
        Task<bool> CanGenerateDeclarationAsync(int contractId, CancellationToken cancellationToken = default);
        Task<bool> CanRegenerateDeclarationAsync(int contractId, CancellationToken cancellationToken = default);
        Task<byte[]?> GetDeclarationPdfAsync(int declarationId);
        Task<List<AddressDeclaration>> GetDeclarationsByContractAsync(int contractId);
        Task InvalidateExpiredDeclarationsAsync();
    }
}