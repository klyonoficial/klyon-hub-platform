namespace KlyonHubPlatform.Domain.Entities
{
    public class Commission
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }
        public int CoworkingId { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public bool IsTransferred { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TransferredAt { get; set; }

        // Navigation properties
        public virtual Payment Payment { get; set; } = null!;
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
