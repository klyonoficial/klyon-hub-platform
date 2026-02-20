using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class FocusNFeWebhookLog
    {
        public int Id { get; set; }
        public string Ref { get; set; } = string.Empty;
        public string CnpjEmitente { get; set; } = string.Empty;
        public string DocumentType { get; set; } = "NFSe";
        public string Status { get; set; } = string.Empty;
        public string? Evento { get; set; }
        public string? Numero { get; set; }
        public string? CodigoVerificacao { get; set; }
        public string? Url { get; set; }
        public string? CaminhoXml { get; set; }
        public int? FiscalNoteId { get; set; }
        public FocusWebhookProcessingStatus ProcessingStatus { get; set; } = FocusWebhookProcessingStatus.Received;
        public string? ErrorMessage { get; set; }
        public string? RawPayload { get; set; }
        public string? SourceIp { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public int ProcessingAttempts { get; set; } = 0;
        public virtual FiscalNote? FiscalNote { get; set; }
    }
}