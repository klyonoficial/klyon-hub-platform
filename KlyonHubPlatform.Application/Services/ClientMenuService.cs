using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
namespace KlyonHubPlatform.Application.Services
{
    public class ClientMenuService : IClientMenuService
    {
        private readonly ApplicationDbContext _context;
        public ClientMenuService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<bool> ClientHasActiveContractAsync(int userId)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
                return false;
            return await _context.Contracts
                .AsNoTracking()
                .AnyAsync(c => c.ClientId == client.Id && c.Status == ContractStatus.Active);
        }
        public async Task<ClientRegistrationStatus> GetRegistrationStatusAsync(int userId)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
                return new ClientRegistrationStatus(false, ClientRegistrationMessageKeys.CompleteRegistration, "RegisterClient");
            var hasPaidContract = await _context.Contracts
                .AsNoTracking()
                .AnyAsync(c => c.ClientId == client.Id &&
                    (c.Status == ContractStatus.Active || c.Status == ContractStatus.PendingCancellation));
            if (hasPaidContract && !ClientContractPartyRules.ClientContractPartyIsComplete(client))
                return new ClientRegistrationStatus(false, ClientRegistrationMessageKeys.CompleteRegistrationWithFields, "Profile");
            return new ClientRegistrationStatus(true, null, null);
        }
    }
}