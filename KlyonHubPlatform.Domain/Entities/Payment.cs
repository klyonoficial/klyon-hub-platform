using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int? SubscriptionId { get; set; } // Nullable para permitir pagamentos de reservas sem subscription
        public int? ReservationId { get; set; } // Para pagamentos de reservas
        public string ExternalPaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentMethod Method { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? TransactionId { get; set; }
        public string? PixCode { get; set; }
        public DateTime? PixExpiresAt { get; set; }
        public string? BoletoCode { get; set; }
        public string? BoletoBarcode { get; set; }
        public DateTime? BoletoExpiresAt { get; set; }
        public string? FailureReason { get; set; }
        public string? ReceiptFilePath { get; set; }
        public string? ValidationNotes { get; set; }
        public int? ValidatedBy { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Subscription? Subscription { get; set; }
        public virtual Reservation? Reservation { get; set; }
        public virtual User? Validator { get; set; }
    }
}

