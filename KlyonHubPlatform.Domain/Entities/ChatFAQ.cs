namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatFAQ
    {
        public int Id { get; set; }
        public int? IntentId { get; set; }
        public ChatIntent? Intent { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public int UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}