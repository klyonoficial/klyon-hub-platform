namespace KlyonHubPlatform.Domain.Entities
{
    public class Client
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Address { get; set; }
        public string? AddressNumber { get; set; }
        public string? Neighborhood { get; set; }
        public string? Complement { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? LogoPath { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
        public virtual ICollection<CreditCard> CreditCards { get; set; } = new List<CreditCard>();
        public virtual ICollection<ClientRegistrationDocument> RegistrationDocuments { get; set; } = new List<ClientRegistrationDocument>();
    }
}