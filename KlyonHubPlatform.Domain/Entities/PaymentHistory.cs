namespace KlyonHubPlatform.Domain.Entities
{
    public class PaymentHistory
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }
        public string Action { get; set; } = string.Empty; // PaymentHistoryActions (Approved, Rejected, ContractCreated, ReceiptUploaded, Cancelled)
        public string? RejectionReason { get; set; }
        public int? ActionBy { get; set; } // UserId do admin/parceiro que aprovou; null = confirmação automática (ex: PIX)
        public DateTime ActionAt { get; set; }

        // Navigation properties
        public virtual Payment Payment { get; set; } = null!;
        public virtual User? ActionByUser { get; set; }
    }
}

