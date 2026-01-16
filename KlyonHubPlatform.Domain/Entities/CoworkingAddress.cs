namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingAddress
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int LocationId { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? Complement { get; set; }
        public string Neighborhood { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int MaxContracts { get; set; } = 1;
        public int CurrentContracts { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Location Location { get; set; } = null!;
    }
}
