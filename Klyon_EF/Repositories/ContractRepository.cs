using Microsoft.EntityFrameworkCore;
using KlyonEF.Data;
using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly ApplicationDbContext _context;

        public ContractRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Contract?> GetByIdAsync(int id)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(c => c.Subscription)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Contract>> GetByClientIdAsync(int clientId)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                .Where(c => c.ClientId == clientId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Contract>> GetByCoworkingAddressIdAsync(int addressId)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.Subscription)
                .Where(c => c.CoworkingAddressId == addressId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Contract?> GetActiveContractByAddressIdAsync(int addressId)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.Subscription)
                .Where(c => c.CoworkingAddressId == addressId && c.Status == ContractStatus.Active)
                .FirstOrDefaultAsync();
        }

        public async Task<Contract> CreateAsync(Contract contract)
        {
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();
            return contract;
        }

        public async Task<Contract> UpdateAsync(Contract contract)
        {
            contract.UpdatedAt = DateTime.UtcNow;
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
            return contract;
        }

        public async Task<List<Contract>> GetAllAsync()
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                .Include(c => c.Subscription)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Contract>> GetByStatusAsync(ContractStatus status)
        {
            return await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                .Where(c => c.Status == status)
                .ToListAsync();
        }
    }
}
