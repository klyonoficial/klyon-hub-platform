namespace KlyonHubPlatform.Application.DTOs.AppHome
{
    public class CreateReservationInput
    {
        public int UserId { get; set; }
        public int CoworkingAddressId { get; set; }
        public DateTime ReservationDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string RoomType { get; set; } = "Shared";
    }
}
