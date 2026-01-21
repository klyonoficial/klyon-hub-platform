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
        public string RoomType { get; set; } = "Shared"; // "Shared", "Private", "Auditorium"
        /// <summary>Quando preenchido, a reserva é por sala individual; conflito verificado por sala.</summary>
        public int? CoworkingRoomId { get; set; }
        public decimal TotalValue { get; set; }
        public int CreditsUsed { get; set; }
        public string Status { get; set; } = "Confirmed"; // "Confirmed", "Cancelled", "Completed"
        /// <summary>Cliente confirmou que realizou/utilizou a reserva.</summary>
        public DateTime? ClientConfirmedAt { get; set; }
        /// <summary>Coworking confirmou que o cliente compareceu e utilizou o espaço.</summary>
        public DateTime? CoworkingConfirmedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual CoworkingRoom? CoworkingRoom { get; set; }
    }
}
