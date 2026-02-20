namespace KlyonHubPlatform.Application.DTOs.Dashboard
{
    public class ContractDashboardItemDto
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
    }
}
