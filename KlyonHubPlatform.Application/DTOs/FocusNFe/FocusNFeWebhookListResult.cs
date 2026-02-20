namespace KlyonHubPlatform.Application.DTOs.FocusNFe;

public class FocusNFeWebhookListResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyList<FocusNFeWebhookLogItemDto> Logs { get; set; } = Array.Empty<FocusNFeWebhookLogItemDto>();
}

public class FocusNFeWebhookLogItemDto
{
    public int Id { get; set; }
    public string Ref { get; set; } = string.Empty;
    public string? CnpjEmitente { get; set; }
    public string? DocumentType { get; set; }
    public string? Status { get; set; }
    public string? Evento { get; set; }
    public string? Numero { get; set; }
    public string? CodigoVerificacao { get; set; }
    public string? Url { get; set; }
    public int? FiscalNoteId { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? SourceIp { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int ProcessingAttempts { get; set; }
}
