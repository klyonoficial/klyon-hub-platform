using KlyonEF.Models;

namespace KlyonEF.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetByIdAsync(int id);
        Task<Subscription?> GetByContractIdAsync(int contractId);
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription> UpdateAsync(Subscription subscription);
        Task<List<Subscription>> GetActiveSubscriptionsAsync();
        Task<List<Subscription>> GetDueSubscriptionsAsync(DateTime date);
    }
}
