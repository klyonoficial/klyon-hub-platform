using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAuditService
    {
        Task LogActionAsync(int? userId, string action, string entityType, int? entityId, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetLogsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, CancellationToken cancellationToken = default);
    }
}