using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly ApplicationDbContext _context;
        public ContractRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Contract?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                        .ThenInclude(c => c.Partners)
                .Include(c => c.Service)
                .Include(c => c.Subscription)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
        public async Task<List<Contract>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                .Where(c => c.ClientId == clientId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<Contract>> GetByCoworkingAddressIdAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.Subscription)
                .Where(c => c.CoworkingAddressId == addressId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<Contract?> GetActiveContractByAddressIdAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.Subscription)
                .Where(c => c.CoworkingAddressId == addressId && c.Status == ContractStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);
        }
        public async Task<Contract> CreateAsync(Contract contract, CancellationToken cancellationToken = default)
        {
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync(cancellationToken);
            return contract;
        }
        public async Task<Contract> UpdateAsync(Contract contract, CancellationToken cancellationToken = default)
        {
            contract.UpdatedAt = DateTime.UtcNow;
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync(cancellationToken);
            return contract;
        }
        public async Task<List<Contract>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                .Include(c => c.Subscription)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<Contract>> GetByStatusAsync(ContractStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                .Where(c => c.Status == status)
                .ToListAsync(cancellationToken);
        }
    }
}