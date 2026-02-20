using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class MonthlySettlement
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int ReferenceMonth { get; set; }
        public int ReferenceYear { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalGrossAmount { get; set; }
        public decimal TotalPlatformCommission { get; set; }
        public int CommissionCount { get; set; }
        public SettlementStatus Status { get; set; } = SettlementStatus.Open;
        public DateTime? ClosedAt { get; set; }
        public DateTime? ScheduledTransferDate { get; set; }
        public DateTime? InvoiceDeadline { get; set; }
        public DateTime? TransferredAt { get; set; }
        public string? ExternalTransferId { get; set; }
        public string? PixKey { get; set; }
        public string? FailureReason { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual User? ApprovedByUser { get; set; }
        public virtual ICollection<SettlementCommission> SettlementCommissions { get; set; } = new List<SettlementCommission>();
        public virtual ICollection<PartnerInvoice> PartnerInvoices { get; set; } = new List<PartnerInvoice>();
    }
    public class SettlementCommission
    {
        public int Id { get; set; }
        public int MonthlySettlementId { get; set; }
        public int CommissionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual MonthlySettlement MonthlySettlement { get; set; } = null!;
        public virtual Commission Commission { get; set; } = null!;
    }
}