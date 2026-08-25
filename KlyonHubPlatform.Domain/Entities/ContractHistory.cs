namespace KlyonHubPlatform.Domain.Entities
{
    public class ContractHistory
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? RejectionReason { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public DateTime ActionAt { get; set; }
        public virtual Contract Contract { get; set; } = null!;
        public virtual User? ActorUser { get; set; }
    }
}
