using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class CommissionRepository : ICommissionRepository
    {
        private readonly ApplicationDbContext _context;
        public CommissionRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Commission?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Include(c => c.Coworking)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
        public async Task<List<Commission>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Where(c => c.CoworkingId == coworkingId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<Commission>> GetPendingTransfersAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Where(c => c.CoworkingId == coworkingId && !c.IsTransferred)
                .ToListAsync(cancellationToken);
        }
        public async Task<Commission> CreateAsync(Commission commission, CancellationToken cancellationToken = default)
        {
            _context.Commissions.Add(commission);
            await _context.SaveChangesAsync(cancellationToken);
            return commission;
        }
        public async Task<Commission> UpdateAsync(Commission commission, CancellationToken cancellationToken = default)
        {
            _context.Commissions.Update(commission);
            await _context.SaveChangesAsync(cancellationToken);
            return commission;
        }
    }
}