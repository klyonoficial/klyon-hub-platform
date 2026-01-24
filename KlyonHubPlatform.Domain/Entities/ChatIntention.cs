namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Representa uma intenção detectada pela IA durante a conversa
    /// </summary>
    public class ChatIntention
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID da conversa
        /// </summary>
        public int ConversationId { get; set; }
        
        /// <summary>
        /// Conversa relacionada
        /// </summary>
        public ChatConversation Conversation { get; set; } = null!;
        
        /// <summary>
        /// ID da mensagem do usuário
        /// </summary>
        public int MessageId { get; set; }
        
        /// <summary>
        /// Mensagem relacionada
        /// </summary>
        public ChatMessage Message { get; set; } = null!;
        
        /// <summary>
        /// Nome da intenção detectada (ex: "cadastro_parceiro", "consultar_reserva")
        /// </summary>
        public string IntentionName { get; set; } = string.Empty;
        
        /// <summary>
        /// Categoria da intenção
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Nível de confiança da detecção (0 a 1)
        /// </summary>
        public decimal Confidence { get; set; }
        
        /// <summary>
        /// Entidades extraídas (JSON)
        /// </summary>
        public string? Entities { get; set; }
        
        /// <summary>
        /// Data de detecção
        /// </summary>
        public DateTime DetectedAt { get; set; }
    }
}
