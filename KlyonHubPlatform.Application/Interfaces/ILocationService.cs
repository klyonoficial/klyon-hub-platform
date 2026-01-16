using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ILocationService
    {
        Task<List<Location>> GetAllAsync();
        Task<List<Location>> GetFilteredAsync(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 12);
        Task<int> GetFilteredCountAsync(string? search = null, string? state = null, string? region = null, string? status = null);
        Task<Location?> GetByIdAsync(int id);
        Task<Location> CreateAsync(string city, string state, string? region, bool isActive, decimal commissionPercentage);
        Task<Location> UpdateAsync(int id, string city, string state, string? region, bool isActive);
        Task<bool> DeleteAsync(int id);
        Task UpdateCommissionAsync(int locationId, decimal commissionPercentage);
        Task<List<string>> GetStatesAsync();
        Task<List<string>> GetRegionsAsync();
        Task<List<LocationCommission>> GetActiveCommissionsAsync();
    }
}
