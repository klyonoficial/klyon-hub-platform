using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface ICommissionRepository
    {
        Task<Commission?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Commission>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Commission>> GetPendingTransfersAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<Commission> CreateAsync(Commission commission, CancellationToken cancellationToken = default);
        Task<Commission> UpdateAsync(Commission commission, CancellationToken cancellationToken = default);
    }
}