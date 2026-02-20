namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public ChatConversation Conversation { get; set; } = null!;
        public string MessageType { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public int? FaqItemId { get; set; }
        public ChatFaqItem? FaqItem { get; set; }
        public bool WasAnswered { get; set; } = false;
        public bool UsedAI { get; set; } = false;
        public decimal? ConfidenceScore { get; set; }
        public DateTime SentAt { get; set; }
        public string? Metadata { get; set; }
    }
}