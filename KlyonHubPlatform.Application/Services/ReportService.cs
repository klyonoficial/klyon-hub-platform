using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            var model = new DashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalCoworkings = await _context.Coworkings.CountAsync(),
                ActiveContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
                TotalRevenue = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,
                TotalLocations = await _context.Locations.CountAsync(),
                PendingPayments = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Pending)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,
                TotalCommissions = await _context.Commissions
                    .Where(c => !c.IsTransferred)
                    .SumAsync(c => (decimal?)c.Amount) ?? 0,
                InactiveUsers = await _context.Users.CountAsync(u => !u.IsActive),
                UnverifiedUsers = await _context.Users.CountAsync(u => !u.EmailVerified)
            };

            // Receita dos últimos 6 meses
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            model.MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { Year = p.PaidAt.Value.Year, Month = p.PaidAt.Value.Month })
                .Select(g => new MonthlyRevenueDto
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // Usuários por tipo
            model.UsersByRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .GroupBy(ur => ur.Role.Name)
                .Select(g => new UsersByRoleDto
                {
                    Role = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // Contratos por status
            model.ContractsByStatus = await _context.Contracts
                .GroupBy(c => c.Status)
                .Select(g => new ContractsByStatusDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            // Top 5 localidades por receita
            model.TopLocations = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Location)
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => p.Subscription.Contract.CoworkingAddress.Location.City)
                .Select(g => new TopLocationDto
                {
                    City = g.Key,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // Pagamentos recentes
            model.RecentPayments = await GetRecentPaymentsAsync(30, 10);

            return model;
        }

        public async Task<List<Payment>> GetRecentPaymentsAsync(int days = 30, int take = 10)
        {
            var dateFrom = DateTime.UtcNow.AddDays(-days);
            return await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= dateFrom)
                .OrderByDescending(p => p.PaidAt)
                .Take(take)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .ToListAsync();
        }
    }
}
