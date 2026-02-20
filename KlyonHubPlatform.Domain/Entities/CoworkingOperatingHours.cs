namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingOperatingHours
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
    }
}