using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<Subscription> CreateSubscriptionAsync(int contractId, decimal monthlyAmount, ContractCardPlanType? cardPlanType = null);
        Task<Subscription> CancelSubscriptionAsync(int subscriptionId, string reason);
        Task<List<Subscription>> ProcessDueSubscriptionsAsync();
        /// <summary>Emite cobranças PIX Automático na janela Asaas (2–10 dias úteis antes do vencimento).</summary>
        Task<List<Subscription>> ProcessPixAutomaticRenewalsAsync();
        Task<Subscription> SuspendSubscriptionAsync(int subscriptionId);
    }
}