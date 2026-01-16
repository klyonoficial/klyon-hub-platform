using KlyonEF.Models;

namespace KlyonEF.Services
{
    public interface ICommissionService
    {
        Task<Commission> CreateCommissionAsync(int paymentId);
        Task<List<Commission>> GetPendingCommissionsAsync(int coworkingId);
        Task<Transfer> CreateTransferAsync(int coworkingId, List<int> commissionIds);
        Task<Transfer> ProcessTransferAsync(int transferId);
    }
}
