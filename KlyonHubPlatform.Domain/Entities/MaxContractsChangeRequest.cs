using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class MaxContractsChangeRequest
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        public int RequestedMaxContracts { get; set; }
        public int PreviousMaxContracts { get; set; }
        public CapacityApprovalStatus Status { get; set; } = CapacityApprovalStatus.Pending;
        public string? Justification { get; set; }
        public string? RejectionReason { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? Reviewer { get; set; }
    }
}