using KlyonHubPlatform.Application.DTOs.PartnerListing;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPartnerListingService
    {
        Task<PartnerListingResult?> GetPartnerListingAsync(int coworkingAddressId, CancellationToken cancellationToken = default);
        Task<PartnerTrustDocumentFile?> GetPublicTrustDocumentAsync(
            int coworkingAddressId,
            CoworkingDocumentType type,
            CancellationToken cancellationToken = default);
    }
}
