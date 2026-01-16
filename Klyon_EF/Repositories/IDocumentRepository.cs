using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(int id);
        Task<List<Document>> GetByContractIdAsync(int contractId);
        Task<Document> CreateAsync(Document document);
        Task<bool> DeleteAsync(int id);
    }
}
