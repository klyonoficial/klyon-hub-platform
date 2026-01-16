namespace KlyonHubPlatform.Domain.Entities
{
    public class Translation
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty; // Chave única da tradução
        public string LanguageCode { get; set; } = string.Empty; // pt-BR, en-US, es-ES
        public string Value { get; set; } = string.Empty; // Texto traduzido
        public string? Category { get; set; } // Categoria (ex: "Menu", "Messages", "Validation")
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual Language? Language { get; set; }
    }
}
