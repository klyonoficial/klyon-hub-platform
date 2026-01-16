namespace KlyonEF.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int CoworkingAddressId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ContractStatus Status { get; set; } = ContractStatus.Active;
        public decimal MonthlyValue { get; set; }
        public string? ContractDocumentPath { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual Subscription? Subscription { get; set; }
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }

    public enum ContractStatus
    {
        Active,
        Suspended,
        Cancelled,
        Expired
    }
}
