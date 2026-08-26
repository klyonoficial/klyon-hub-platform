using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.DTOs.Admin;

public class CardPriceRequestDetailDto
{
    public int Id { get; set; }
    public CapacityApprovalStatus Status { get; set; }
    public string CoworkingName { get; set; } = string.Empty;
    public int CoworkingId { get; set; }
    public string AddressLabel { get; set; } = string.Empty;
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
    public string CreatedAtFormatted { get; set; } = string.Empty;
}
