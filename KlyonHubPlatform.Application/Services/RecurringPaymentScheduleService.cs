using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Calcula próximas parcelas previstas para assinaturas recorrentes quando ainda não há cobrança real no banco.
    /// Prioriza o pagamento pago local (SnapshotBillingCycle / método / valor) sobre assinatura Asaas residual órfã.
    /// </summary>
    public class RecurringPaymentScheduleService : IRecurringPaymentScheduleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAddressPricingService _addressPricingService;

        public RecurringPaymentScheduleService(
            ApplicationDbContext context,
            IAddressPricingService addressPricingService)
        {
            _context = context;
            _addressPricingService = addressPricingService;
        }

        public async Task<List<PredictedPaymentDto>> GetPredictedNextPaymentsAsync(
            int clientId,
            CancellationToken cancellationToken = default)
        {
            var subscriptions = await _context.Subscriptions
                .Include(s => s.Contract)
                    .ThenInclude(c => c!.CoworkingAddress)
                .Include(s => s.Payments)
                .Where(s =>
                    s.Status == SubscriptionStatus.Active
                    && s.Contract != null
                    && s.Contract.ClientId == clientId
                    && (s.Contract.Status == ContractStatus.Active
                        || s.Contract.Status == ContractStatus.PendingCancellation))
                .ToListAsync(cancellationToken);

            var predicted = new List<PredictedPaymentDto>();

            foreach (var subscription in subscriptions)
            {
                var contract = subscription.Contract!;
                var payments = subscription.Payments ?? Enumerable.Empty<Payment>();
                var lastPaid = GetLastPaid(payments);
                var plan = contract.CardPlanType;
                var isAsaasSub = IsAsaasManagedSubscription(subscription);
                if (!IsRecurringPlan(plan) && !isAsaasSub)
                    continue;

                var billingCycle = ResolveBillingCycle(lastPaid, subscription, contract);
                var nextDueDate = ResolveNextDueDate(subscription, lastPaid, billingCycle);
                if (!nextDueDate.HasValue)
                    continue;

                if (HasVisibleFutureCharge(payments))
                    continue;

                var amount = ResolveAmount(subscription, contract, lastPaid, billingCycle);
                var predictedMethod = ResolvePredictedMethod(plan, lastPaid);
                var isPixAutomatic = predictedMethod == PaymentMethod.PIX && plan.IsPixAutomatic();
                var (statusLabel, explanation) = ResolvePredictedLabels(isPixAutomatic, predictedMethod, billingCycle);

                predicted.Add(new PredictedPaymentDto
                {
                    SubscriptionId = subscription.Id,
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    Address = contract.CoworkingAddress?.Address ?? contract.SnapshotAddress,
                    Amount = amount,
                    Method = predictedMethod,
                    DueDate = nextDueDate.Value,
                    StatusLabel = statusLabel,
                    Explanation = explanation
                });
            }

            return predicted
                .OrderBy(p => p.DueDate)
                .ToList();
        }

        private static Payment? GetLastPaid(IEnumerable<Payment> payments) =>
            payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .OrderByDescending(p => p.DueDate)
                .ThenByDescending(p => p.PaidAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefault();

        private static bool IsRecurringPlan(ContractCardPlanType? plan) =>
            plan is ContractCardPlanType.CardMonthly
                or ContractCardPlanType.Card1x
                or ContractCardPlanType.PixMonthly
                or ContractCardPlanType.PixAnnual;

        private static bool IsAsaasManagedSubscription(Subscription subscription) =>
            !string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId)
            && subscription.ExternalSubscriptionId.StartsWith("sub_", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Ciclo efetivo: Snapshot do pagamento pago → CardPlanType do contrato → BillingCycle da assinatura.
        /// Ignora sub Asaas/PIX mensal órfã quando o pagamento pago indica anual.
        /// </summary>
        private static string? ResolveBillingCycle(
            Payment? lastPaid,
            Subscription subscription,
            Contract contract)
        {
            if (lastPaid != null)
            {
                if (BillingCycleConstants.IsMonthly(lastPaid.SnapshotBillingCycle))
                    return BillingCycleConstants.Monthly;
                if (BillingCycleConstants.IsYearly(lastPaid.SnapshotBillingCycle))
                    return BillingCycleConstants.Yearly;
            }

            var plan = contract.CardPlanType;
            if (plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly)
                return BillingCycleConstants.Monthly;
            if (plan.IsAnnualBillingCycle())
                return BillingCycleConstants.Yearly;

            if (BillingCycleConstants.IsMonthly(subscription.BillingCycle))
                return BillingCycleConstants.Monthly;
            if (BillingCycleConstants.IsYearly(subscription.BillingCycle))
                return BillingCycleConstants.Yearly;

            // PIX tradicional (None) em assinatura Asaas = mensal, salvo snapshot anual no pago.
            if (plan == ContractCardPlanType.None || plan == null)
            {
                if (lastPaid != null)
                {
                    var fromPaid = PaymentNavigationHelper.ResolveEffectiveBillingCycle(lastPaid);
                    if (fromPaid != null)
                        return fromPaid;
                }

                return BillingCycleConstants.Monthly;
            }

            return null;
        }

        /// <summary>
        /// Próximo vencimento a partir do último pago + ciclo local.
        /// Não confia em NextBillingDate de sub Asaas residual (ex.: PIX mensal órfã após cartão anual).
        /// </summary>
        private static DateTime? ResolveNextDueDate(
            Subscription subscription,
            Payment? lastPaid,
            string? billingCycle)
        {
            if (lastPaid != null)
            {
                return BillingCycleConstants.IsYearly(billingCycle)
                    ? lastPaid.DueDate.Date.AddYears(1)
                    : lastPaid.DueDate.Date.AddMonths(1);
            }

            if (subscription.NextBillingDate > DateTime.MinValue.AddYears(1))
                return subscription.NextBillingDate.Date;

            return null;
        }

        private static bool HasVisibleFutureCharge(IEnumerable<Payment> subscriptionPayments) =>
            subscriptionPayments.Any(p =>
                p.Status == PaymentStatus.Pending
                || p.Status == PaymentStatus.Processing
                || p.Status == PaymentStatus.AwaitingValidation);

        /// <summary>
        /// Meio vigente: último pagamento pago (não cancelado/falho residual), senão plano do contrato.
        /// </summary>
        private static PaymentMethod ResolvePredictedMethod(
            ContractCardPlanType? plan,
            Payment? lastPaid)
        {
            if (lastPaid != null)
                return lastPaid.Method;

            if (plan.IsPixAutomatic()
                || plan == ContractCardPlanType.PixMonthly
                || plan == ContractCardPlanType.PixAnnual
                || plan == ContractCardPlanType.None)
                return PaymentMethod.PIX;

            if (plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.Card1x)
                return PaymentMethod.CreditCard;

            return PaymentMethod.CreditCard;
        }

        private static (string StatusLabel, string Explanation) ResolvePredictedLabels(
            bool isPixAutomatic,
            PaymentMethod method,
            string? billingCycle)
        {
            var isYearly = BillingCycleConstants.IsYearly(billingCycle);
            var periodLabel = isYearly ? "anual" : "mensal";

            if (isPixAutomatic)
            {
                return (
                    "Aguardando emissão",
                    isYearly
                        ? "Cobrança PIX Automático anual emitida 2–10 dias úteis antes do vencimento."
                        : "Cobrança PIX Automático emitida 2–10 dias úteis antes do vencimento.");
            }

            return method switch
            {
                PaymentMethod.PIX => (
                    "Previsto",
                    $"O Asaas emitirá um QR Code PIX {periodLabel} pela assinatura. Pague cada cobrança em Meus Pagamentos."),
                PaymentMethod.BankTransfer => (
                    "Previsto",
                    $"O Asaas emitirá um boleto {periodLabel} pela assinatura. Pague cada cobrança em Meus Pagamentos."),
                _ => (
                    "Previsto",
                    isYearly
                        ? "Próxima cobrança anual será debitada automaticamente no seu cartão cadastrado."
                        : "Próxima parcela da assinatura será cobrada automaticamente no seu cartão cadastrado.")
            };
        }

        private decimal ResolveAmount(
            Subscription subscription,
            Contract contract,
            Payment? lastPaid,
            string? billingCycle)
        {
            // Valor do ciclo vigente = o que o cliente pagou (ex.: 675 anual), não parcela residual PIX.
            if (lastPaid != null && lastPaid.Amount > 0)
                return lastPaid.Amount;

            if (subscription.MonthlyAmount > 0)
                return subscription.MonthlyAmount;

            var isMonthly = BillingCycleConstants.IsMonthly(billingCycle)
                || contract.CardPlanType.IsMonthlyInstallmentPlan()
                || (contract.CardPlanType == ContractCardPlanType.None
                    && !BillingCycleConstants.IsYearly(billingCycle));

            if (isMonthly)
            {
                return _addressPricingService.GetMonthlyInstallment(
                    contract.SnapshotListPrice,
                    contract.SnapshotCardMonthlySurchargePercent);
            }

            return _addressPricingService.GetChargeAmount(
                contract.SnapshotListPrice,
                contract.SnapshotCardMonthlySurchargePercent,
                contract.CardPlanType);
        }
    }
}
