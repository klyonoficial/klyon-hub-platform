using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class SystemConfigurationService : ISystemConfigurationService
    {
        private readonly ApplicationDbContext _context;

        public SystemConfigurationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SystemConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SystemConfigurations
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Key)
                .ToListAsync(cancellationToken);
        }

        public async Task<SystemConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        }

        public async Task<SystemConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        {
            var config = await GetByKeyAsync(key, cancellationToken);
            return config?.Value;
        }

        public async Task<SystemConfiguration> CreateOrUpdateAsync(string key, string value, string? description = null, string category = SystemConfigurationCategoryKeys.General, bool isEncrypted = false, bool isSensitive = false, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            var existing = await GetByKeyAsync(key, cancellationToken);

            if (existing != null)
            {
                existing.Value = value;
                existing.Description = description ?? existing.Description;
                existing.Category = category;
                existing.IsEncrypted = isEncrypted;
                existing.IsSensitive = isSensitive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = updatedBy;
            }
            else
            {
                existing = new SystemConfiguration
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    Category = category,
                    IsEncrypted = isEncrypted,
                    IsSensitive = isSensitive,
                    UpdatedBy = updatedBy
                };
                _context.SystemConfigurations.Add(existing);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        public async Task<SystemConfiguration> UpdateAsync(SystemConfiguration configuration, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            configuration.UpdatedAt = DateTime.UtcNow;
            configuration.UpdatedBy = updatedBy;
            _context.SystemConfigurations.Update(configuration);
            await _context.SaveChangesAsync(cancellationToken);
            return configuration;
        }

        public async Task<List<SystemConfiguration>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            return await _context.SystemConfigurations
                .Where(c => c.Category == category)
                .OrderBy(c => c.Key)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var config = await GetByIdAsync(id, cancellationToken);
            if (config == null)
                return false;

            _context.SystemConfigurations.Remove(config);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
