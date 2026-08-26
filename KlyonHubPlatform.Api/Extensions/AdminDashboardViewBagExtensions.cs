using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.ViewModels;
namespace KlyonHubPlatform.Api.Extensions
{
    public static class AdminDashboardViewBagExtensions
    {
        public static void SetDashboardViewBag(this Controller controller, AdminDashboardViewModel model)
        {
            if (model == null) return;
            var d = model.DashboardData;
            controller.ViewBag.TotalUsers = d.TotalUsers;
            controller.ViewBag.TotalCoworkings = d.TotalCoworkings;
            controller.ViewBag.ActiveContracts = d.ActiveContracts;
            controller.ViewBag.TotalRevenue = d.TotalRevenue;
            controller.ViewBag.GrossRevenue = d.GrossRevenue;
            controller.ViewBag.PartnerRevenue = d.PartnerRevenue;
            controller.ViewBag.NetRevenue = d.NetRevenue;
            controller.ViewBag.TotalLocations = d.TotalLocations;
            controller.ViewBag.PendingPayments = d.PendingPayments;
            controller.ViewBag.TotalCommissions = d.TotalCommissions;
            controller.ViewBag.CompletedCommissions = d.CompletedCommissions;
            controller.ViewBag.InactiveUsers = d.InactiveUsers;
            controller.ViewBag.UnverifiedUsers = d.UnverifiedUsers;
            controller.ViewBag.MonthlyRevenue = d.MonthlyRevenue;
            controller.ViewBag.UsersByRole = d.UsersByRole;
            controller.ViewBag.ContractsByStatus = d.ContractsByStatus;
            controller.ViewBag.TopLocations = d.TopLocations;
            controller.ViewBag.RecentPayments = d.RecentPayments;
            controller.ViewBag.MonthlyContracts = d.MonthlyContracts;
            controller.ViewBag.TopCoworkingsByRevenue = d.TopCoworkingsByRevenue;
            controller.ViewBag.PaymentsByMethod = d.PaymentsByMethod;
            controller.ViewBag.MonthlyNewClients = d.MonthlyNewClients;
            controller.ViewBag.MonthlyRevenueBreakdown = d.MonthlyRevenueBreakdown;
            controller.ViewBag.CancelledContractsLast6Months = d.CancelledContractsLast6Months;
            controller.ViewBag.NewContractsLast6Months = d.NewContractsLast6Months;
            controller.ViewBag.PendingMigrationsCount = model.PendingMigrationsCount;
            controller.ViewBag.PendingMigrations = model.PendingMigrations;
            controller.ViewBag.ProductsByType = model.ProductsByType;
            controller.ViewBag.TotalReservations = model.TotalReservations;
            controller.ViewBag.ReservationsByStatus = model.ReservationsByStatus;
            controller.ViewBag.PartnersWithoutPercentage = model.PartnersWithoutPercentage;
            controller.ViewBag.ServicesAwaitingPercentage = model.ServicesAwaitingPercentage;
            controller.ViewBag.PendingRemovalRequests = model.PendingRemovalRequests;
            // Assign concrete AdminAlertDto list (not anonymous types): Razor `as List<dynamic>`
            // fails for List<TAnonymous> and would drop all admin alerts including pending prices.
            foreach (var alert in model.AdminAlerts)
            {
                if (!string.IsNullOrEmpty(alert.ActionName) && !string.IsNullOrEmpty(alert.ActionController))
                    alert.ActionUrl = controller.Url?.Action(alert.ActionName, alert.ActionController, alert.ActionRouteValues);
            }
            controller.ViewBag.AdminAlerts = model.AdminAlerts;
        }
    }
}