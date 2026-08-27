using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Calcula próximas parcelas previstas para assinaturas recorrentes quando ainda não há cobrança real no banco.
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
                var contract = subscription.Contract;
                var plan = contract.CardPlanType;
                if (!IsRecurringPlan(plan))
                    continue;

                var nextDueDate = ResolveNextDueDate(subscription, contract, subscription.Payments);
                if (!nextDueDate.HasValue)
                    continue;

                if (HasVisibleFutureCharge(subscription.Payments))
                    continue;

                var amount = ResolveAmount(subscription, contract);
                var isPixAutomatic = plan!.IsPixAutomatic();

                predicted.Add(new PredictedPaymentDto
                {
                    SubscriptionId = subscription.Id,
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    Address = contract.CoworkingAddress?.Address ?? contract.SnapshotAddress,
                    Amount = amount,
                    Method = isPixAutomatic ? PaymentMethod.PIX : PaymentMethod.CreditCard,
                    DueDate = nextDueDate.Value,
                    StatusLabel = isPixAutomatic ? "Aguardando emissão" : "Previsto",
                    Explanation = isPixAutomatic
                        ? "Cobrança PIX Automático emitida 2–10 dias úteis antes do vencimento."
                        : "Próxima parcela da assinatura será cobrada automaticamente no seu cartão cadastrado."
                });
            }

            return predicted
                .OrderBy(p => p.DueDate)
                .ToList();
        }

        private static bool IsRecurringPlan(ContractCardPlanType? plan) =>
            plan is ContractCardPlanType.CardMonthly
                or ContractCardPlanType.Card1x
                or ContractCardPlanType.PixMonthly
                or ContractCardPlanType.PixAnnual;

        private static DateTime? ResolveNextDueDate(
            Subscription subscription,
            Contract contract,
            IEnumerable<Payment> subscriptionPayments)
        {
            if (subscription.NextBillingDate > DateTime.MinValue.AddYears(1))
                return subscription.NextBillingDate.Date;

            var lastPaid = subscriptionPayments
                .Where(p => p.Status == PaymentStatus.Paid)
                .OrderByDescending(p => p.DueDate)
                .ThenByDescending(p => p.PaidAt)
                .FirstOrDefault();

            if (lastPaid == null)
                return null;

            return contract.CardPlanType.IsAnnualBillingCycle()
                ? lastPaid.DueDate.Date.AddYears(1)
                : lastPaid.DueDate.Date.AddMonths(1);
        }

        private static bool HasVisibleFutureCharge(IEnumerable<Payment> subscriptionPayments) =>
            subscriptionPayments.Any(p =>
                p.Status == PaymentStatus.Pending
                || p.Status == PaymentStatus.Processing
                || p.Status == PaymentStatus.AwaitingValidation);

        private decimal ResolveAmount(Subscription subscription, Contract contract)
        {
            if (subscription.MonthlyAmount > 0)
                return subscription.MonthlyAmount;

            return _addressPricingService.GetChargeAmount(
                contract.SnapshotListPrice,
                contract.SnapshotCardMonthlySurchargePercent,
                contract.CardPlanType);
        }
    }
}
