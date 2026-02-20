using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
        Task<List<Payment>> GetRecentPaymentsAsync(int days = 30, int take = 10);
        Task<ReportFinancialResult> GetFinancialReportAsync(DateTime dateFrom, DateTime dateTo, string? product = null, string? assistedSaleFilter = null);
        Task<List<ClientCreditsReportDto>> GetClientCreditsReportAsync();
        Task<ClientCreditStatementDto> GetClientCreditStatementAsync(int clientId, DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<List<CoworkingRevenueReportDto>> GetCoworkingRevenueReportAsync();
        Task<CoworkingRevenueDetailDto> GetCoworkingRevenueDetailAsync(int coworkingId, DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<CoworkingProductDetailDto> GetCoworkingProductDetailAsync(int coworkingId, string productType, DateTime? dateFrom = null, DateTime? dateTo = null);
    }
    public class ClientCreditStatementDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int CurrentBalance { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<ClientCreditStatementRowDto> Movements { get; set; } = new();
    }
    public class ClientCreditStatementRowDto
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "Entrada";
        public int CreditsDelta { get; set; }
        public decimal Amount { get; set; }
        public int? PaymentId { get; set; }
        public int? ReservationId { get; set; }
    }
    public static class ReportProductFilter
    {
        public const string Todos = "";
        public const string EnderecoFiscal = "EnderecoFiscal";
        public const string AluguelSalas = "AluguelSalas";
        public const string CertificadoDigitais = "CertificadoDigitais";
        public static string GetDisplayName(string productType) => productType switch
        {
            EnderecoFiscal => DisplayNames.EnderecoFiscal,
            AluguelSalas => DisplayNames.AluguelSalas,
            CertificadoDigitais => DisplayNames.CertificadoDigitais,
            _ => productType
        };
        public static class DisplayNames
        {
            public const string EnderecoFiscal = "Endereço Fiscal";
            public const string AluguelSalas = "Aluguel de Salas";
            public const string CertificadoDigitais = "Certificado Digitais";
        }
    }
    public static class ReportAssistedSaleFilter
    {
        public const string All = "";
        public const string AssistedOnly = "AssistedOnly";
        public const string SelfServiceOnly = "SelfServiceOnly";
    }
    public class ReportFinancialResult
    {
        public List<ReportFinancialRowDto> Rows { get; set; } = new();
        public int TotalPayments { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal TotalCommissions { get; set; }
        public decimal Pending { get; set; }
    }
    public class ReportFinancialRowDto
    {
        public string ProductType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsAssistedSale { get; set; }
        public string SaleOrigin { get; set; } = string.Empty;
        public string? AssistedSaleAdminName { get; set; }
    }
    public class ClientCreditsReportDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int TotalCreditsPurchased { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public List<ClientCreditsSpendingByCoworkingDto> SpendingByCoworking { get; set; } = new();
        public int CurrentBalance { get; set; }
    }
    public class ClientCreditsSpendingByCoworkingDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public int CreditsUsed { get; set; }
        public decimal TotalValue { get; set; }
    }
    public class CoworkingRevenueReportDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public List<CoworkingProductRevenueDto> Products { get; set; } = new();
    }
    public class CoworkingProductRevenueDto
    {
        public string ProductType { get; set; } = string.Empty;
        public string ProductDisplayName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
        public decimal CommissionPercentage { get; set; }
    }
    public class CoworkingRevenueDetailDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public List<CoworkingReservationDetailRowDto> Reservations { get; set; } = new();
        public decimal TotalRevenue { get; set; }
    }
    public class CoworkingProductDetailDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string ProductDisplayName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal CommissionPercentage { get; set; }
        public List<ProductTransactionDetailDto> Transactions { get; set; } = new();
    }
    public class ProductTransactionDetailDto
    {
        public int PaymentId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int? ClientId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdditionalInfo { get; set; }
        public int? ReservationId { get; set; }
        public int? ContractId { get; set; }
    }
    public class CoworkingReservationDetailRowDto
    {
        public int ReservationId { get; set; }
        public DateTime Date { get; set; }
        public string RoomType { get; set; } = string.Empty;
        public string RoomTypeDisplay { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
        public int CreditsUsed { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal CommissionPercentage { get; set; }
    }
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalCoworkings { get; set; }
        public int ActiveContracts { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal PartnerRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public int TotalLocations { get; set; }
        public decimal PendingPayments { get; set; }
        public decimal TotalCommissions { get; set; }
        public decimal CompletedCommissions { get; set; }
        public int InactiveUsers { get; set; }
        public int UnverifiedUsers { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<UsersByRoleDto> UsersByRole { get; set; } = new();
        public List<ContractsByStatusDto> ContractsByStatus { get; set; } = new();
        public List<TopLocationDto> TopLocations { get; set; } = new();
        public List<Payment> RecentPayments { get; set; } = new();
        public List<MonthlyContractsDto> MonthlyContracts { get; set; } = new();
        public List<TopCoworkingRevenueDto> TopCoworkingsByRevenue { get; set; } = new();
        public List<PaymentsByMethodDto> PaymentsByMethod { get; set; } = new();
        public List<MonthlyNewClientsDto> MonthlyNewClients { get; set; } = new();
        public List<MonthlyRevenueBreakdownDto> MonthlyRevenueBreakdown { get; set; } = new();
        public int CancelledContractsLast6Months { get; set; }
        public int NewContractsLast6Months { get; set; }
    }
    public class MonthlyRevenueDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal Revenue { get; set; }
    }
    public class UsersByRoleDto
    {
        public string Role { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    public class ContractsByStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    public class TopLocationDto
    {
        public string City { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
    public class MonthlyContractsDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int NewContracts { get; set; }
        public int CancelledContracts { get; set; }
    }
    public class TopCoworkingRevenueDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int ContractsCount { get; set; }
    }
    public class PaymentsByMethodDto
    {
        public string Method { get; set; } = string.Empty;
        public string MethodDisplay { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }
    public class MonthlyNewClientsDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int NewClients { get; set; }
    }
    public class MonthlyRevenueBreakdownDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal PartnerRevenue { get; set; }
        public decimal NetRevenue { get; set; }
    }
}