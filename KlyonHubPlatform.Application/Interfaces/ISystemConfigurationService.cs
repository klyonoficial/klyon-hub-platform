using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ISystemConfigurationService
    {
        Task<List<SystemConfiguration>> GetAllAsync();
        Task<SystemConfiguration?> GetByKeyAsync(string key);
        Task<SystemConfiguration?> GetByIdAsync(int id);
        Task<string?> GetValueAsync(string key);
        Task<SystemConfiguration> CreateOrUpdateAsync(string key, string value, string? description = null, string category = "General", bool isEncrypted = false, bool isSensitive = false, string? updatedBy = null);
        Task<SystemConfiguration> UpdateAsync(SystemConfiguration configuration, string? updatedBy = null);
        Task<List<SystemConfiguration>> GetByCategoryAsync(string category);
        Task<bool> DeleteAsync(int id);
    }
}
