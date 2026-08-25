using KlyonHubPlatform.Application.DTOs.AppHome;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAppHomeService
    {
        Task<int?> GetSolePublishedAddressIdAsync(CancellationToken cancellationToken = default);
        Task<MarketplaceListResult?> GetMarketplaceListAsync(MarketplaceSearchDto search, CancellationToken cancellationToken = default);
        Task<MarketplaceDetailsResult?> GetMarketplaceDetailsAsync(int addressId, int? userId, CancellationToken cancellationToken = default);
        Task<SubmitInterestResult> SubmitInterestAsync(SubmitInterestInput input, CancellationToken cancellationToken = default);
        Task<CreateReservationResult> CreateReservationAsync(CreateReservationInput input, CancellationToken cancellationToken = default);
        Task<object?> GetAvailabilityForMonthAsync(int addressId, string roomType, int year, int month, CancellationToken cancellationToken = default);
        Task<object?> GetAvailabilityAsync(int addressId, string roomType, DateTime? date, CancellationToken cancellationToken = default);
    }
}
