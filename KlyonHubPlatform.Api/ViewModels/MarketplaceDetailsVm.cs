using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Api.ViewModels
{
    public class MarketplaceDetailsVm
    {
        public CoworkingAddress Address { get; set; } = null!;
        public Coworking Coworking { get; set; } = null!;
        public int? MainPhotoId { get; set; }
        public List<int> PhotoIds { get; set; } = new();
        public string? ReturnUrl { get; set; }
    }
}