using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ISystemConfigurationService
    {
        Task<List<SystemConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SystemConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
        Task<SystemConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
        Task<SystemConfiguration> CreateOrUpdateAsync(string key, string value, string? description = null, string category = "General", bool isEncrypted = false, bool isSensitive = false, string? updatedBy = null, CancellationToken cancellationToken = default);
        Task<SystemConfiguration> UpdateAsync(SystemConfiguration configuration, string? updatedBy = null, CancellationToken cancellationToken = default);
        Task<List<SystemConfiguration>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
