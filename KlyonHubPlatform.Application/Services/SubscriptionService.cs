using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;
namespace KlyonHubPlatform.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRepository _paymentRepository;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IPaymentService paymentService,
            IAsaasService asaasService,
            IPaymentRepository paymentRepository)
        {
            _subscriptionRepository = subscriptionRepository;
            _paymentService = paymentService;
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
        }
        public async Task<Subscription> CreateSubscriptionAsync(int contractId, decimal monthlyAmount, ContractCardPlanType? cardPlanType = null)
        {
            var isAnnual = cardPlanType == ContractCardPlanType.Card1x || cardPlanType == ContractCardPlanType.PixAnnual;
            var subscription = new Subscription
            {
                ContractId = contractId,
                MonthlyAmount = monthlyAmount,
                BillingCycle = isAnnual ? "Yearly" : "Monthly",
                Status = SubscriptionStatus.Active,
                NextBillingDate = isAnnual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1),
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
                    var isPixAnnual = subscription.Contract?.CardPlanType == ContractCardPlanType.PixAnnual
                        && !string.IsNullOrEmpty(subscription.PixAutomaticAuthorizationId);

                    if (isPixAnnual)
                    {
                        var payment = await _paymentService.CreatePaymentAsync(
                            subscription.Id,
                            subscription.MonthlyAmount,
                            PaymentMethod.PIX
                        );
                        var contract = subscription.Contract;
                        var client = contract?.Client;
                        if (client != null && contract != null)
                        {
                            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? "";
                            var clientEmail = client.ContactEmail ?? client.User?.Email ?? "";
                            var clientTaxId = client.CPF ?? client.CNPJ ?? "";
                            var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(clientName, clientEmail, clientTaxId ?? "", client.ContactPhone, null, $"client_{client.Id}");
                            var order = await _asaasService.CreatePaymentWithPixAutomaticAuthorizationAsync(
                                asaasCustomerId,
                                subscription.PixAutomaticAuthorizationId!,
                                subscription.MonthlyAmount,
                                subscription.NextBillingDate,
                                payment.Id.ToString(),
                                $"Contrato {contract.ContractNumber} - renovação anual PIX"
                            );
                            if (order != null)
                            {
                                payment.ExternalPaymentId = order.Id;
                                await _paymentRepository.UpdateAsync(payment);
                                subscription.LastBillingDate = DateTime.UtcNow;
                                subscription.NextBillingDate = DateTime.UtcNow.AddYears(1);
                                await _subscriptionRepository.UpdateAsync(subscription);
                                processed.Add(subscription);
                            }
                        }
                    }
                    else
                    {
                        var payment = await _paymentService.CreatePaymentAsync(
                            subscription.Id,
                            subscription.MonthlyAmount,
                            PaymentMethod.CreditCard
                        );
                        await _paymentService.ProcessPaymentAsync(payment.Id);
                        subscription.LastBillingDate = DateTime.UtcNow;
                        subscription.NextBillingDate = subscription.Contract?.CardPlanType == ContractCardPlanType.Card1x || subscription.Contract?.CardPlanType == ContractCardPlanType.PixAnnual
                            ? DateTime.UtcNow.AddYears(1)
                            : DateTime.UtcNow.AddMonths(1);
                        await _subscriptionRepository.UpdateAsync(subscription);
                        processed.Add(subscription);
                    }
                }
                catch (Exception)
                {
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