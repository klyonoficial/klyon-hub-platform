using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Registro de webhooks recebidos do PagBank para garantir idempotência
    /// e manter histórico de notificações.
    /// </summary>
    public class PagBankWebhookLog
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID único do evento enviado pelo PagBank (usado para idempotência)
        /// </summary>
        public string EventId { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo do evento (CHARGE_PAID, CHARGE_CANCELED, etc.)
        /// </summary>
        public string EventType { get; set; } = string.Empty;
        
        /// <summary>
        /// ID do recurso (charge ou order)
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Reference ID do pedido (nosso ExternalPaymentId original)
        /// </summary>
        public string? ReferenceId { get; set; }
        
        /// <summary>
        /// Status do recurso no momento da notificação
        /// </summary>
        public string? ResourceStatus { get; set; }
        
        /// <summary>
        /// ID do Payment associado (se encontrado)
        /// </summary>
        public int? PaymentId { get; set; }
        
        /// <summary>
        /// Status do processamento do webhook
        /// </summary>
        public WebhookProcessingStatus ProcessingStatus { get; set; } = WebhookProcessingStatus.Received;
        
        /// <summary>
        /// Mensagem de erro se o processamento falhou
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Payload JSON completo recebido (para debug)
        /// </summary>
        public string? RawPayload { get; set; }
        
        /// <summary>
        /// Assinatura recebida no header
        /// </summary>
        public string? ReceivedSignature { get; set; }
        
        /// <summary>
        /// Se a assinatura foi validada com sucesso
        /// </summary>
        public bool SignatureValid { get; set; }
        
        /// <summary>
        /// IP de origem da requisição
        /// </summary>
        public string? SourceIp { get; set; }
        
        /// <summary>
        /// Data/hora de recebimento
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Data/hora de processamento
        /// </summary>
        public DateTime? ProcessedAt { get; set; }
        
        /// <summary>
        /// Número de tentativas de processamento
        /// </summary>
        public int ProcessingAttempts { get; set; } = 0;
        
        // Navigation property
        public virtual Payment? Payment { get; set; }
    }
}
