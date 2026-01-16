namespace KlyonHubPlatform.Domain.Entities
{
    public class SystemConfiguration
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty; // Chave única da configuração
        public string Value { get; set; } = string.Empty; // Valor da configuração (pode ser criptografado)
        public string? Description { get; set; } // Descrição do que é essa configuração
        public string Category { get; set; } = string.Empty; // Categoria: Email, Payment, Storage, etc.
        public bool IsEncrypted { get; set; } = false; // Se o valor está criptografado
        public bool IsSensitive { get; set; } = false; // Se é informação sensível (senhas, tokens)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; } // Email do usuário que atualizou
    }
}
