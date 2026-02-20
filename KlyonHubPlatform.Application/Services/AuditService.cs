using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
namespace KlyonHubPlatform.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        public AuditService(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;
        }
        public async Task LogActionAsync(int? userId, string action, string entityType, int? entityId, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ActionDate = DateTime.UtcNow
                };
                await _auditLogRepository.CreateAsync(log, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao criar log de auditoria: {ex.Message}");
            }
        }
        public async Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
        {
            return await _auditLogRepository.GetByEntityAsync(entityType, entityId, cancellationToken);
        }
        public async Task<List<AuditLog>> GetLogsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _auditLogRepository.GetByUserIdAsync(userId, cancellationToken);
        }
        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            return await _auditLogRepository.GetRecentAsync(count, cancellationToken);
        }
    }
}