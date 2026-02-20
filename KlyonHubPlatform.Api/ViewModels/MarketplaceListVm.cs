namespace KlyonHubPlatform.Api.ViewModels
{
    public class MarketplaceListVm
    {
        public MarketplaceSearchVm Search { get; set; } = new();
        public List<string> AvailableStates { get; set; } = new();
        public List<string> AvailableCities { get; set; } = new();
        public List<MarketplaceCardVm> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}