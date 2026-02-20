namespace KlyonHubPlatform.Domain.Entities
{
    public class Translation
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Language? Language { get; set; }
    }
}