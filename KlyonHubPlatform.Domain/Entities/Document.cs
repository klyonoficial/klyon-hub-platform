using System.ComponentModel.DataAnnotations.Schema;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class Document
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public DocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? D4SignDocumentUuid { get; set; }
        [Column("ClicksignEnvelopeId")]
        public string? ClicksignEnvelopeIdentifier { get; set; }
        public virtual Contract Contract { get; set; } = null!;
    }
}