namespace KlyonHubPlatform.Application.DTOs.Dashboard
{
    public class PaymentDashboardItemDto
    {
        public int Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }
}
