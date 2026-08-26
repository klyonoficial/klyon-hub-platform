using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.DTOs.PromoCode
{
    public class PromoCodeCreateInput
    {
        public string Code { get; set; } = string.Empty;
        public PromoDiscountType Type { get; set; }
        public decimal Value { get; set; }
        public PromoScope Scope { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int? MaxRedemptions { get; set; }
        /// <summary>Obrigatório quando Scope = Coworking.</summary>
        public int? CoworkingId { get; set; }
        public int CreatedByUserId { get; set; }
        public bool IsAdmin { get; set; }
    }
}
