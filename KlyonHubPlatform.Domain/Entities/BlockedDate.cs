namespace KlyonHubPlatform.Domain.Entities
{
    public class BlockedDate
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public DateTime BlockedDateValue { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool BlocksFullDay { get; set; } = true;
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
    }
}