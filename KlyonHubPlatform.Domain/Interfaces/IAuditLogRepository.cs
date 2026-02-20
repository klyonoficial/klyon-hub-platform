using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<AuditLog> CreateAsync(AuditLog log, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default);
    }
}