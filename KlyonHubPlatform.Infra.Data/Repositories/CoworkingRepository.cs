using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class CoworkingRepository : ICoworkingRepository
    {
        private readonly ApplicationDbContext _context;

        public CoworkingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Coworking?> GetByIdAsync(int id)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Coworking>> GetAllAsync()
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                .ToListAsync();
        }

        public async Task<List<Coworking>> GetByUserIdAsync(int userId)
        {
            return await _context.Coworkings
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        public async Task<Coworking> CreateAsync(Coworking coworking)
        {
            _context.Coworkings.Add(coworking);
            await _context.SaveChangesAsync();
            return coworking;
        }

        public async Task<Coworking> UpdateAsync(Coworking coworking)
        {
            coworking.UpdatedAt = DateTime.UtcNow;
            _context.Coworkings.Update(coworking);
            await _context.SaveChangesAsync();
            return coworking;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var coworking = await GetByIdAsync(id);
            if (coworking == null) return false;

            _context.Coworkings.Remove(coworking);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CoworkingAddress?> GetAddressByIdAsync(int id)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null)
        {
            // Buscar o ID do serviço "Ofertar Endereço Fiscal"
            var fiscalAddressService = await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive);

            if (fiscalAddressService == null)
            {
                // Se o serviço não existir, retornar lista vazia
                return new List<CoworkingAddress>();
            }

            // Buscar os IDs dos coworkings que têm o serviço com percentagem > 0
            var coworkingIdsWithService = await _context.PartnerServices
                .Where(ps => ps.ServiceId == fiscalAddressService.Id && ps.Percentage > 0)
                .Select(ps => ps.CoworkingId)
                .Distinct()
                .ToListAsync();

            // Se não houver coworkings com o serviço configurado, retornar lista vazia
            if (!coworkingIdsWithService.Any())
            {
                return new List<CoworkingAddress>();
            }

            // Filtrar endereços: parceiro ativo, endereço ativo (suspenso/removido bloqueia só novas vendas)
            var query = _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .Where(a => a.IsAvailable 
                    && a.AddressStatus == AddressStatus.Active
                    && a.CurrentContracts < a.MaxContracts
                    && a.Coworking.IsActive 
                    && a.Coworking.IsPartner
                    && a.Coworking.ApprovalStatus == CoworkingApprovalStatus.Approved
                    && coworkingIdsWithService.Contains(a.CoworkingId));

            if (locationId.HasValue)
            {
                query = query.Where(a => a.LocationId == locationId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address)
        {
            _context.CoworkingAddresses.Add(address);
            await _context.SaveChangesAsync();
            return address;
        }

        public async Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address)
        {
            address.UpdatedAt = DateTime.UtcNow;
            _context.CoworkingAddresses.Update(address);
            await _context.SaveChangesAsync();
            return address;
        }
    }
}
