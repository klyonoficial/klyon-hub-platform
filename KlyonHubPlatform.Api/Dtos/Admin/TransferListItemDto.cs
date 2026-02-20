namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class TransferListItemDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Description { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
