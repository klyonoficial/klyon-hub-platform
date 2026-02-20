namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class CommissionListItemDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public bool IsTransferred { get; set; }
        public DateTime? TransferredAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
    }
}
