using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class CardPriceChangeRequest
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        public decimal? RequestedBasePrice { get; set; }
        public decimal? PreviousBasePrice { get; set; }
        public decimal? RequestedCardMonthlySurchargePercent { get; set; }
        public decimal? PreviousCardMonthlySurchargePercent { get; set; }
        public decimal? RequestedCardPrice1x { get; set; }
        public decimal? RequestedCardPriceMonthly { get; set; }
        public decimal? RequestedCardPrice12x { get; set; }
        public decimal? PreviousCardPrice1x { get; set; }
        public decimal? PreviousCardPriceMonthly { get; set; }
        public decimal? PreviousCardPrice12x { get; set; }
        public CapacityApprovalStatus Status { get; set; } = CapacityApprovalStatus.Pending;
        public string? RejectionReason { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? Reviewer { get; set; }
    }
}