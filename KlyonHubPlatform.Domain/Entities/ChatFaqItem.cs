namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Item da base de conhecimento (FAQ) do KlyonBoot
    /// </summary>
    public class ChatFaqItem
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Categoria do FAQ (Cadastro, Reservas, Pagamentos, etc.)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Pergunta principal
        /// </summary>
        public string Question { get; set; } = string.Empty;
        
        /// <summary>
        /// Resposta formatada
        /// </summary>
        public string Answer { get; set; } = string.Empty;
        
        /// <summary>
        /// Palavras-chave para matching (separadas por vírgula)
        /// </summary>
        public string Keywords { get; set; } = string.Empty;
        
        /// <summary>
        /// Intenções relacionadas (separadas por vírgula)
        /// </summary>
        public string Intentions { get; set; } = string.Empty;
        
        /// <summary>
        /// Ordem de exibição
        /// </summary>
        public int DisplayOrder { get; set; }
        
        /// <summary>
        /// Se o item está ativo
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Número de vezes que esta resposta foi utilizada
        /// </summary>
        public int UsageCount { get; set; } = 0;
        
        /// <summary>
        /// Data de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Data da última atualização
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
