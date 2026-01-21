using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
        Task<List<Payment>> GetRecentPaymentsAsync(int days = 30, int take = 10);
        Task<ReportFinancialResult> GetFinancialReportAsync(DateTime dateFrom, DateTime dateTo, string? product = null);
        Task<List<ClientCreditsReportDto>> GetClientCreditsReportAsync();
        Task<ClientCreditStatementDto> GetClientCreditStatementAsync(int clientId, DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<List<CoworkingRevenueReportDto>> GetCoworkingRevenueReportAsync();
        Task<CoworkingRevenueDetailDto> GetCoworkingRevenueDetailAsync(int coworkingId, DateTime? dateFrom = null, DateTime? dateTo = null);
    }

    /// <summary>Extrato de créditos do cliente: compras (entrada) e reservas (saída) em ordem cronológica.</summary>
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
        public string Type { get; set; } = "Entrada"; // "Entrada" ou "Saída"
        public int CreditsDelta { get; set; }  // positivo = compra, negativo = uso
        public decimal Amount { get; set; }    // R$
    }

    /// <summary>Filtros de produto: null/"" = Todos, "EnderecoFiscal", "AluguelSalas", "CertificadoDigitais"</summary>
    public static class ReportProductFilter
    {
        public const string Todos = "";
        public const string EnderecoFiscal = "EnderecoFiscal";
        public const string AluguelSalas = "AluguelSalas";
        public const string CertificadoDigitais = "CertificadoDigitais";
    }

    public class ReportFinancialResult
    {
        public List<ReportFinancialRowDto> Rows { get; set; } = new();
        public int TotalPayments { get; set; }
        public decimal TotalReceived { get; set; }
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
    }

    /// <summary>Relatório de créditos por cliente: compras, gastos por coworking e saldo.</summary>
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

    /// <summary>Relatório de receita por coworking (reservas de salas).</summary>
    public class CoworkingRevenueReportDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int ReservationCount { get; set; }
    }

    /// <summary>Detalhe de receita do coworking: reservas com tipo de sala, data e cliente.</summary>
    public class CoworkingRevenueDetailDto
    {
        public int CoworkingId { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public List<CoworkingReservationDetailRowDto> Reservations { get; set; } = new();
        public decimal TotalRevenue { get; set; }
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
    }

    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalCoworkings { get; set; }
        public int ActiveContracts { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalLocations { get; set; }
        public decimal PendingPayments { get; set; }
        public decimal TotalCommissions { get; set; }
        public int InactiveUsers { get; set; }
        public int UnverifiedUsers { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<UsersByRoleDto> UsersByRole { get; set; } = new();
        public List<ContractsByStatusDto> ContractsByStatus { get; set; } = new();
        public List<TopLocationDto> TopLocations { get; set; } = new();
        public List<Payment> RecentPayments { get; set; } = new();
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
}
