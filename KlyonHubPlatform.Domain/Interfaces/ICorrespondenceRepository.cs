using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface ICorrespondenceRepository
    {
        Task<Correspondence?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Correspondence> CreateAsync(Correspondence correspondence, CancellationToken cancellationToken = default);
        Task<Correspondence> UpdateAsync(Correspondence correspondence, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByCoworkingIdFilteredAsync(int coworkingId, DateTime? pickupDateFrom, DateTime? pickupDateTo, CorrespondencePickupStatus? status, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetAllAsync(int? coworkingIdFilter, CancellationToken cancellationToken = default);
    }
}
