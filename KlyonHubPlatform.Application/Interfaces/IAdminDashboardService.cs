using KlyonHubPlatform.Application.ViewModels;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardViewModelAsync(CancellationToken cancellationToken = default);
    }
}
