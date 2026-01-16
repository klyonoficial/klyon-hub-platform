using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAuditService
    {
        Task LogActionAsync(int? userId, string action, string entityType, int? entityId, string? oldValues = null, string? newValues = null);
        Task<List<AuditLog>> GetLogsByUserIdAsync(int userId);
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 100);
    }
}
