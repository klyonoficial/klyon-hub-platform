namespace KlyonEF.Models
{
    public class Document
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public DocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }

        // Navigation properties
        public virtual Contract Contract { get; set; } = null!;
    }

    public enum DocumentType
    {
        Contract,
        Receipt,
        TaxDeclaration,
        TermsOfUse
    }
}
