namespace KlyonHubPlatform.Application.DTOs.Dashboard
{
    public class ReservationDashboardItemDto
    {
        public int Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string CoworkingName { get; set; } = string.Empty;
        public string ReservationDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
    }
}
