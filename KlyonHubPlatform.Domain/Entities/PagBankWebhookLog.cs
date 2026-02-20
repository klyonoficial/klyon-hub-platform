using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class PagBankWebhookLog
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public string? ResourceStatus { get; set; }
        public int? PaymentId { get; set; }
        public WebhookProcessingStatus ProcessingStatus { get; set; } = WebhookProcessingStatus.Received;
        public string? ErrorMessage { get; set; }
        public string? RawPayload { get; set; }
        public string? ReceivedSignature { get; set; }
        public bool SignatureValid { get; set; }
        public string? SourceIp { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public int ProcessingAttempts { get; set; } = 0;
        public virtual Payment? Payment { get; set; }
    }
}