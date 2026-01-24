namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Representa uma mensagem individual na conversa
    /// </summary>
    public class ChatMessage
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
        /// Tipo de mensagem (user, bot, system)
        /// </summary>
        public string MessageType { get; set; } = "user";
        
        /// <summary>
        /// Conteúdo da mensagem
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// ID do FAQ item usado (se aplicável)
        /// </summary>
        public int? FaqItemId { get; set; }
        
        /// <summary>
        /// FAQ item relacionado
        /// </summary>
        public ChatFaqItem? FaqItem { get; set; }
        
        /// <summary>
        /// Se a resposta foi encontrada na base de conhecimento
        /// </summary>
        public bool WasAnswered { get; set; } = false;
        
        /// <summary>
        /// Se foi necessário usar IA para responder
        /// </summary>
        public bool UsedAI { get; set; } = false;
        
        /// <summary>
        /// Confiança da resposta (0 a 1)
        /// </summary>
        public decimal? ConfidenceScore { get; set; }
        
        /// <summary>
        /// Data de envio da mensagem
        /// </summary>
        public DateTime SentAt { get; set; }
        
        /// <summary>
        /// Metadados adicionais (JSON)
        /// </summary>
        public string? Metadata { get; set; }
    }
}
