using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IContractRepository
    {
        Task<Contract?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetByCoworkingAddressIdAsync(int addressId, CancellationToken cancellationToken = default);
        Task<Contract?> GetActiveContractByAddressIdAsync(int addressId, CancellationToken cancellationToken = default);
        Task<Contract> CreateAsync(Contract contract, CancellationToken cancellationToken = default);
        Task<Contract> UpdateAsync(Contract contract, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<Contract>> GetByStatusAsync(ContractStatus status, CancellationToken cancellationToken = default);
    }
}