using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Solicitação de alteração de preços de venda no cartão de um endereço.
    /// Os preços só são exibidos aos clientes após aprovação administrativa.
    /// </summary>
    public class CardPriceChangeRequest
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        /// <summary>Valor à vista (1x) solicitado pelo parceiro</summary>
        public decimal? RequestedCardPrice1x { get; set; }
        /// <summary>Valor mensal solicitado pelo parceiro</summary>
        public decimal? RequestedCardPriceMonthly { get; set; }
        /// <summary>Valor por parcela (12x) solicitado pelo parceiro</summary>
        public decimal? RequestedCardPrice12x { get; set; }
        /// <summary>Valores anteriores (antes da solicitação)</summary>
        public decimal? PreviousCardPrice1x { get; set; }
        public decimal? PreviousCardPriceMonthly { get; set; }
        public decimal? PreviousCardPrice12x { get; set; }
        /// <summary>Status da solicitação</summary>
        public CapacityApprovalStatus Status { get; set; } = CapacityApprovalStatus.Pending;
        /// <summary>Motivo da reprovação (se aplicável)</summary>
        public string? RejectionReason { get; set; }
        /// <summary>ID do administrador que aprovou/reprovou</summary>
        public int? ReviewedBy { get; set; }
        /// <summary>Data da revisão</summary>
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? Reviewer { get; set; }
    }
}
