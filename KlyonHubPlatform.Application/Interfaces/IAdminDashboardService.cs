using KlyonHubPlatform.Application.ViewModels;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardViewModelAsync(CancellationToken cancellationToken = default);
        /// <summary>Retorna os dados da listagem do dashboard por tipo (users, coworkings, contracts, etc.). Null se tipo inválido.</summary>
        Task<DashboardListResult?> GetDashboardListAsync(string type, string? filter, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}