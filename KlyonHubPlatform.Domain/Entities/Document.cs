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
        /// <summary>
        /// UUID do documento na D4Sign (quando enviado para assinatura eletrônica).
        /// </summary>
        public string? D4SignDocumentUuid { get; set; }

        /// <summary>
        /// ID do envelope na Clicksign (quando enviado para assinatura eletrônica via Clicksign).
        /// </summary>
        public string? ClicksignEnvelopeId { get; set; }

        // Navigation properties
        public virtual Contract Contract { get; set; } = null!;
    }
}

