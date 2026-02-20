using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminDataService : IAdminDataService
    {
        private readonly ApplicationDbContext _context;

        public AdminDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CoworkingAddress?> GetAddressByIdAndCoworkingAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task SetAddressStatusAsync(int addressId, int coworkingId, AddressStatus newStatus, CancellationToken cancellationToken = default)
        {
            var address = await GetAddressByIdAndCoworkingAsync(addressId, coworkingId, cancellationToken);
            if (address == null) return;
            if (newStatus == AddressStatus.Active)
            {
                address.AddressStatus = AddressStatus.Active;
                address.IsAvailable = true;
            }
            else if (newStatus == AddressStatus.PendingApproval || newStatus == AddressStatus.Suspended || newStatus == AddressStatus.Removed)
            {
                address.AddressStatus = newStatus;
                address.IsAvailable = false;
            }
            else
            {
                address.AddressStatus = newStatus;
            }
            address.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Coworking?> GetCoworkingWithUserAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<AuditLog?> GetAuditLogWithUserAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.AuditLogs
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        }

        public async Task<Coworking?> GetCoworkingForEditAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Coworking?> FindCoworkingAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<bool> CoworkingExistsCnpjAsync(string cnpjClean, int excludeCoworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .AnyAsync(c => c.Id != excludeCoworkingId && c.CNPJ != null &&
                    c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean, cancellationToken);
        }

        public async Task<Service?> GetFiscalAddressServiceAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive, cancellationToken);
        }

        public async Task<PartnerService?> GetPartnerServiceForCoworkingAndServiceAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default)
        {
            return await _context.PartnerServices
                .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId && ps.Percentage > 0, cancellationToken);
        }

        public async Task<(bool Success, string? ErrorMessage)> ToggleCoworkingStatusAsync(int id, CancellationToken cancellationToken = default)
        {
            var coworking = await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);
            if (coworking == null)
                return (false, "Coworking não encontrado.");
            if (!coworking.IsActive)
            {
                var fiscalAddressService = await GetFiscalAddressServiceAsync(cancellationToken);
                if (fiscalAddressService != null)
                {
                    var partnerService = await GetPartnerServiceForCoworkingAndServiceAsync(coworking.Id, fiscalAddressService.Id, cancellationToken);
                    if (partnerService == null)
                        return (false, $"O coworking '{coworking.Name}' não pode ser ativado sem percentual de endereço fiscal.");
                }
            }
            coworking.IsActive = !coworking.IsActive;
            coworking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
    }
}
