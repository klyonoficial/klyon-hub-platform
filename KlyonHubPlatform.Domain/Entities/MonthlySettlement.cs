using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Fechamento mensal de repasses para parceiros
    /// Consolida todas as comissões aprovadas de um mês para transferência
    /// </summary>
    public class MonthlySettlement
    {
        public int Id { get; set; }

        /// <summary>ID do parceiro (coworking)</summary>
        public int CoworkingId { get; set; }

        /// <summary>Mês de referência (1-12)</summary>
        public int ReferenceMonth { get; set; }

        /// <summary>Ano de referência</summary>
        public int ReferenceYear { get; set; }

        /// <summary>Valor total das comissões do parceiro (valor líquido a receber)</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Valor total bruto das vendas</summary>
        public decimal TotalGrossAmount { get; set; }

        /// <summary>Valor total da comissão da plataforma</summary>
        public decimal TotalPlatformCommission { get; set; }

        /// <summary>Quantidade de comissões incluídas</summary>
        public int CommissionCount { get; set; }

        /// <summary>Status atual do fechamento</summary>
        public SettlementStatus Status { get; set; } = SettlementStatus.Open;

        /// <summary>Data do fechamento (quando foi consolidado)</summary>
        public DateTime? ClosedAt { get; set; }

        /// <summary>Data agendada para transferência (último dia útil do mês seguinte)</summary>
        public DateTime? ScheduledTransferDate { get; set; }

        /// <summary>Data limite para envio da NF pelo parceiro</summary>
        public DateTime? InvoiceDeadline { get; set; }

        /// <summary>Data em que a transferência foi executada</summary>
        public DateTime? TransferredAt { get; set; }

        /// <summary>ID externo da transferência (PagBank)</summary>
        public string? ExternalTransferId { get; set; }

        /// <summary>Chave PIX usada para transferência</summary>
        public string? PixKey { get; set; }

        /// <summary>Motivo de falha na transferência (se houver)</summary>
        public string? FailureReason { get; set; }

        /// <summary>ID do usuário admin que aprovou a NF</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>Data da aprovação da NF</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Observações</summary>
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual User? ApprovedByUser { get; set; }
        public virtual ICollection<SettlementCommission> SettlementCommissions { get; set; } = new List<SettlementCommission>();
        public virtual ICollection<PartnerInvoice> PartnerInvoices { get; set; } = new List<PartnerInvoice>();
    }

    /// <summary>
    /// Relacionamento entre fechamento mensal e comissões incluídas
    /// </summary>
    public class SettlementCommission
    {
        public int Id { get; set; }
        public int MonthlySettlementId { get; set; }
        public int CommissionId { get; set; }

        /// <summary>Valor da comissão no momento da inclusão</summary>
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual MonthlySettlement MonthlySettlement { get; set; } = null!;
        public virtual Commission Commission { get; set; } = null!;
    }
}
