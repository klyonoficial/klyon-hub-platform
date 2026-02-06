using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class CreditPurchase
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int CreditPlanId { get; set; }
        public decimal AmountPaid { get; set; }
        public int CreditsReceived { get; set; }
        public int BonusCredits { get; set; }
        public CreditPurchaseStatus Status { get; set; } = CreditPurchaseStatus.Pending;
        public PaymentMethod? PaymentMethod { get; set; }
        public string? ExternalPaymentId { get; set; }
        public string? PixCode { get; set; }
        public DateTime? PixExpiresAt { get; set; }
        public string? BoletoCode { get; set; }
        public string? BoletoBarcode { get; set; }
        public DateTime? BoletoExpiresAt { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual CreditPlan CreditPlan { get; set; } = null!;
    }
}

