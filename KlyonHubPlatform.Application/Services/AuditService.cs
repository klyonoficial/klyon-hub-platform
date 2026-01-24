using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Serviço de auditoria para logs imutáveis de ações críticas do sistema.
    /// Os logs registrados por este serviço não podem ser editados ou excluídos.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;

        public AuditService(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;
        }

        /// <summary>
        /// Registra uma ação crítica no log de auditoria.
        /// Este log é imutável e não pode ser editado ou excluído.
        /// </summary>
        /// <param name="userId">ID do usuário que realizou a ação (null para ações do sistema)</param>
        /// <param name="action">Descrição da ação realizada</param>
        /// <param name="entityType">Tipo da entidade afetada (ex: "Payment", "Contract", "FiscalNote")</param>
        /// <param name="entityId">ID da entidade afetada</param>
        /// <param name="oldValues">Valores anteriores (JSON ou texto)</param>
        /// <param name="newValues">Valores novos (JSON ou texto)</param>
        /// <param name="ipAddress">Endereço IP de origem</param>
        /// <param name="userAgent">User Agent do navegador/cliente</param>
        public async Task LogActionAsync(int? userId, string action, string entityType, int? entityId, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null)
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

                await _auditLogRepository.CreateAsync(log);
            }
            catch (Exception ex)
            {
                // Se houver erro ao criar o log de auditoria (ex: tabela foi limpa, AUTO_INCREMENT não resetado),
                // não quebrar a aplicação. Apenas logar o erro silenciosamente.
                // Em produção, considere usar um logger adequado aqui.
                System.Diagnostics.Debug.WriteLine($"Erro ao criar log de auditoria: {ex.Message}");
            }
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
