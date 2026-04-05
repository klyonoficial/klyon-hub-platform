using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class Contract
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int CoworkingAddressId { get; set; }
        /// <summary>Serviço contratado (ex.: Endereço Fiscal, Endereço Comercial).</summary>
        public int ServiceId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? LastExpiryNotificationSent { get; set; }
        public ContractStatus Status { get; set; } = ContractStatus.Active;
        public decimal MonthlyValue { get; set; }
        public string? ContractDocumentPath { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancellationRequestedAt { get; set; }
        public string? CancellationRequestReason { get; set; }
        public int? CancellationApprovedBy { get; set; }
        public DateTime? CancellationApprovedAt { get; set; }
        public string? CancellationRejectionReason { get; set; }
        public decimal SnapshotMonthlyValue { get; set; }
        public decimal SnapshotPlatformCommissionPercentage { get; set; }
        public decimal SnapshotCoworkingCommissionPercentage { get; set; }
        public string SnapshotAddress { get; set; } = string.Empty;
        public decimal SnapshotCommissionPercentage { get; set; }
        public int SnapshotCapacityConsumed { get; set; }
        public bool IsAssistedSale { get; set; }
        public int? AssistedSaleAdminId { get; set; }
        /// <summary>Tipo de plano no cartão (1x anual, mensal ou 12x) para definir recorrência da cobrança.</summary>
        public ContractCardPlanType? CardPlanType { get; set; }
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual Service? Service { get; set; }
        public virtual Subscription? Subscription { get; set; }
        public virtual User? AssistedSaleAdmin { get; set; }
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}