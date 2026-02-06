using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Registro de perguntas não respondidas - importante para melhorias do FAQ
    /// </summary>
    public class ChatUnansweredQuestion
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
        /// ID da mensagem
        /// </summary>
        public int MessageId { get; set; }
        
        /// <summary>
        /// Mensagem relacionada
        /// </summary>
        public ChatMessage Message { get; set; } = null!;
        
        /// <summary>
        /// Pergunta do usuário
        /// </summary>
        public string Question { get; set; } = string.Empty;
        
        /// <summary>
        /// Intenção detectada (se houver)
        /// </summary>
        public string? DetectedIntention { get; set; }
        
        /// <summary>
        /// Categoria sugerida
        /// </summary>
        public string? SuggestedCategory { get; set; }
        
        /// <summary>
        /// Status (pending, under_review, resolved, added_to_faq)
        /// </summary>
        public UnansweredQuestionStatus Status { get; set; } = UnansweredQuestionStatus.Pending;
        
        /// <summary>
        /// ID do admin que revisou
        /// </summary>
        public int? ReviewedBy { get; set; }
        
        /// <summary>
        /// Admin que revisou
        /// </summary>
        public User? Reviewer { get; set; }
        
        /// <summary>
        /// Data de revisão
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
        
        /// <summary>
        /// Notas do revisor
        /// </summary>
        public string? ReviewNotes { get; set; }
        
        /// <summary>
        /// Número de vezes que esta pergunta apareceu
        /// </summary>
        public int Frequency { get; set; } = 1;
        
        /// <summary>
        /// Data de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
