namespace KlyonHubPlatform.Api.ViewModels
{
    public class MarketplaceCardVm
    {
        public int CoworkingAddressId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string CityState { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public bool IsAvailable { get; set; }
        public int CurrentContracts { get; set; }
        public int MaxContracts { get; set; }
        public int MaxPeople { get; set; }
        public int? MainPhotoId { get; set; }
        public bool OffersSharedRoom { get; set; }
        public decimal? SharedRoomPricePerHour { get; set; }
        public bool OffersPrivateRoom { get; set; }
        public decimal? PrivateRoomPricePerHour { get; set; }
    }
}