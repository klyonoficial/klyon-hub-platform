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
        public string BillingCycle { get; set; } = "Monthly";
        public DateTime NextBillingDate { get; set; }
        public DateTime? LastBillingDate { get; set; }
        public int FailedPaymentAttempts { get; set; } = 0;
        public DateTime? SuspendedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        /// <summary>ID da autorização PIX Automático no Asaas (quando recorrência é PIX anual).</summary>
        public string? PixAutomaticAuthorizationId { get; set; }
        /// <summary>Payload do QR Code PIX Automático (guarda último código válido para reaproveitar ao alternar tipo de PIX).</summary>
        public string? PixAutomaticPayload { get; set; }
        /// <summary>Data de expiração do QR PIX Automático armazenado em PixAutomaticPayload.</summary>
        public DateTime? PixAutomaticPayloadExpiresAt { get; set; }
        /// <summary>Identificador de conciliação do PIX Automático (para webhook e exibição consistente).</summary>
        public string? PixAutomaticConciliationIdentifier { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Contract Contract { get; set; } = null!;
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}