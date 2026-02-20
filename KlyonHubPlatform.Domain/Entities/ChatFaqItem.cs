namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatFaqItem
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string Intentions { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public int UsageCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}