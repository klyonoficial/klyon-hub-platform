using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public interface IAuditLogRepository
    {
        Task<AuditLog> CreateAsync(AuditLog log);
        Task<List<AuditLog>> GetByUserIdAsync(int userId);
        Task<List<AuditLog>> GetByEntityAsync(string entityType, int entityId);
        Task<List<AuditLog>> GetRecentAsync(int count = 100);
    }
}
