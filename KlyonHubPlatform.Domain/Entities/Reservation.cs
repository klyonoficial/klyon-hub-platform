using KlyonHubPlatform.Domain.Core.Enums;
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
        public string RoomType { get; set; } = "Shared";
        public int? CoworkingRoomId { get; set; }
        public decimal TotalValue { get; set; }
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
        public DateTime? ClientConfirmedAt { get; set; }
        public DateTime? CoworkingConfirmedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual CoworkingRoom? CoworkingRoom { get; set; }
    }
}