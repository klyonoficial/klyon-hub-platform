using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public interface IContractRepository
    {
        Task<Contract?> GetByIdAsync(int id);
        Task<List<Contract>> GetByClientIdAsync(int clientId);
        Task<List<Contract>> GetByCoworkingAddressIdAsync(int addressId);
        Task<Contract?> GetActiveContractByAddressIdAsync(int addressId);
        Task<Contract> CreateAsync(Contract contract);
        Task<Contract> UpdateAsync(Contract contract);
        Task<List<Contract>> GetAllAsync();
        Task<List<Contract>> GetByStatusAsync(ContractStatus status);
    }
}
