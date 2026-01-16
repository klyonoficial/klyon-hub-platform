using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
        Task<List<Payment>> GetRecentPaymentsAsync(int days = 30, int take = 10);
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
