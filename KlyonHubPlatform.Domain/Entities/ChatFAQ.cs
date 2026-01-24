using System;
using System.Collections.Generic;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Representa uma entrada da base de conhecimento (FAQ) do chatbot
    /// </summary>
    public class ChatFAQ
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID da intenção relacionada
        /// </summary>
        public int? IntentId { get; set; }
        public ChatIntent? Intent { get; set; }
        
        /// <summary>
        /// Pergunta/título da FAQ
        /// </summary>
        public string Question { get; set; } = string.Empty;
        
        /// <summary>
        /// Resposta da FAQ
        /// </summary>
        public string Answer { get; set; } = string.Empty;
        
        /// <summary>
        /// Categoria (Cadastro, Pagamento, Reserva, etc)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Ordem de exibição
        /// </summary>
        public int DisplayOrder { get; set; }
        
        /// <summary>
        /// Indica se está ativa
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Contador de quantas vezes foi usada
        /// </summary>
        public int UsageCount { get; set; }
        
        /// <summary>
        /// Data de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Data da última atualização
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Mensagens que foram respondidas por esta FAQ
        /// </summary>
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
