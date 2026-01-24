namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Representa uma conversa completa com o KlyonBoot
    /// </summary>
    public class ChatConversation
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID do usuário (opcional, pode ser anônimo)
        /// </summary>
        public int? UserId { get; set; }
        
        /// <summary>
        /// Usuário relacionado
        /// </summary>
        public User? User { get; set; }
        
        /// <summary>
        /// Identificador de sessão (para usuários anônimos)
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// IP do usuário
        /// </summary>
        public string? IpAddress { get; set; }
        
        /// <summary>
        /// User Agent do navegador
        /// </summary>
        public string? UserAgent { get; set; }
        
        /// <summary>
        /// Data de início da conversa
        /// </summary>
        public DateTime StartedAt { get; set; }
        
        /// <summary>
        /// Data de término da conversa
        /// </summary>
        public DateTime? EndedAt { get; set; }
        
        /// <summary>
        /// Se a conversa ainda está ativa
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Se foi resolvida
        /// </summary>
        public bool? WasResolved { get; set; }
        
        /// <summary>
        /// Se foi direcionado para atendimento humano
        /// </summary>
        public bool WasRedirectedToHuman { get; set; } = false;
        
        /// <summary>
        /// Mensagens da conversa
        /// </summary>
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        
        /// <summary>
        /// Intenções detectadas nesta conversa
        /// </summary>
        public ICollection<ChatIntention> Intentions { get; set; } = new List<ChatIntention>();
    }
}
