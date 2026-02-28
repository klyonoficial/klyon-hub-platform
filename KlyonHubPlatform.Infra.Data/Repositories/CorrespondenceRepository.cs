using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class CorrespondenceRepository : ICorrespondenceRepository
    {
        private readonly ApplicationDbContext _context;

        public CorrespondenceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Correspondence?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Correspondences
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.Client)
                    .ThenInclude(cl => cl!.User)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Photos)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Correspondence> CreateAsync(Correspondence correspondence, CancellationToken cancellationToken = default)
        {
            _context.Correspondences.Add(correspondence);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondence;
        }

        public async Task<Correspondence> UpdateAsync(Correspondence correspondence, CancellationToken cancellationToken = default)
        {
            _context.Correspondences.Update(correspondence);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondence;
        }

        public async Task<List<Correspondence>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Correspondences
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .Include(c => c.Photos)
                .Where(c => c.Contract.ClientId == clientId)
                .OrderByDescending(c => c.ArrivalDate)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Correspondence>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Correspondences
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.Client)
                    .ThenInclude(cl => cl!.User)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Photos)
                .Where(c => c.Contract.CoworkingAddress.CoworkingId == coworkingId)
                .OrderByDescending(c => c.ArrivalDate)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Correspondence>> GetByCoworkingIdFilteredAsync(int coworkingId, DateTime? pickupDateFrom, DateTime? pickupDateTo, CorrespondencePickupStatus? status, CancellationToken cancellationToken = default)
        {
            var query = _context.Correspondences
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.Client)
                    .ThenInclude(cl => cl!.User)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Photos)
                .Where(c => c.Contract.CoworkingAddress.CoworkingId == coworkingId);

            if (status.HasValue)
                query = query.Where(c => c.PickupStatus == status.Value);

            if (pickupDateFrom.HasValue)
                query = query.Where(c => c.PickupScheduledAt != null && c.PickupScheduledAt.Value.Date >= pickupDateFrom.Value.Date);
            if (pickupDateTo.HasValue)
                query = query.Where(c => c.PickupScheduledAt != null && c.PickupScheduledAt.Value.Date <= pickupDateTo.Value.Date);

            return await query
                .OrderByDescending(c => c.PickupScheduledAt)
                .ThenByDescending(c => c.ArrivalDate)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Correspondence>> GetAllAsync(int? coworkingIdFilter, CancellationToken cancellationToken = default)
        {
            var query = _context.Correspondences
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.Client)
                    .ThenInclude(cl => cl!.User)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .Include(c => c.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Photos)
                .AsQueryable();

            if (coworkingIdFilter.HasValue)
                query = query.Where(c => c.Contract.CoworkingAddress.CoworkingId == coworkingIdFilter.Value);

            return await query
                .OrderByDescending(c => c.ArrivalDate)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
