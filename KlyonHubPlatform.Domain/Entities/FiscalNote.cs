using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Nota Fiscal emitida via SEFAZ ES
    /// </summary>
    public class FiscalNote
    {
        public int Id { get; set; }
        /// <summary>Tipo: Intermediacao (plataforma) ou Servico (parceiro)</summary>
        public string Type { get; set; } = "Intermediacao"; // "Intermediacao" ou "Servico"
        
        // Relacionamentos
        /// <summary>ID da reserva (se for NF de intermediação de reserva)</summary>
        public int? ReservationId { get; set; }
        /// <summary>ID do contrato (se for NF de intermediação de endereço fiscal)</summary>
        public int? ContractId { get; set; }
        /// <summary>ID do pagamento relacionado</summary>
        public int? PaymentId { get; set; }
        /// <summary>ID do parceiro (coworking)</summary>
        public int? CoworkingId { get; set; }
        /// <summary>ID do cliente</summary>
        public int? ClientId { get; set; }
        
        // Dados da NF
        /// <summary>Número da NF emitida pela SEFAZ</summary>
        public string? Number { get; set; }
        /// <summary>Código de verificação da NF</summary>
        public string? VerificationCode { get; set; }
        /// <summary>Chave de acesso da NF (NFe)</summary>
        public string? AccessKey { get; set; }
        /// <summary>Valor da nota fiscal</summary>
        public decimal Amount { get; set; }
        /// <summary>Valor da comissão (se for NF de intermediação)</summary>
        public decimal? CommissionAmount { get; set; }
        /// <summary>Valor do serviço total (se for NF de serviço)</summary>
        public decimal? ServiceAmount { get; set; }
        /// <summary>Data de emissão</summary>
        public DateTime IssueDate { get; set; }
        /// <summary>Data de vencimento</summary>
        public DateTime? DueDate { get; set; }
        
        // Status
        /// <summary>Status: Pendente, Emitida, Cancelada, Erro, etc.</summary>
        public FiscalNoteStatus Status { get; set; } = FiscalNoteStatus.Pendente;
        /// <summary>Mensagem de erro (se houver)</summary>
        public string? ErrorMessage { get; set; }
        /// <summary>XML da NF (retornado pela SEFAZ)</summary>
        public string? XmlContent { get; set; }
        /// <summary>URL para visualização da NF</summary>
        public string? ViewUrl { get; set; }
        /// <summary>URL para download do PDF</summary>
        public string? PdfUrl { get; set; }
        
        // Dados adicionais
        /// <summary>Protocolo de autorização da SEFAZ</summary>
        public string? AuthorizationProtocol { get; set; }
        /// <summary>Data de autorização</summary>
        public DateTime? AuthorizedAt { get; set; }
        /// <summary>Data de cancelamento (se cancelada)</summary>
        public DateTime? CancelledAt { get; set; }
        /// <summary>Justificativa do cancelamento</summary>
        public string? CancellationReason { get; set; }
        
        /// <summary>Chave de idempotência: evita NF duplicada para mesmo reserva/contrato + tipo (ex: INTERMEDIACAO-R-123, INTERMEDIACAO-C-456)</summary>
        public string? IdempotencyKey { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual Reservation? Reservation { get; set; }
        public virtual Contract? Contract { get; set; }
        public virtual Payment? Payment { get; set; }
        public virtual Coworking? Coworking { get; set; }
        public virtual Client? Client { get; set; }
    }
}

