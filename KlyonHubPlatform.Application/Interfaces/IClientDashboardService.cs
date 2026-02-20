namespace KlyonHubPlatform.Application.Interfaces
{
    public record ClientDashboardDetailsResult(
        KlyonHubPlatform.Domain.Entities.Client? Client,
        int ReservationsCount,
        bool HasFinancialPending,
        bool HasCertificateExpiring);
    public interface IClientDashboardService
    {
        Task<ClientDashboardDetailsResult> GetDashboardDetailsAsync(int userId, CancellationToken cancellationToken = default);
    }
}