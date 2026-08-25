using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.DTOs.Contracts
{
    public class ContractPartnerDocumentDto
    {
        public int DocumentId { get; set; }
        public CoworkingDocumentType Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ContractPartnerDocumentFileDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
    }
}
