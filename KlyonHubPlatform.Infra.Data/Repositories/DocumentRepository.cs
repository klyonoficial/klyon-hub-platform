using System.Threading;
using System.Threading.Tasks;
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

        public async Task<Document?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .Include(d => d.Contract)
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        }

        public async Task<List<Document>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.GeneratedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Document> CreateAsync(Document document, CancellationToken cancellationToken = default)
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            return document;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var document = await GetByIdAsync(id, cancellationToken);
            if (document == null) return false;

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
