using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.DTOs.PartnerListing
{
    public class PartnerListingResult
    {
        public CoworkingAddress Address { get; set; } = null!;
        public List<CoworkingPhoto> Photos { get; set; } = new();
        public List<PartnerTrustDocumentDto> TrustDocuments { get; set; } = new();
        public bool IsSolePublishedAddress { get; set; }
    }

    public class PartnerTrustDocumentDto
    {
        public CoworkingDocumentType Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int? DocumentId { get; set; }
    }

    public class PartnerTrustDocumentFile
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
    }
}
