using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
namespace KlyonHubPlatform.Application.Services
{
    public class LocationService : ILocationService
    {
        private readonly ApplicationDbContext _context;
        public LocationService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Locations.AsNoTracking().ToListAsync(cancellationToken);
        }
        public async Task<List<Location>> GetFilteredAsync(string? search = null, string? state = null, string? region = null, ActiveFilter? status = null, int page = 1, int pageSize = 12, CancellationToken cancellationToken = default)
        {
            var query = BuildFilterQuery(search, state, region, status);
            query = query.OrderBy(l => l.State).ThenBy(l => l.City);
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        public async Task<int> GetFilteredCountAsync(string? search = null, string? state = null, string? region = null, ActiveFilter? status = null, CancellationToken cancellationToken = default)
        {
            var query = BuildFilterQuery(search, state, region, status);
            return await query.CountAsync(cancellationToken);
        }
        private IQueryable<Location> BuildFilterQuery(string? search, string? state, string? region, ActiveFilter? status)
        {
            var query = _context.Locations.AsNoTracking();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.City.Contains(search) || l.State.Contains(search));
            }
            if (!string.IsNullOrEmpty(state))
            {
                query = query.Where(l => l.State == state);
            }
            if (!string.IsNullOrEmpty(region))
            {
                query = query.Where(l => l.Region == region);
            }
            if (status is ActiveFilter.Active or ActiveFilter.Inactive)
            {
                bool isActive = status == ActiveFilter.Active;
                query = query.Where(l => l.IsActive == isActive);
            }
            return query;
        }
        public async Task<Location?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Locations.FindAsync(new object[] { id }, cancellationToken);
        }
        public async Task<Location> CreateAsync(string city, string state, string? region, bool isActive, decimal commissionPercentage, CancellationToken cancellationToken = default)
        {
            var existing = await _context.Locations
                .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower() && l.State.ToUpper() == state.ToUpper(), cancellationToken);
            if (existing != null)
            {
                throw new InvalidOperationException($"Já existe uma localidade cadastrada para {city}/{state}.");
            }
            var location = new Location
            {
                City = city,
                State = state.ToUpper(),
                Region = region,
                IsActive = isActive
            };
            _context.Locations.Add(location);
            await _context.SaveChangesAsync(cancellationToken);
            var commission = new LocationCommission
            {
                LocationId = location.Id,
                CommissionPercentage = commissionPercentage,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow
            };
            _context.LocationCommissions.Add(commission);
            await _context.SaveChangesAsync(cancellationToken);
            return location;
        }
        public async Task<Location> UpdateAsync(int id, string city, string state, string? region, bool isActive, CancellationToken cancellationToken = default)
        {
            var location = await _context.Locations.FindAsync(new object[] { id }, cancellationToken);
            if (location == null)
            {
                throw new KeyNotFoundException("Localidade não encontrada.");
            }
            var existing = await _context.Locations
                .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower()
                    && l.State.ToUpper() == state.ToUpper()
                    && l.Id != id, cancellationToken);
            if (existing != null)
            {
                throw new InvalidOperationException($"Já existe uma localidade cadastrada para {city}/{state}.");
            }
            location.City = city;
            location.State = state.ToUpper();
            location.Region = region;
            location.IsActive = isActive;
            await _context.SaveChangesAsync(cancellationToken);
            return location;
        }
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var location = await _context.Locations
                .Include(l => l.LocationCommissions)
                .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
            if (location == null)
            {
                return false;
            }
            var hasAddresses = await _context.CoworkingAddresses
                .AnyAsync(a => a.LocationId == id, cancellationToken);
            if (hasAddresses)
            {
                throw new InvalidOperationException("Não é possível excluir localidade com endereços vinculados. Desative a localidade ao invés de excluí-la.");
            }
            _context.LocationCommissions.RemoveRange(location.LocationCommissions);
            _context.Locations.Remove(location);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        public async Task UpdateCommissionAsync(int locationId, decimal commissionPercentage, CancellationToken cancellationToken = default)
        {
            if (commissionPercentage < 0 || commissionPercentage > 100)
            {
                throw new ArgumentException("A comissão deve estar entre 0% e 100%.");
            }
            var existing = await _context.LocationCommissions
                .Where(lc => lc.LocationId == locationId && lc.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var comm in existing)
            {
                comm.IsActive = false;
                comm.EndDate = DateTime.UtcNow;
            }
            var newCommission = new LocationCommission
            {
                LocationId = locationId,
                CommissionPercentage = commissionPercentage,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow
            };
            _context.LocationCommissions.Add(newCommission);
            await _context.SaveChangesAsync(cancellationToken);
        }
        public async Task<List<string>> GetStatesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Locations
                .AsNoTracking()
                .Select(l => l.State)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<string>> GetRegionsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Locations
                .AsNoTracking()
                .Where(l => l.Region != null)
                .Select(l => l.Region!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<LocationCommission>> GetActiveCommissionsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.LocationCommissions
                .AsNoTracking()
                .Where(lc => lc.IsActive)
                .ToListAsync(cancellationToken);
        }
    }
}