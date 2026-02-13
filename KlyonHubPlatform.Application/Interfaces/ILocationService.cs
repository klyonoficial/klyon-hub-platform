using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ILocationService
    {
        Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<Location>> GetFilteredAsync(string? search = null, string? state = null, string? region = null, ActiveFilter? status = null, int page = 1, int pageSize = 12, CancellationToken cancellationToken = default);
        Task<int> GetFilteredCountAsync(string? search = null, string? state = null, string? region = null, ActiveFilter? status = null, CancellationToken cancellationToken = default);
        Task<Location?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Location> CreateAsync(string city, string state, string? region, bool isActive, decimal commissionPercentage, CancellationToken cancellationToken = default);
        Task<Location> UpdateAsync(int id, string city, string state, string? region, bool isActive, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task UpdateCommissionAsync(int locationId, decimal commissionPercentage, CancellationToken cancellationToken = default);
        Task<List<string>> GetStatesAsync(CancellationToken cancellationToken = default);
        Task<List<string>> GetRegionsAsync(CancellationToken cancellationToken = default);
        Task<List<LocationCommission>> GetActiveCommissionsAsync(CancellationToken cancellationToken = default);
    }
}
