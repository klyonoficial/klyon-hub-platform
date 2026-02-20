using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class RequestRejectionUpdateDto
    {
        public CapacityApprovalStatus Status { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
        public int ReviewedBy { get; set; }
        public DateTime ReviewedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}