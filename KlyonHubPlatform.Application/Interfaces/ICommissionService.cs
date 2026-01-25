using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ICommissionService
    {
        Task<Commission> CreateCommissionAsync(int paymentId);
        Task<List<Commission>> GetPendingCommissionsAsync(int coworkingId);
        Task<Transfer> CreateTransferAsync(int coworkingId, List<int> commissionIds);
        Task<Transfer> ApproveTransferAsync(int transferId);
        Task<Transfer> ProcessTransferAsync(int transferId);
    }
}
