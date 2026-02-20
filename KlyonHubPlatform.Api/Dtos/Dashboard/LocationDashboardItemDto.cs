namespace KlyonHubPlatform.Api.Dtos.Dashboard
{
    public class LocationDashboardItemDto
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Region { get; set; }
        public bool IsActive { get; set; }
    }
}
