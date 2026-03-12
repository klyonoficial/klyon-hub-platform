using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Subscription?> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default);
        Task<Subscription> UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
        Task<List<Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);
        Task<List<Subscription>> GetDueSubscriptionsAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Subscription?> GetByPixAutomaticAuthorizationIdAsync(string pixAutomaticAuthorizationId, CancellationToken cancellationToken = default);
    }
}