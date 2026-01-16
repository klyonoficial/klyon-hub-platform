using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
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

        public async Task<List<SystemConfiguration>> GetAllAsync()
        {
            return await _context.SystemConfigurations
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Key)
                .ToListAsync();
        }

        public async Task<SystemConfiguration?> GetByKeyAsync(string key)
        {
            return await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Key == key);
        }

        public async Task<SystemConfiguration?> GetByIdAsync(int id)
        {
            return await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<string?> GetValueAsync(string key)
        {
            var config = await GetByKeyAsync(key);
            return config?.Value;
        }

        public async Task<SystemConfiguration> CreateOrUpdateAsync(string key, string value, string? description = null, string category = "General", bool isEncrypted = false, bool isSensitive = false, string? updatedBy = null)
        {
            var existing = await GetByKeyAsync(key);

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

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<SystemConfiguration> UpdateAsync(SystemConfiguration configuration, string? updatedBy = null)
        {
            configuration.UpdatedAt = DateTime.UtcNow;
            configuration.UpdatedBy = updatedBy;
            _context.SystemConfigurations.Update(configuration);
            await _context.SaveChangesAsync();
            return configuration;
        }

        public async Task<List<SystemConfiguration>> GetByCategoryAsync(string category)
        {
            return await _context.SystemConfigurations
                .Where(c => c.Category == category)
                .OrderBy(c => c.Key)
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var config = await GetByIdAsync(id);
            if (config == null)
                return false;

            _context.SystemConfigurations.Remove(config);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
