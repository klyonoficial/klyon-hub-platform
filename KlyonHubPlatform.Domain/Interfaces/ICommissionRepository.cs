using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface ICommissionRepository
    {
        Task<Commission?> GetByIdAsync(int id);
        Task<List<Commission>> GetByCoworkingIdAsync(int coworkingId);
        Task<List<Commission>> GetPendingTransfersAsync(int coworkingId);
        Task<Commission> CreateAsync(Commission commission);
        Task<Commission> UpdateAsync(Commission commission);
    }
}
