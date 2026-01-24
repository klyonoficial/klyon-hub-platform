using System;
using System.Collections.Generic;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Representa uma intenção identificada pelo sistema de IA
    /// </summary>
    public class ChatIntent
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Nome da intenção (ex: cadastro, pagamento, reserva)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Descrição da intenção
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Palavras-chave associadas (JSON array)
        /// </summary>
        public string Keywords { get; set; } = "[]";
        
        /// <summary>
        /// Categoria da intenção
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Indica se está ativa
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Data de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Data da última atualização
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Mensagens que detectaram esta intenção
        /// </summary>
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        
        /// <summary>
        /// FAQs relacionadas a esta intenção
        /// </summary>
        public ICollection<ChatFAQ> FAQs { get; set; } = new List<ChatFAQ>();
    }
}
