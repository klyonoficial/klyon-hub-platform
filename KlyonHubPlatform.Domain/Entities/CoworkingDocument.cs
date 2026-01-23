using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingDocument
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public CoworkingDocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsValid { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}

