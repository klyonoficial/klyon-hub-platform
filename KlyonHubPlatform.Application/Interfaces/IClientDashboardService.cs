namespace KlyonHubPlatform.Application.Interfaces
{
    public record ClientDashboardDetailsResult(
        KlyonHubPlatform.Domain.Entities.Client? Client,
        int ReservationsCount,
        bool HasFinancialPending,
        bool HasCertificateExpiring,
        System.Collections.Generic.IReadOnlySet<int>? SubscriptionIdsWithPaymentAwaitingValidation,
        int? FirstPendingPaymentId);
    public interface IClientDashboardService
    {
        Task<ClientDashboardDetailsResult> GetDashboardDetailsAsync(int userId, CancellationToken cancellationToken = default);
    }
}