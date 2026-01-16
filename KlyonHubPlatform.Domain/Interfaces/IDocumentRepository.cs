using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(int id);
        Task<List<Document>> GetByContractIdAsync(int contractId);
        Task<Document> CreateAsync(Document document);
        Task<bool> DeleteAsync(int id);
    }
}
