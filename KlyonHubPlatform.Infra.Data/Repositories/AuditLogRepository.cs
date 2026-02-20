using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        public AuditLogRepository(ApplicationDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }
        public async Task<AuditLog> CreateAsync(AuditLog log, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var separateContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                separateContext.AuditLogs.Add(log);
                await separateContext.SaveChangesAsync(cancellationToken);
                return log;
            }
            catch (Exception ex)
            {
                try
                {
                    _context.AuditLogs.Add(log);
                    await _context.SaveChangesAsync(cancellationToken);
                    return log;
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao criar AuditLog: {ex.Message}");
                    return log;
                }
            }
        }
        public async Task<List<AuditLog>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.AuditLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.ActionDate)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
        {
            return await _context.AuditLogs
                .Include(l => l.User)
                .Where(l => l.EntityType == entityType && l.EntityId == entityId)
                .OrderByDescending(l => l.ActionDate)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<AuditLog>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            return await _context.AuditLogs
                .OrderByDescending(l => l.ActionDate)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
    }
}