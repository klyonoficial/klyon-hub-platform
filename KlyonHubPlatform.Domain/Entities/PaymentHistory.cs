namespace KlyonHubPlatform.Domain.Entities
{
    public class PaymentHistory
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public int? ActionBy { get; set; }
        public DateTime ActionAt { get; set; }
        public virtual Payment Payment { get; set; } = null!;
        public virtual User? ActionByUser { get; set; }
    }
}