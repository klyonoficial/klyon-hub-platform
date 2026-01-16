namespace KlyonEF.Models
{
    public class Transfer
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public decimal Amount { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public string? ExternalTransferId { get; set; }
        public string? Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }

    public enum TransferStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled
    }
}
