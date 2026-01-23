namespace KlyonHubPlatform.Domain.Entities
{
    public class ClientCredit
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int Credits { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
    }
}

