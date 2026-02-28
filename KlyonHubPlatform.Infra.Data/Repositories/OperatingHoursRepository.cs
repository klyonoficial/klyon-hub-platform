using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class OperatingHoursRepository : IOperatingHoursRepository
    {
        private readonly ApplicationDbContext _context;

        public OperatingHoursRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CoworkingOperatingHours?> GetByCoworkingAndDayAsync(int coworkingId, string dayOfWeek, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingOperatingHours
                .FirstOrDefaultAsync(h => h.CoworkingId == coworkingId && h.DayOfWeek == dayOfWeek, cancellationToken);
        }

        public async Task<List<CoworkingOperatingHours>> GetByCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingOperatingHours
                .Where(h => h.CoworkingId == coworkingId)
                .OrderBy(h => h.DayOfWeek)
                .ToListAsync(cancellationToken);
        }
    }
}
