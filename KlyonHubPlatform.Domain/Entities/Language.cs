namespace KlyonHubPlatform.Domain.Entities
{
    public class Language
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public string FlagIcon { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }
}