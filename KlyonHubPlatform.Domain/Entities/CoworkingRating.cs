namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingRating
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int? ClientId { get; set; }
        public int Rating { get; set; } // 1 a 5 estrelas
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Client? Client { get; set; }
    }
}

