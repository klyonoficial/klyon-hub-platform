namespace KlyonHubPlatform.Domain.Entities
{
    public class ChatConversation
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool? WasResolved { get; set; }
        public bool WasRedirectedToHuman { get; set; } = false;
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatIntention> Intentions { get; set; } = new List<ChatIntention>();
    }
}