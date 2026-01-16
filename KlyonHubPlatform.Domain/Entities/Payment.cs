using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public string ExternalPaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentMethod Method { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? TransactionId { get; set; }
        public string? PixCode { get; set; }
        public DateTime? PixExpiresAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Subscription Subscription { get; set; } = null!;
    }
}
