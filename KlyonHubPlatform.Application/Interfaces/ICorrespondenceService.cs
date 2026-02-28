using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ICorrespondenceService
    {
        Task<Correspondence?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Correspondence> RegisterArrivalAsync(
            int contractId,
            DateTime arrivalDate,
            string? notes,
            int createdByUserId,
            IReadOnlyList<(string FilePath, string FileName)> photos,
            CancellationToken cancellationToken = default);
        Task<Correspondence> SchedulePickupAsync(int correspondenceId, DateTime scheduledDate, TimeSpan scheduledTime, int clientId, CancellationToken cancellationToken = default);
        Task ResendArrivalNotificationAsync(int correspondenceId, int coworkingId, CancellationToken cancellationToken = default);
        Task<Correspondence> MarkAsPickedUpAsync(int correspondenceId, int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetByCoworkingIdFilteredAsync(int coworkingId, DateTime? pickupDateFrom, DateTime? pickupDateTo, CorrespondencePickupStatus? status, CancellationToken cancellationToken = default);
        Task<List<Correspondence>> GetAllAsync(int? coworkingIdFilter, CancellationToken cancellationToken = default);
    }
}
