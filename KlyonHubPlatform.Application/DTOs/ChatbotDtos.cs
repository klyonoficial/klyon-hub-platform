namespace KlyonHubPlatform.Application.DTOs
{
    public class ChatRequestDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
    public class ChatResponseDto
    {
        public int ConversationId { get; set; }
        public int MessageId { get; set; }
        public string Response { get; set; } = string.Empty;
        public string MessageType { get; set; } = "bot";
        public bool WasAnswered { get; set; }
        public bool UsedAI { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string? IntentionDetected { get; set; }
        public bool SuggestHumanSupport { get; set; }
        public DateTime SentAt { get; set; }
    }
    public class ConversationDto
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public bool IsActive { get; set; }
        public bool? WasResolved { get; set; }
        public List<MessageDto> Messages { get; set; } = new();
    }
    public class MessageDto
    {
        public int Id { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool WasAnswered { get; set; }
        public DateTime SentAt { get; set; }
    }
    public class FaqItemDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public decimal? RelevanceScore { get; set; }
    }
    public class UnansweredQuestionDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string? DetectedIntention { get; set; }
        public string? SuggestedCategory { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}