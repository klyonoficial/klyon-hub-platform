using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.DTOs.PromoCode
{
    public class PromoCodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public PromoDiscountType Type { get; set; }
        public decimal Value { get; set; }
        public PromoScope Scope { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsActive { get; set; }
        public int? MaxRedemptions { get; set; }
        public int RedemptionCount { get; set; }
        public int? CoworkingId { get; set; }
        public string? CoworkingName { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
