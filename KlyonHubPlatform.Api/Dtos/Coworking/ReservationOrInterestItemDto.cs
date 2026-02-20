namespace KlyonHubPlatform.Api.Dtos.Coworking
{
    public class ReservationOrInterestItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public object? StartTime { get; set; }
        public object? EndTime { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public object? Address { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string RoomType { get; set; } = string.Empty;
        public decimal? TotalValue { get; set; }
        public DateTime? ClientConfirmedAt { get; set; }
        public DateTime? CoworkingConfirmedAt { get; set; }
    }
}