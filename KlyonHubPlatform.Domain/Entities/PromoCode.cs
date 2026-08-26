using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class PromoCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public PromoDiscountType Type { get; set; }
        public decimal Value { get; set; }
        public PromoScope Scope { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsActive { get; set; } = true;
        public int? MaxRedemptions { get; set; }
        public int RedemptionCount { get; set; }
        /// <summary>Métodos de pagamento permitidos (nomes do enum PaymentMethod, separados por vírgula). Null/vazio = todos.</summary>
        public string? AllowedPaymentMethods { get; set; }
        /// <summary>Null quando escopo global (somente admin).</summary>
        public int? CoworkingId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual Coworking? Coworking { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
