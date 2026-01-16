using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class LocationService : ILocationService
    {
        private readonly ApplicationDbContext _context;

        public LocationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Location>> GetAllAsync()
        {
            return await _context.Locations.ToListAsync();
        }

        public async Task<List<Location>> GetFilteredAsync(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 12)
        {
            var query = BuildFilterQuery(search, state, region, status);
            query = query.OrderBy(l => l.State).ThenBy(l => l.City);
            
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetFilteredCountAsync(string? search = null, string? state = null, string? region = null, string? status = null)
        {
            var query = BuildFilterQuery(search, state, region, status);
            return await query.CountAsync();
        }

        private IQueryable<Location> BuildFilterQuery(string? search, string? state, string? region, string? status)
        {
            var query = _context.Locations.AsQueryable();

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

            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(l => l.IsActive == isActive);
            }

            return query;
        }

        public async Task<Location?> GetByIdAsync(int id)
        {
            return await _context.Locations.FindAsync(id);
        }

        public async Task<Location> CreateAsync(string city, string state, string? region, bool isActive, decimal commissionPercentage)
        {
            // Verificar se já existe
            var existing = await _context.Locations
                .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower() && l.State.ToUpper() == state.ToUpper());

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
            await _context.SaveChangesAsync();

            // Criar comissão inicial
            var commission = new LocationCommission
            {
                LocationId = location.Id,
                CommissionPercentage = commissionPercentage,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow
            };

            _context.LocationCommissions.Add(commission);
            await _context.SaveChangesAsync();

            return location;
        }

        public async Task<Location> UpdateAsync(int id, string city, string state, string? region, bool isActive)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                throw new KeyNotFoundException("Localidade não encontrada.");
            }

            // Verificar se já existe outra localidade com mesma cidade e estado
            var existing = await _context.Locations
                .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower() 
                    && l.State.ToUpper() == state.ToUpper() 
                    && l.Id != id);

            if (existing != null)
            {
                throw new InvalidOperationException($"Já existe uma localidade cadastrada para {city}/{state}.");
            }

            location.City = city;
            location.State = state.ToUpper();
            location.Region = region;
            location.IsActive = isActive;

            await _context.SaveChangesAsync();
            return location;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var location = await _context.Locations
                .Include(l => l.LocationCommissions)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (location == null)
            {
                return false;
            }

            // Verificar se há endereços vinculados
            var hasAddresses = await _context.CoworkingAddresses
                .AnyAsync(a => a.LocationId == id);

            if (hasAddresses)
            {
                throw new InvalidOperationException("Não é possível excluir localidade com endereços vinculados. Desative a localidade ao invés de excluí-la.");
            }

            // Remover comissões
            _context.LocationCommissions.RemoveRange(location.LocationCommissions);
            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task UpdateCommissionAsync(int locationId, decimal commissionPercentage)
        {
            if (commissionPercentage < 0 || commissionPercentage > 100)
            {
                throw new ArgumentException("A comissão deve estar entre 0% e 100%.");
            }

            // Desativar comissões ativas existentes
            var existing = await _context.LocationCommissions
                .Where(lc => lc.LocationId == locationId && lc.IsActive)
                .ToListAsync();

            foreach (var comm in existing)
            {
                comm.IsActive = false;
                comm.EndDate = DateTime.UtcNow;
            }

            // Criar nova comissão
            var newCommission = new LocationCommission
            {
                LocationId = locationId,
                CommissionPercentage = commissionPercentage,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow
            };

            _context.LocationCommissions.Add(newCommission);
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetStatesAsync()
        {
            return await _context.Locations
                .Select(l => l.State)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public async Task<List<string>> GetRegionsAsync()
        {
            return await _context.Locations
                .Where(l => l.Region != null)
                .Select(l => l.Region!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
        }

        public async Task<List<LocationCommission>> GetActiveCommissionsAsync()
        {
            return await _context.LocationCommissions
                .Where(lc => lc.IsActive)
                .ToListAsync();
        }
    }
}
