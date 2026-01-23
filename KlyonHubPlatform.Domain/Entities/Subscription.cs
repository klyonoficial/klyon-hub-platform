using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class Subscription
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string ExternalSubscriptionId { get; set; } = string.Empty;
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        public decimal MonthlyAmount { get; set; }
        /// <summary>Ciclo de cobrança: ex. Monthly, Yearly. Obrigatório se a coluna existir no BD.</summary>
        public string BillingCycle { get; set; } = "Monthly";
        public DateTime NextBillingDate { get; set; }
        public DateTime? LastBillingDate { get; set; }
        public int FailedPaymentAttempts { get; set; } = 0;
        public DateTime? SuspendedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Contract Contract { get; set; } = null!;
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}

