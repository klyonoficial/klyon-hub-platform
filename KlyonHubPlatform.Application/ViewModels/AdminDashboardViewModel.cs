using KlyonHubPlatform.Application.Interfaces;

namespace KlyonHubPlatform.Application.ViewModels
{
    /// <summary>
    /// Modelo completo para a view Dashboard do Admin (dados que hoje vão para ViewBag).
    /// </summary>
    public class AdminDashboardViewModel
    {
        public DashboardViewModel DashboardData { get; set; } = null!;

        public int PendingMigrationsCount { get; set; }
        public List<string> PendingMigrations { get; set; } = new();

        public List<ProductTypeCountDto> ProductsByType { get; set; } = new();
        public int TotalReservations { get; set; }
        public List<ReservationStatusCountDto> ReservationsByStatus { get; set; } = new();

        public int PartnersWithoutPercentage { get; set; }
        public int ServicesAwaitingPercentage { get; set; }
        public int PendingRemovalRequests { get; set; }

        public List<AdminAlertDto> AdminAlerts { get; set; } = new();
    }

    public class ProductTypeCountDto
    {
        public string ProductType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReservationStatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Alerta administrativo. ActionUrl é preenchido no controller via IUrlHelper.
    /// </summary>
    public class AdminAlertDto
    {
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public string? ActionKey { get; set; }
        public string ActionController { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public object? ActionRouteValues { get; set; }
        public string ActionText { get; set; } = string.Empty;
    }
}
