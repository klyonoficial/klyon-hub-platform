using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPagBankWebhookLogRepository
    {
        Task<PagBankWebhookLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<PagBankWebhookLog?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
        Task<bool> ExistsEventIdAsync(string eventId, CancellationToken cancellationToken = default);
        Task<PagBankWebhookLog> CreateAsync(PagBankWebhookLog log, CancellationToken cancellationToken = default);
        Task<PagBankWebhookLog> UpdateAsync(PagBankWebhookLog log, CancellationToken cancellationToken = default);
        Task<List<PagBankWebhookLog>> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default);
        Task<List<PagBankWebhookLog>> GetByStatusAsync(WebhookProcessingStatus status, CancellationToken cancellationToken = default);
        Task<List<PagBankWebhookLog>> GetRecentLogsAsync(int count = 100, CancellationToken cancellationToken = default);
        Task<List<PagBankWebhookLog>> GetFailedLogsForRetryAsync(int maxAttempts = 3, CancellationToken cancellationToken = default);
    }
}
