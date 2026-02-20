using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class FiscalNote
    {
        public int Id { get; set; }
        public string Type { get; set; } = FiscalNoteType.Default;
        public int? ReservationId { get; set; }
        public int? ContractId { get; set; }
        public int? PaymentId { get; set; }
        public int? CoworkingId { get; set; }
        public int? ClientId { get; set; }
        public string? Number { get; set; }
        public string? VerificationCode { get; set; }
        public string? AccessKey { get; set; }
        public decimal Amount { get; set; }
        public decimal? CommissionAmount { get; set; }
        public decimal? ServiceAmount { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public FiscalNoteStatus Status { get; set; } = FiscalNoteStatus.Pendente;
        public string? ErrorMessage { get; set; }
        public string? XmlContent { get; set; }
        public string? ViewUrl { get; set; }
        public string? PdfUrl { get; set; }
        public string? AuthorizationProtocol { get; set; }
        public DateTime? AuthorizedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public string? IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Reservation? Reservation { get; set; }
        public virtual Contract? Contract { get; set; }
        public virtual Payment? Payment { get; set; }
        public virtual Coworking? Coworking { get; set; }
        public virtual Client? Client { get; set; }
    }
}