using Microsoft.EntityFrameworkCore;
using KlyonEF.Data;
using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _context;

        public AuditLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AuditLog> CreateAsync(AuditLog log)
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }

        public async Task<List<AuditLog>> GetByUserIdAsync(int userId)
        {
            return await _context.AuditLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.ActionDate)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetByEntityAsync(string entityType, int entityId)
        {
            return await _context.AuditLogs
                .Where(l => l.EntityType == entityType && l.EntityId == entityId)
                .OrderByDescending(l => l.ActionDate)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
        {
            return await _context.AuditLogs
                .OrderByDescending(l => l.ActionDate)
                .Take(count)
                .ToListAsync();
        }
    }
}
