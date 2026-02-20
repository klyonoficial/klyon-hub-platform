using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatUnansweredQuestion
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public ChatConversation Conversation { get; set; } = null!;
        public int MessageId { get; set; }
        public ChatMessage Message { get; set; } = null!;
        public string Question { get; set; } = string.Empty;
        public string? DetectedIntention { get; set; }
        public string? SuggestedCategory { get; set; }
        public UnansweredQuestionStatus Status { get; set; } = UnansweredQuestionStatus.Pending;
        public int? ReviewedBy { get; set; }
        public User? Reviewer { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
        public int Frequency { get; set; } = 1;
        public DateTime CreatedAt { get; set; }
    }
}