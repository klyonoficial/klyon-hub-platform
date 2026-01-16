using KlyonEF.Models;

namespace KlyonEF.Services
{
    public interface ISubscriptionService
    {
        Task<Subscription> CreateSubscriptionAsync(int contractId, decimal monthlyAmount);
        Task<Subscription> CancelSubscriptionAsync(int subscriptionId, string reason);
        Task<List<Subscription>> ProcessDueSubscriptionsAsync();
        Task<Subscription> SuspendSubscriptionAsync(int subscriptionId);
    }
}
