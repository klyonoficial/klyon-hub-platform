using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractService
    {
        Task<Contract?> GetByIdAsync(int id);
        Task<List<Contract>> GetByClientIdAsync(int clientId);
        Task<Contract> CreateContractAsync(int clientId, int coworkingAddressId, decimal monthlyValue, int? assistedSaleAdminId = null, PaymentMethod? initialPaymentMethod = null);
        Task<Contract> CancelContractAsync(int contractId, string reason);
        Task<Contract> SuspendContractAsync(int contractId);
        Task<Contract> ActivateContractAsync(int contractId);
        Task<string> GenerateContractNumberAsync();
    }
}