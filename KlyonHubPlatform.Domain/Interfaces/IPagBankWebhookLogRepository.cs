using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPagBankWebhookLogRepository
    {
        Task<PagBankWebhookLog?> GetByIdAsync(int id);
        Task<PagBankWebhookLog?> GetByEventIdAsync(string eventId);
        Task<bool> ExistsEventIdAsync(string eventId);
        Task<PagBankWebhookLog> CreateAsync(PagBankWebhookLog log);
        Task<PagBankWebhookLog> UpdateAsync(PagBankWebhookLog log);
        Task<List<PagBankWebhookLog>> GetByResourceIdAsync(string resourceId);
        Task<List<PagBankWebhookLog>> GetByStatusAsync(WebhookProcessingStatus status);
        Task<List<PagBankWebhookLog>> GetRecentLogsAsync(int count = 100);
        Task<List<PagBankWebhookLog>> GetFailedLogsForRetryAsync(int maxAttempts = 3);
    }
}
