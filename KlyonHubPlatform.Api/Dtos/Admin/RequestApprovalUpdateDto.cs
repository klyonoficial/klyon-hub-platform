using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class RequestApprovalUpdateDto
    {
        public CapacityApprovalStatus Status { get; set; }
        public int ReviewedBy { get; set; }
        public DateTime ReviewedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static RequestApprovalUpdateDto CreateApproved(int userId, DateTime now) => new()
        {
            Status = CapacityApprovalStatus.Approved,
            ReviewedBy = userId,
            ReviewedAt = now,
            UpdatedAt = now
        };
    }
}
