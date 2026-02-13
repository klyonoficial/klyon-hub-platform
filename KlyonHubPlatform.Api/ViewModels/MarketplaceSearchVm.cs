namespace KlyonHubPlatform.Api.ViewModels
{
    public class MarketplaceSearchVm
    {
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Neighborhood { get; set; }
        public int? MinPeople { get; set; }
        public int? MaxPeople { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
