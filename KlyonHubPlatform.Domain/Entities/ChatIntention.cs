namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatIntention
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public ChatConversation Conversation { get; set; } = null!;
        public int MessageId { get; set; }
        public ChatMessage Message { get; set; } = null!;
        public string IntentionName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string? Entities { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}