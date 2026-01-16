using Microsoft.EntityFrameworkCore;
using KlyonEF.Data;
using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public class CommissionRepository : ICommissionRepository
    {
        private readonly ApplicationDbContext _context;

        public CommissionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Commission?> GetByIdAsync(int id)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Include(c => c.Coworking)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Commission>> GetByCoworkingIdAsync(int coworkingId)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Where(c => c.CoworkingId == coworkingId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Commission>> GetPendingTransfersAsync(int coworkingId)
        {
            return await _context.Commissions
                .Include(c => c.Payment)
                .Where(c => c.CoworkingId == coworkingId && !c.IsTransferred)
                .ToListAsync();
        }

        public async Task<Commission> CreateAsync(Commission commission)
        {
            _context.Commissions.Add(commission);
            await _context.SaveChangesAsync();
            return commission;
        }

        public async Task<Commission> UpdateAsync(Commission commission)
        {
            _context.Commissions.Update(commission);
            await _context.SaveChangesAsync();
            return commission;
        }
    }
}
