using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IOperatingHoursRepository
    {
        Task<CoworkingOperatingHours?> GetByCoworkingAndDayAsync(int coworkingId, string dayOfWeek, CancellationToken cancellationToken = default);
        Task<List<CoworkingOperatingHours>> GetByCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default);
    }
}
