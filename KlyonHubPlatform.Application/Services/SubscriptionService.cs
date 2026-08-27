using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IPaymentService paymentService,
            IAsaasService asaasService,
            IPaymentRepository paymentRepository,
            ILogger<SubscriptionService> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _paymentService = paymentService;
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
            _logger = logger;
        }

        public async Task<Subscription> CreateSubscriptionAsync(int contractId, decimal monthlyAmount, ContractCardPlanType? cardPlanType = null)
        {
            var isAnnual = cardPlanType.IsAnnualBillingCycle();
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
            // Renovações PIX Automático são tratadas por ProcessPixAutomaticRenewalsAsync (janela 2–10 dias úteis).
            // Este método permanece para cobranças locais de cartão sem assinatura Asaas (legado).
            var dueSubscriptions = await _subscriptionRepository.GetDueSubscriptionsAsync(DateTime.UtcNow);
            var processed = new List<Subscription>();
            foreach (var subscription in dueSubscriptions)
            {
                try
                {
                    if (!string.IsNullOrEmpty(subscription.ExternalSubscriptionId)
                        && subscription.ExternalSubscriptionId.StartsWith("sub_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var isPixAutomatic = subscription.Contract?.CardPlanType.IsPixAutomatic() == true
                        && !string.IsNullOrEmpty(subscription.PixAutomaticAuthorizationId);
                    if (isPixAutomatic)
                        continue;

                    var payment = await _paymentService.CreatePaymentAsync(
                        subscription.Id,
                        subscription.MonthlyAmount,
                        PaymentMethod.CreditCard,
                        snapshotListPrice: subscription.Contract?.SnapshotListPrice ?? 0m
                    );
                    await _paymentService.ProcessPaymentAsync(payment.Id);
                    subscription.LastBillingDate = DateTime.UtcNow;
                    subscription.NextBillingDate = subscription.Contract?.CardPlanType.IsAnnualBillingCycle() == true
                        ? DateTime.UtcNow.AddYears(1)
                        : DateTime.UtcNow.AddMonths(1);
                    await _subscriptionRepository.UpdateAsync(subscription);
                    processed.Add(subscription);
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

        /// <summary>
        /// Emite cobranças PIX Automático na janela Asaas (2–10 dias úteis antes do vencimento).
        /// O primeiro pagamento não gera as cobranças futuras; a integração deve criá-las.
        /// </summary>
        public async Task<List<Subscription>> ProcessPixAutomaticRenewalsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var subscriptions = await _subscriptionRepository.GetActivePixAutomaticSubscriptionsAsync();
            var processed = new List<Subscription>();

            foreach (var subscription in subscriptions)
            {
                try
                {
                    var dueDate = subscription.NextBillingDate.Date;
                    if (!BusinessDayHelper.IsWithinPixAutomaticInstructionWindow(today, dueDate))
                    {
                        if (dueDate < today)
                        {
                            _logger.LogWarning(
                                "PIX Automático: assinatura {SubscriptionId} com NextBillingDate {DueDate:yyyy-MM-dd} já passou e está fora da janela de emissão. Verifique manualmente.",
                                subscription.Id, dueDate);
                        }
                        continue;
                    }

                    var existingForCycle = subscription.Payments?
                        .Where(p =>
                            p.Method == PaymentMethod.PIX
                            && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                            && p.DueDate.Date == dueDate)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefault();

                    if (existingForCycle != null
                        && !string.IsNullOrEmpty(existingForCycle.ExternalPaymentId)
                        && existingForCycle.ExternalPaymentId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug(
                            "PIX Automático: cobrança já emitida no Asaas para assinatura {SubscriptionId} vencimento {DueDate:yyyy-MM-dd} ({ExternalId})",
                            subscription.Id, dueDate, existingForCycle.ExternalPaymentId);
                        continue;
                    }

                    var contract = subscription.Contract;
                    var client = contract?.Client;
                    if (client == null || contract == null || !PaymentGatewayCustomer.CanCharge(client))
                    {
                        _logger.LogWarning(
                            "PIX Automático: assinatura {SubscriptionId} sem cliente válido para cobrança.",
                            subscription.Id);
                        continue;
                    }

                    var payment = existingForCycle;
                    if (payment == null)
                    {
                        payment = await _paymentService.CreatePaymentAsync(
                            subscription.Id,
                            subscription.MonthlyAmount,
                            PaymentMethod.PIX,
                            snapshotListPrice: contract.SnapshotListPrice
                        );
                    }

                    var clientName = PaymentGatewayCustomer.ResolveName(client);
                    var clientEmail = PaymentGatewayCustomer.ResolveEmail(client) ?? "";
                    var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client) ?? "";
                    var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                        clientName,
                        clientEmail,
                        clientTaxId,
                        PaymentGatewayCustomer.ResolvePhone(client),
                        null,
                        $"client_{client.Id}");

                    var renewalLabel = subscription.Contract?.CardPlanType == ContractCardPlanType.PixMonthly
                        ? "renovação mensal PIX Automático"
                        : "renovação anual PIX Automático";
                    var order = await _asaasService.CreatePaymentWithPixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        subscription.PixAutomaticAuthorizationId!,
                        subscription.MonthlyAmount,
                        dueDate,
                        payment.Id.ToString(),
                        $"Contrato {contract.ContractNumber} - {renewalLabel}"
                    );

                    if (order == null || string.IsNullOrEmpty(order.Id))
                    {
                        _logger.LogError(
                            "PIX Automático: falha ao criar cobrança no Asaas para assinatura {SubscriptionId}, pagamento {PaymentId}, vencimento {DueDate:yyyy-MM-dd}",
                            subscription.Id, payment.Id, dueDate);
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
                        continue;
                    }

                    payment.ExternalPaymentId = order.Id;
                    payment.DueDate = dueDate;
                    await _paymentRepository.UpdateAsync(payment);

                    // Não avança NextBillingDate aqui: ConfirmPaymentAsync faz isso na liquidação.
                    // Evita perder o ciclo se a instrução for recusada e corrige a data pelo PaidAt.
                    subscription.FailedPaymentAttempts = 0;
                    if (subscription.Status == SubscriptionStatus.PastDue)
                        subscription.Status = SubscriptionStatus.Active;
                    await _subscriptionRepository.UpdateAsync(subscription);

                    processed.Add(subscription);
                    _logger.LogInformation(
                        "PIX Automático: cobrança {AsaasPaymentId} criada para assinatura {SubscriptionId}, vencimento {DueDate:yyyy-MM-dd}",
                        order.Id, subscription.Id, dueDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PIX Automático: erro ao processar renovação da assinatura {SubscriptionId}", subscription.Id);
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
