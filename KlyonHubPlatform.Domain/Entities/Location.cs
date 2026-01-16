namespace KlyonHubPlatform.Domain.Entities
{
    public class Location
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Region { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<LocationCommission> LocationCommissions { get; set; } = new List<LocationCommission>();
    }
}
