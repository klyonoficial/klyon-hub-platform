using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;

namespace KlyonHubPlatform.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;

        public AuditService(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;
        }

        public async Task LogActionAsync(int? userId, string action, string entityType, int? entityId, string? oldValues = null, string? newValues = null)
        {
            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                ActionDate = DateTime.UtcNow
            };

            await _auditLogRepository.CreateAsync(log);
        }

        public async Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId)
        {
            return await _auditLogRepository.GetByEntityAsync(entityType, entityId);
        }

        public async Task<List<AuditLog>> GetLogsByUserIdAsync(int userId)
        {
            return await _auditLogRepository.GetByUserIdAsync(userId);
        }

        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100)
        {
            return await _auditLogRepository.GetRecentAsync(count);
        }
    }
}
