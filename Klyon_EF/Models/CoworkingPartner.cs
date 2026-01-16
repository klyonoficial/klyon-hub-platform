namespace KlyonEF.Models
{
    public class CoworkingPartner
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CPF { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; } // Sócio, Diretor, Gerente, etc.
        public decimal? SharePercentage { get; set; } // Percentual de participação
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
