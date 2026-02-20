namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class PaymentHistoryItemDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string ActionBy { get; set; } = string.Empty;
        public string ActionAt { get; set; } = string.Empty;
    }
}
