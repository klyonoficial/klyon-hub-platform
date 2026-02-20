using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Document>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Document> CreateAsync(Document document, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}