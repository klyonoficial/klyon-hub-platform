using KlyonHubPlatform.Application.Interfaces;
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<ClientRegistrationStatus> GetRegistrationStatusAsync(int userId)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
                return new ClientRegistrationStatus(false, "CompleteRegistration", "RegisterClient");

            var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
            var missingRequired = !hasDocument ||
                string.IsNullOrWhiteSpace(client.ContactPhone) ||
                string.IsNullOrWhiteSpace(client.ContactName) ||
                string.IsNullOrWhiteSpace(client.State);
            if (missingRequired)
                return new ClientRegistrationStatus(false, "CompleteRegistrationWithFields", "RegisterClient");

            return new ClientRegistrationStatus(true, null, null);
        }
    }
}
