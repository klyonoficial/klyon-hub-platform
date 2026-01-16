using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Document?> GetByIdAsync(int id)
        {
            return await _context.Documents
                .Include(d => d.Contract)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Document>> GetByContractIdAsync(int contractId)
        {
            return await _context.Documents
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.GeneratedAt)
                .ToListAsync();
        }

        public async Task<Document> CreateAsync(Document document)
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var document = await GetByIdAsync(id);
            if (document == null) return false;

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
