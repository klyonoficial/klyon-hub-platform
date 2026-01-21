using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentService _paymentService;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IPaymentService paymentService)
        {
            _subscriptionRepository = subscriptionRepository;
            _paymentService = paymentService;
        }

        public async Task<Subscription> CreateSubscriptionAsync(int contractId, decimal monthlyAmount)
        {
            var subscription = new Subscription
            {
                ContractId = contractId,
                MonthlyAmount = monthlyAmount,
                Status = SubscriptionStatus.Active,
                NextBillingDate = DateTime.UtcNow.AddMonths(1),
                ExternalSubscriptionId = Guid.NewGuid().ToString()
            };

            return await _subscriptionRepository.CreateAsync(subscription);
        }

        public async Task<Subscription> CancelSubscriptionAsync(int subscriptionId, string reason)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
            subscription.CancellationReason = reason;

            return await _subscriptionRepository.UpdateAsync(subscription);
        }

        public async Task<List<Subscription>> ProcessDueSubscriptionsAsync()
        {
            var dueSubscriptions = await _subscriptionRepository.GetDueSubscriptionsAsync(DateTime.UtcNow);
            var processed = new List<Subscription>();

            foreach (var subscription in dueSubscriptions)
            {
                try
                {
                    // Criar pagamento
                    var payment = await _paymentService.CreatePaymentAsync(
                        subscription.Id,
                        subscription.MonthlyAmount,
                        PaymentMethod.CreditCard // Default, pode ser configurável
                    );

                    // Processar pagamento
                    await _paymentService.ProcessPaymentAsync(payment.Id);

                    processed.Add(subscription);
                }
                catch (Exception)
                {
                    // Log error
                    subscription.FailedPaymentAttempts++;
                    if (subscription.FailedPaymentAttempts >= 3)
                    {
                        subscription.Status = SubscriptionStatus.Suspended;
                        subscription.SuspendedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        subscription.Status = SubscriptionStatus.PastDue;
                    }
                    await _subscriptionRepository.UpdateAsync(subscription);
                }
            }

            return processed;
        }

        public async Task<Subscription> SuspendSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            subscription.Status = SubscriptionStatus.Suspended;
            subscription.SuspendedAt = DateTime.UtcNow;

            return await _subscriptionRepository.UpdateAsync(subscription);
        }
    }
}
