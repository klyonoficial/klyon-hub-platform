using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço para gerenciar fechamentos mensais de repasses
    /// </summary>
    public interface IMonthlySettlementService
    {
        #region Fechamento Mensal

        /// <summary>
        /// Obtém ou cria o fechamento mensal aberto para um parceiro
        /// </summary>
        Task<MonthlySettlement> GetOrCreateOpenSettlementAsync(int coworkingId, int month, int year);

        /// <summary>
        /// Adiciona uma comissão aprovada ao fechamento mensal
        /// </summary>
        Task AddCommissionToSettlementAsync(int commissionId);

        /// <summary>
        /// Fecha o mês e prepara para aguardar NF do parceiro
        /// Chamado automaticamente no primeiro dia do mês seguinte
        /// </summary>
        Task<MonthlySettlement> CloseMonthAsync(int coworkingId, int month, int year);

        /// <summary>
        /// Fecha todos os meses abertos (job automático)
        /// </summary>
        Task CloseAllOpenMonthsAsync();

        /// <summary>
        /// Obtém fechamentos de um parceiro
        /// </summary>
        Task<List<MonthlySettlement>> GetSettlementsByCoworkingAsync(int coworkingId);

        /// <summary>
        /// Obtém fechamentos aguardando NF
        /// </summary>
        Task<List<MonthlySettlement>> GetSettlementsAwaitingInvoiceAsync();

        /// <summary>
        /// Obtém fechamentos com NF para validar (admin)
        /// </summary>
        Task<List<MonthlySettlement>> GetSettlementsWithInvoiceToValidateAsync();

        /// <summary>
        /// Obtém fechamentos prontos para transferência
        /// </summary>
        Task<List<MonthlySettlement>> GetSettlementsReadyForTransferAsync();

        /// <summary>
        /// Obtém um fechamento por ID
        /// </summary>
        Task<MonthlySettlement?> GetSettlementByIdAsync(int settlementId);

        #endregion

        #region Nota Fiscal do Parceiro

        /// <summary>
        /// Faz upload da NF do parceiro
        /// </summary>
        Task<PartnerInvoice> UploadPartnerInvoiceAsync(int settlementId, int coworkingId, 
            Stream pdfStream, string pdfFileName, Stream? xmlStream, string? xmlFileName, 
            string? invoiceNumber, decimal? invoiceAmount, string? partnerNotes);

        /// <summary>
        /// Valida automaticamente a NF (compara valores, CNPJ, etc.)
        /// </summary>
        Task<(bool IsValid, string Message)> ValidateInvoiceAutomaticallyAsync(int invoiceId);

        /// <summary>
        /// Admin aprova a NF manualmente
        /// </summary>
        Task<PartnerInvoice> ApproveInvoiceAsync(int invoiceId, int adminUserId, string? notes);

        /// <summary>
        /// Admin rejeita a NF
        /// </summary>
        Task<PartnerInvoice> RejectInvoiceAsync(int invoiceId, int adminUserId, string rejectionReason);

        /// <summary>
        /// Emite NF automaticamente via Focus NFe (quando parceiro não envia)
        /// </summary>
        Task<PartnerInvoice> GenerateAutomaticInvoiceAsync(int settlementId);

        /// <summary>
        /// Verifica fechamentos com prazo de NF expirado e emite automaticamente
        /// </summary>
        Task ProcessExpiredInvoiceDeadlinesAsync();

        /// <summary>
        /// Obtém NF de um fechamento
        /// </summary>
        Task<PartnerInvoice?> GetInvoiceBySettlementAsync(int settlementId);

        #endregion

        #region Transferência

        /// <summary>
        /// Agenda a transferência para o último dia útil do mês
        /// </summary>
        Task ScheduleTransferAsync(int settlementId);

        /// <summary>
        /// Executa transferências agendadas para hoje via PagBank PIX
        /// </summary>
        Task ProcessScheduledTransfersAsync();

        /// <summary>
        /// Executa uma transferência específica
        /// </summary>
        Task<MonthlySettlement> ExecuteTransferAsync(int settlementId);

        /// <summary>
        /// Processa callback de transferência do PagBank
        /// </summary>
        Task ProcessTransferCallbackAsync(string externalTransferId, bool success, string? failureReason);

        #endregion

        #region Utilitários

        /// <summary>
        /// Calcula o último dia útil de um mês
        /// </summary>
        DateTime GetLastBusinessDayOfMonth(int year, int month);

        /// <summary>
        /// Calcula a data limite para envio da NF (5 dias úteis após fechamento)
        /// </summary>
        DateTime CalculateInvoiceDeadline(DateTime closingDate);

        /// <summary>
        /// Obtém resumo de fechamentos para dashboard
        /// </summary>
        Task<SettlementDashboardSummary> GetDashboardSummaryAsync(int? coworkingId = null);

        #endregion
    }

    /// <summary>
    /// Resumo para dashboard de fechamentos
    /// </summary>
    public class SettlementDashboardSummary
    {
        public int TotalOpen { get; set; }
        public int TotalAwaitingInvoice { get; set; }
        public int TotalInvoiceReceived { get; set; }
        public int TotalInvoiceApproved { get; set; }
        public int TotalScheduled { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalFailed { get; set; }
        public decimal TotalAmountPending { get; set; }
        public decimal TotalAmountTransferred { get; set; }
    }
}
