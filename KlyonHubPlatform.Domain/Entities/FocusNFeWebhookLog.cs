using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Log de webhooks recebidos do Focus NFe
    /// Usado para rastreabilidade, idempotência e auditoria
    /// </summary>
    public class FocusNFeWebhookLog
    {
        public int Id { get; set; }

        /// <summary>Referência única da nota (nosso ID)</summary>
        public string Ref { get; set; } = string.Empty;

        /// <summary>CNPJ do emitente</summary>
        public string CnpjEmitente { get; set; } = string.Empty;

        /// <summary>Tipo do documento (NFSe, NFe, NFCe)</summary>
        public string DocumentType { get; set; } = "NFSe";

        /// <summary>Status recebido (autorizado, cancelado, etc.)</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Evento (autorizado, cancelado, etc.)</summary>
        public string? Evento { get; set; }

        /// <summary>Número da nota fiscal</summary>
        public string? Numero { get; set; }

        /// <summary>Código de verificação</summary>
        public string? CodigoVerificacao { get; set; }

        /// <summary>URL para visualização da nota</summary>
        public string? Url { get; set; }

        /// <summary>URL do XML da nota</summary>
        public string? CaminhoXml { get; set; }

        /// <summary>ID da nota fiscal no nosso sistema (FiscalNote)</summary>
        public int? FiscalNoteId { get; set; }

        /// <summary>Status do processamento do webhook</summary>
        public FocusWebhookProcessingStatus ProcessingStatus { get; set; } = FocusWebhookProcessingStatus.Received;

        /// <summary>Mensagem de erro (se houver)</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Payload completo recebido (JSON)</summary>
        public string? RawPayload { get; set; }

        /// <summary>IP de origem da requisição</summary>
        public string? SourceIp { get; set; }

        /// <summary>Data/hora de recebimento</summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Data/hora de processamento</summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Número de tentativas de processamento</summary>
        public int ProcessingAttempts { get; set; } = 0;

        // Navigation property
        public virtual FiscalNote? FiscalNote { get; set; }
    }
}
