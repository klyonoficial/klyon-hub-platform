using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class PartnerService
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int ServiceId { get; set; }
        public decimal Percentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public RemovalRequestStatus RemovalRequestStatus { get; set; } = RemovalRequestStatus.None;
        public DateTime? RemovalRequestedAt { get; set; }
        public DateTime? RemovalReviewedAt { get; set; }
        public int? RemovalReviewedBy { get; set; }
        public string? RemovalRejectionReason { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Service Service { get; set; } = null!;
    }
}