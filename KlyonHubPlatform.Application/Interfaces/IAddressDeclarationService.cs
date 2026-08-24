using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAddressDeclarationService
    {
        Task<AddressDeclaration> GenerateDeclarationAsync(int contractId);
        Task<bool> CanGenerateDeclarationAsync(int contractId, CancellationToken cancellationToken = default);
        Task<byte[]?> GetDeclarationPdfAsync(int declarationId);
        Task<List<AddressDeclaration>> GetDeclarationsByContractAsync(int contractId);
        Task InvalidateExpiredDeclarationsAsync();
    }
}