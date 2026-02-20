using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAddressDeclarationService
    {
        Task<AddressDeclaration> GenerateDeclarationAsync(int contractId);
        Task<byte[]?> GetDeclarationPdfAsync(int declarationId);
        Task<List<AddressDeclaration>> GetDeclarationsByContractAsync(int contractId);
        Task InvalidateExpiredDeclarationsAsync();
    }
}