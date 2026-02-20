namespace KlyonHubPlatform.Application.DTOs.AppHome
{
    public class MarketplaceListResult
    {
        public MarketplaceSearchDto Search { get; set; } = new();
        public List<string> AvailableStates { get; set; } = new();
        public List<string> AvailableCities { get; set; } = new();
        public List<MarketplaceCardDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public Dictionary<int, bool> CoworkingProducts { get; set; } = new();
    }
}
