namespace KlyonHubPlatform.Api.Dtos.Dashboard
{
    public class CardPriceRequestDashboardItemDto
    {
        public int Id { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public int CoworkingId { get; set; }
        public string AddressLabel { get; set; } = string.Empty;
        public decimal? RequestedCardPrice1x { get; set; }
        public decimal? RequestedCardPriceMonthly { get; set; }
        public decimal? RequestedCardPrice12x { get; set; }
        public decimal? PreviousCardPrice1x { get; set; }
        public decimal? PreviousCardPriceMonthly { get; set; }
        public decimal? PreviousCardPrice12x { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
