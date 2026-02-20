using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IMonthlySettlementService
    {
        #region Fechamento Mensal
        Task<MonthlySettlement> GetOrCreateOpenSettlementAsync(int coworkingId, int month, int year);
        Task AddCommissionToSettlementAsync(int commissionId);
        Task<MonthlySettlement> CloseMonthAsync(int coworkingId, int month, int year);
        Task CloseAllOpenMonthsAsync();
        Task<List<MonthlySettlement>> GetSettlementsByCoworkingAsync(int coworkingId);
        Task<List<MonthlySettlement>> GetSettlementsAwaitingInvoiceAsync();
        Task<List<MonthlySettlement>> GetSettlementsWithInvoiceToValidateAsync();
        Task<List<MonthlySettlement>> GetSettlementsReadyForTransferAsync();
        Task<MonthlySettlement?> GetSettlementByIdAsync(int settlementId);
        #endregion
        #region Nota Fiscal do Parceiro
        Task<PartnerInvoice> UploadPartnerInvoiceAsync(int settlementId, int coworkingId, 
            Stream pdfStream, string pdfFileName, Stream? xmlStream, string? xmlFileName, 
            string? invoiceNumber, decimal? invoiceAmount, string? partnerNotes);
        Task<(bool IsValid, string Message)> ValidateInvoiceAutomaticallyAsync(int invoiceId);
        Task<PartnerInvoice> ApproveInvoiceAsync(int invoiceId, int adminUserId, string? notes);
        Task<PartnerInvoice> RejectInvoiceAsync(int invoiceId, int adminUserId, string rejectionReason);
        Task<PartnerInvoice> GenerateAutomaticInvoiceAsync(int settlementId);
        Task ProcessExpiredInvoiceDeadlinesAsync();
        Task<PartnerInvoice?> GetInvoiceBySettlementAsync(int settlementId);
        #endregion
        #region Transferência
        Task ScheduleTransferAsync(int settlementId);
        Task ProcessScheduledTransfersAsync();
        Task<MonthlySettlement> ExecuteTransferAsync(int settlementId);
        Task ProcessTransferCallbackAsync(string externalTransferId, bool success, string? failureReason);
        #endregion
        #region Utilitários
        DateTime GetLastBusinessDayOfMonth(int year, int month);
        DateTime CalculateInvoiceDeadline(DateTime closingDate);
        Task<SettlementDashboardSummary> GetDashboardSummaryAsync(int? coworkingId = null);
        #endregion
    }
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