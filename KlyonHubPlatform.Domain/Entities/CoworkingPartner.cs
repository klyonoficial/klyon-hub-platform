namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingPartner
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CPF { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }
        public decimal? SharePercentage { get; set; }
        public string? PhotoPath { get; set; }
        public bool IsPrincipal { get; set; }
        public bool IsContractSigner { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
    }
}