using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class PagBankWebhookLogRepository : IPagBankWebhookLogRepository
    {
        private readonly ApplicationDbContext _context;
        public PagBankWebhookLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<PagBankWebhookLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        }
        public async Task<PagBankWebhookLog?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .FirstOrDefaultAsync(w => w.EventId == eventId, cancellationToken);
        }
        public async Task<bool> ExistsEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .AnyAsync(w => w.EventId == eventId, cancellationToken);
        }
        public async Task<PagBankWebhookLog> CreateAsync(PagBankWebhookLog log, CancellationToken cancellationToken = default)
        {
            _context.PagBankWebhookLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
            return log;
        }
        public async Task<PagBankWebhookLog> UpdateAsync(PagBankWebhookLog log, CancellationToken cancellationToken = default)
        {
            _context.PagBankWebhookLogs.Update(log);
            await _context.SaveChangesAsync(cancellationToken);
            return log;
        }
        public async Task<List<PagBankWebhookLog>> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .Where(w => w.ResourceId == resourceId)
                .OrderByDescending(w => w.ReceivedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<PagBankWebhookLog>> GetByStatusAsync(WebhookProcessingStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .Where(w => w.ProcessingStatus == status)
                .OrderByDescending(w => w.ReceivedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<PagBankWebhookLog>> GetRecentLogsAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .OrderByDescending(w => w.ReceivedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<PagBankWebhookLog>> GetFailedLogsForRetryAsync(int maxAttempts = 3, CancellationToken cancellationToken = default)
        {
            return await _context.PagBankWebhookLogs
                .Include(w => w.Payment)
                .Where(w => w.ProcessingStatus == WebhookProcessingStatus.Failed 
                         && w.ProcessingAttempts < maxAttempts)
                .OrderBy(w => w.ReceivedAt)
                .ToListAsync(cancellationToken);
        }
    }
}