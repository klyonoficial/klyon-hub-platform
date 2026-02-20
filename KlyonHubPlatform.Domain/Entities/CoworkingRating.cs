namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingRating
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int? ClientId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Client? Client { get; set; }
    }
}