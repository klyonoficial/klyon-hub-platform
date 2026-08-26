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
        public async Task<Coworking?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
        public async Task<List<Coworking>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<Coworking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);
        }
        public async Task<Coworking> CreateAsync(Coworking coworking, CancellationToken cancellationToken = default)
        {
            _context.Coworkings.Add(coworking);
            await _context.SaveChangesAsync(cancellationToken);
            return coworking;
        }
        public async Task<Coworking> UpdateAsync(Coworking coworking, CancellationToken cancellationToken = default)
        {
            coworking.UpdatedAt = DateTime.UtcNow;
            _context.Coworkings.Update(coworking);
            await _context.SaveChangesAsync(cancellationToken);
            return coworking;
        }
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var coworking = await GetByIdAsync(id, cancellationToken);
            if (coworking == null) return false;
            _context.Coworkings.Remove(coworking);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        public async Task<CoworkingAddress?> GetAddressByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }
        public async Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null, CancellationToken cancellationToken = default)
        {
            var fiscalAddressService = await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive, cancellationToken);
            if (fiscalAddressService == null)
            {
                return new List<CoworkingAddress>();
            }
            var coworkingIdsWithService = await _context.PartnerServices
                .Where(ps => ps.ServiceId == fiscalAddressService.Id && ps.Percentage > 0)
                .Select(ps => ps.CoworkingId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (!coworkingIdsWithService.Any())
            {
                return new List<CoworkingAddress>();
            }
            var query = _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .Where(a => a.IsAvailable 
                    && a.AddressStatus == AddressStatus.Active
                    && a.Coworking.IsActive 
                    && a.Coworking.IsPartner
                    && a.Coworking.ApprovalStatus == CoworkingApprovalStatus.Approved
                    && coworkingIdsWithService.Contains(a.CoworkingId));
            if (locationId.HasValue)
            {
                query = query.Where(a => a.LocationId == locationId.Value);
            }
            return await query.ToListAsync(cancellationToken);
        }
        public async Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default)
        {
            _context.CoworkingAddresses.Add(address);
            await _context.SaveChangesAsync(cancellationToken);
            return address;
        }
        public async Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default)
        {
            address.UpdatedAt = DateTime.UtcNow;
            _context.CoworkingAddresses.Update(address);
            await _context.SaveChangesAsync(cancellationToken);
            return address;
        }
    }
}