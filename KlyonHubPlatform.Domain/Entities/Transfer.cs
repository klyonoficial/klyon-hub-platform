using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class Transfer
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        /// <summary>Quando preenchido, é repasse automático de reserva (dupla confirmação).</summary>
        public int? ReservationId { get; set; }
        public decimal Amount { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public string? ExternalTransferId { get; set; }
        public string? Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Reservation? Reservation { get; set; }
    }
}
