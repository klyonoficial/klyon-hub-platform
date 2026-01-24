namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status do processamento de webhooks
    /// </summary>
    public enum WebhookProcessingStatus
    {
        /// <summary>
        /// Webhook recebido, ainda não processado
        /// </summary>
        Received = 0,
        
        /// <summary>
        /// Webhook processado com sucesso
        /// </summary>
        Processed = 1,
        
        /// <summary>
        /// Webhook ignorado (duplicado ou não aplicável)
        /// </summary>
        Ignored = 2,
        
        /// <summary>
        /// Erro no processamento
        /// </summary>
        Failed = 3,
        
        /// <summary>
        /// Pagamento não encontrado
        /// </summary>
        PaymentNotFound = 4,
        
        /// <summary>
        /// Assinatura inválida
        /// </summary>
        InvalidSignature = 5
    }
}
