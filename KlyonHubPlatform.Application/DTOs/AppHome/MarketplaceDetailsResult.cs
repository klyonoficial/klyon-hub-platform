using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.DTOs.AppHome
{
    public class MarketplaceDetailsResult
    {
        public CoworkingAddress? Address { get; set; }
        public Coworking? Coworking { get; set; }
        public List<int> PhotoIds { get; set; } = new();
        public int? MainPhotoId { get; set; }
        public string? ReturnUrl { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public List<CoworkingRating> Ratings { get; set; } = new();
        public bool IsClientLogged { get; set; }
        public int AvailableCredits { get; set; }
        public bool OffersSharedRoom { get; set; }
        public bool OffersPrivateRoom { get; set; }
        public decimal? SharedRoomPricePerHour { get; set; }
        public int? SharedRoomMaxSeats { get; set; }
        public decimal? PrivateRoomPricePerHour { get; set; }
        public int? PrivateRoomMaxSeats { get; set; }
    }
}
