namespace KlyonHubPlatform.Domain.Entities
{
    public class Reservation
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int CoworkingAddressId { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string RoomType { get; set; } = "Shared"; // "Shared" ou "Private"
        public decimal TotalValue { get; set; }
        public int CreditsUsed { get; set; }
        public string Status { get; set; } = "Confirmed"; // "Confirmed", "Cancelled", "Completed"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
    }
}
