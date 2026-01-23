using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Tabela intermediária: Relaciona Parceiro (Coworking) com Serviço e define percentagem fechada em contrato
    /// </summary>
    public class PartnerService
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int ServiceId { get; set; }
        /// <summary>Percentagem fechada em contrato (definida pelo administrador, não pode ser alterada pelo parceiro)</summary>
        public decimal Percentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Campos para solicitação de remoção
        /// <summary>Status da solicitação de remoção</summary>
        public RemovalRequestStatus RemovalRequestStatus { get; set; } = RemovalRequestStatus.None;
        /// <summary>Data em que a solicitação de remoção foi feita</summary>
        public DateTime? RemovalRequestedAt { get; set; }
        /// <summary>Data em que a solicitação foi revisada pelo administrador</summary>
        public DateTime? RemovalReviewedAt { get; set; }
        /// <summary>ID do administrador que revisou a solicitação</summary>
        public int? RemovalReviewedBy { get; set; }
        /// <summary>Motivo da rejeição da solicitação de remoção</summary>
        public string? RemovalRejectionReason { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Service Service { get; set; } = null!;
    }
}

