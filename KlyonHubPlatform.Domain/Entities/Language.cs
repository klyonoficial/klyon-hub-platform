namespace KlyonHubPlatform.Domain.Entities
{
    public class Language
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // pt-BR, en-US, es-ES
        public string Name { get; set; } = string.Empty; // Português, English, Español
        public string NativeName { get; set; } = string.Empty; // Português, English, Español
        public string FlagIcon { get; set; } = string.Empty; // 🇧🇷, 🇺🇸, 🇪🇸 ou classes CSS
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
