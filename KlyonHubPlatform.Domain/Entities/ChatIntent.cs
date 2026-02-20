namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatIntent
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Keywords { get; set; } = "[]";
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatFAQ> FAQs { get; set; } = new List<ChatFAQ>();
    }
}