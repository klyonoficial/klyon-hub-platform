namespace KlyonHubPlatform.Domain.Entities
{
    public class AddressDeclaration
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string DeclarationNumber { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? DigitalSignature { get; set; }
        public bool IsValid { get; set; } = true;
        public DateTime? InvalidatedAt { get; set; }
        public string? InvalidationReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Contract Contract { get; set; } = null!;
    }
}