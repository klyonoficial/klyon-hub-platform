namespace KlyonHubPlatform.Domain.Entities
{
    public class Interest
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        public DateTime? AllocationDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Contacted, Converted, Rejected
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public int? ContactedByUserId { get; set; }
        public DateTime? ContactedAt { get; set; }

        // Navigation properties
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? ContactedByUser { get; set; }
    }
}
