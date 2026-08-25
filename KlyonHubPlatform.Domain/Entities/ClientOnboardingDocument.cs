using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class ClientOnboardingDocument
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int ContractId { get; set; }
        public ClientOnboardingDocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public ClientOnboardingDocumentStatus Status { get; set; } = ClientOnboardingDocumentStatus.Pending;
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual Client Client { get; set; } = null!;
        public virtual Contract Contract { get; set; } = null!;
        public virtual User? ReviewedByUser { get; set; }
    }
}
