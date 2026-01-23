namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingPhoto
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMain { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}

