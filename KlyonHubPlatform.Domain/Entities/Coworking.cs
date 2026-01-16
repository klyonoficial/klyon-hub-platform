namespace KlyonHubPlatform.Domain.Entities
{
    public class Coworking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;
        public string? TradeName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? BankName { get; set; }
        public string? BankAgency { get; set; }
        public string? BankAccount { get; set; }
        public string? BankAccountType { get; set; }
        public string? BankPixKey { get; set; }
        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? LinkedIn { get; set; }
        public string? Address { get; set; }
        public string? AddressNumber { get; set; }
        public string? AddressComplement { get; set; }
        public string? AddressNeighborhood { get; set; }
        public string? AddressCity { get; set; }
        public string? AddressState { get; set; }
        public string? AddressZipCode { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? SecondaryEmail { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsPartner { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<CoworkingAddress> Addresses { get; set; } = new List<CoworkingAddress>();
        public virtual ICollection<CoworkingPhoto> Photos { get; set; } = new List<CoworkingPhoto>();
        public virtual ICollection<CoworkingPartner> Partners { get; set; } = new List<CoworkingPartner>();
    }
}
