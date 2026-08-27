using System.Globalization;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public sealed record PaymentNavigationResult(
        PaymentClientDestination Destination,
        bool ShowActionLink,
        string ActionLabel,
        string ActionIconCss);

    public sealed record CreditCardCheckoutDisplayInfo(
        string PlanOption,
        decimal DisplayAmount,
        string RecurrenceLabel);

    public sealed record CreditCardCheckoutPlanOptions(
        bool ShowCard1x,
        bool ShowCardMonthly,
        string DefaultPlanOption);
    /// <summary>
    /// Regras de navegação cliente: checkout inicial (PaymentDetails) vs acerto (PaymentSettlement)
    /// vs recibo (PaymentSuccess) vs resumo read-only (PaymentSummary).
    /// </summary>
    public static class PaymentNavigationHelper
    {
        public static PaymentNavigationResult Resolve(Payment payment, DateTime? referenceBrazilNow = null)
        {
            var isOverdue = payment.Status == PaymentStatus.Pending
                && BrazilDateTime.IsCalendarDueDateOverdue(payment.DueDate, referenceBrazilNow);
            var hasBillingError = HasBillingError(payment);

            return payment.Status switch
            {
                PaymentStatus.Paid or PaymentStatus.AwaitingValidation =>
                    Link(PaymentClientDestination.PaymentSuccess, "Ver recibo", "bi-receipt"),

                PaymentStatus.Processing =>
                    Link(PaymentClientDestination.PaymentSummary, "Ver status", "bi-arrow-repeat"),

                PaymentStatus.Failed =>
                    ShouldAllowCheckout(payment, isOverdue, hasBillingError)
                        ? LinkPayableCheckout(
                            payment,
                            ResolveFailedActionLabel(payment),
                            ResolveFailedActionIcon(payment))
                        : Link(PaymentClientDestination.PaymentSummary, "Ver detalhes", "bi-info-circle"),

                PaymentStatus.Cancelled or PaymentStatus.Refunded =>
                    Link(PaymentClientDestination.PaymentSummary, "Ver detalhes", "bi-info-circle"),

                PaymentStatus.Pending => ResolvePending(payment, isOverdue, hasBillingError),

                _ => Link(PaymentClientDestination.PaymentSummary, "Ver detalhes", "bi-eye")
            };
        }

        public static bool CanAccessPaymentCheckout(Payment payment, DateTime? referenceBrazilNow = null) =>
            Resolve(payment, referenceBrazilNow).Destination == PaymentClientDestination.PaymentDetails;

        public static bool CanAccessPaymentSettlement(Payment payment, DateTime? referenceBrazilNow = null) =>
            Resolve(payment, referenceBrazilNow).Destination == PaymentClientDestination.PaymentSettlement;

        /// <summary>
        /// Checkout inicial (1ª parcela / contrato aguardando 1º pagamento) vs acerto de contas (parcela 2+).
        /// </summary>
        public static bool IsFirstContractPayment(Payment payment)
        {
            if (!payment.SubscriptionId.HasValue)
                return true;

            var contract = payment.Subscription?.Contract;
            if (contract?.Status == ContractStatus.PendingPayment)
                return true;

            var subscriptionPayments = payment.Subscription?.Payments;
            if (subscriptionPayments == null || subscriptionPayments.Count == 0)
                return true;

            var firstPayment = subscriptionPayments
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .First();

            return firstPayment.Id == payment.Id;
        }

        public static bool IsRecurringAutoDebitInstallment(Payment payment)
        {
            if (!payment.SubscriptionId.HasValue)
                return false;

            var contract = payment.Subscription?.Contract;
            if (contract == null)
                return false;

            if (!contract.CardPlanType.IsMonthlyInstallmentPlan())
                return false;

            var subscriptionPayments = payment.Subscription?.Payments;
            if (subscriptionPayments == null || subscriptionPayments.Count == 0)
                return false;

            var firstPayment = subscriptionPayments
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .First();

            return firstPayment.Id != payment.Id;
        }

        /// <summary>
        /// Parcela mensal recorrente já emitida: valor cobrado vem de Payment.Amount / Subscription.MonthlyAmount,
        /// não do snapshot recalculado (BasePrice/12 + surcharge).
        /// </summary>
        public static decimal? ResolveLockedMonthlyChargeAmount(Payment payment)
        {
            if (payment.Amount <= 0)
                return null;

            if (!IsRecurringAutoDebitInstallment(payment))
                return null;

            if (BillingCycleConstants.IsMonthly(ResolveEffectiveBillingCycle(payment)))
                return payment.Amount;

            var contract = payment.Subscription?.Contract;
            if (contract?.CardPlanType == ContractCardPlanType.CardMonthly)
                return payment.Amount;

            return null;
        }

        /// <summary>
        /// Cupom só no checkout da 1ª parcela. Parcelas 2+ (renovação Asaas / débito automático)
        /// não devem recalcular preço de lista nem alterar valor da assinatura recorrente.
        /// </summary>
        public static bool CanApplyPromoCode(Payment payment)
        {
            if (payment.Status != PaymentStatus.Pending)
                return false;

            if (!IsFirstContractPayment(payment))
                return false;

            return !IsRecurringAutoDebitInstallment(payment);
        }

        /// <summary>
        /// Ciclo de cobrança efetivo da parcela: snapshot imutável no Payment, fallback Subscription e CardPlanType.
        /// </summary>
        public static string? ResolveEffectiveBillingCycle(Payment payment)
        {
            if (BillingCycleConstants.IsMonthly(payment.SnapshotBillingCycle))
                return BillingCycleConstants.Monthly;
            if (BillingCycleConstants.IsYearly(payment.SnapshotBillingCycle))
                return BillingCycleConstants.Yearly;

            if (BillingCycleConstants.IsMonthly(payment.Subscription?.BillingCycle))
                return BillingCycleConstants.Monthly;
            if (BillingCycleConstants.IsYearly(payment.Subscription?.BillingCycle))
                return BillingCycleConstants.Yearly;

            var plan = payment.Subscription?.Contract?.CardPlanType;
            if (plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly)
                return BillingCycleConstants.Monthly;
            // PIX tradicional (None) = assinatura mensal Asaas.
            if (payment.Method == PaymentMethod.PIX && (plan == null || plan == ContractCardPlanType.None))
                return BillingCycleConstants.Monthly;
            if (plan?.IsAnnualBillingCycle() == true)
                return BillingCycleConstants.Yearly;

            return null;
        }

        public static bool IsMonthlyBillingCycle(Payment payment) =>
            BillingCycleConstants.IsMonthly(ResolveEffectiveBillingCycle(payment))
            || payment.Subscription?.Contract?.CardPlanType == ContractCardPlanType.CardMonthly
            || payment.Subscription?.Contract?.CardPlanType == ContractCardPlanType.PixMonthly;

        public static bool IsYearlyBillingCycle(Payment payment) =>
            BillingCycleConstants.IsYearly(ResolveEffectiveBillingCycle(payment))
            || payment.Subscription?.Contract?.CardPlanType == ContractCardPlanType.Card1x
            || payment.Subscription?.Contract?.CardPlanType == ContractCardPlanType.PixAnnual;

        /// <summary>
        /// Opções de plano no checkout de cartão.
        /// Checkout inicial (1ª parcela): ambas se endereço/HiddenPaymentOptions suportarem.
        /// Acerto / parcelas 2+: conforme ciclo snapshot.
        /// </summary>
        public static CreditCardCheckoutPlanOptions ResolveCreditCardCheckoutPlanOptions(
            Payment payment,
            bool addressSupportsCard1x,
            bool addressSupportsCardMonthly)
        {
            if (IsFirstContractPayment(payment))
            {
                return new CreditCardCheckoutPlanOptions(
                    ShowCard1x: addressSupportsCard1x,
                    ShowCardMonthly: addressSupportsCardMonthly,
                    DefaultPlanOption: addressSupportsCard1x ? "1x" : "monthly");
            }

            var cycle = ResolveEffectiveBillingCycle(payment);
            var isRecurring = IsRecurringAutoDebitInstallment(payment);
            var isSettlement = !IsFirstContractPayment(payment);

            if (BillingCycleConstants.IsMonthly(cycle) || (isSettlement && IsMonthlyBillingCycle(payment)))
            {
                return new CreditCardCheckoutPlanOptions(
                    ShowCard1x: false,
                    ShowCardMonthly: addressSupportsCardMonthly,
                    DefaultPlanOption: "monthly");
            }

            if (BillingCycleConstants.IsYearly(cycle) || (isSettlement && IsYearlyBillingCycle(payment)))
            {
                return new CreditCardCheckoutPlanOptions(
                    ShowCard1x: addressSupportsCard1x,
                    ShowCardMonthly: false,
                    DefaultPlanOption: "1x");
            }

            if (isRecurring)
            {
                return new CreditCardCheckoutPlanOptions(
                    ShowCard1x: addressSupportsCard1x,
                    ShowCardMonthly: false,
                    DefaultPlanOption: "1x");
            }

            return new CreditCardCheckoutPlanOptions(
                ShowCard1x: addressSupportsCard1x,
                ShowCardMonthly: addressSupportsCardMonthly,
                DefaultPlanOption: "1x");
        }

        /// <summary>
        /// Label de recorrência sem misturar "equivale a R$/mês" em opção já mensal.
        /// </summary>
        public static string FormatBillingRecurrenceLabel(decimal amount, bool isMonthly) =>
            isMonthly
                ? "recorrência mensal"
                : "recorrência anual, equivale a R$ "
                  + (amount / 12m).ToString(NumberFormatConstants.CurrencyTwoDecimals, CultureInfo.GetCultureInfo("pt-BR"))
                  + "/mês";

        public static string FormatCreditCardRecurrenceLabel(decimal amount, bool isMonthly) =>
            FormatBillingRecurrenceLabel(amount, isMonthly);

        /// <summary>
        /// Label de recorrência para exibição read-only (Failed, Pending vencido, parcela recorrente).
        /// Usa BillingCycle como fonte primária; não divide valor mensal por 12.
        /// </summary>
        public static string? ResolveCreditCardRecurrenceLabelForPayment(Payment payment)
        {
            if (payment.Method != PaymentMethod.CreditCard)
                return null;

            if (BillingCycleConstants.IsMonthly(ResolveEffectiveBillingCycle(payment)))
                return FormatCreditCardRecurrenceLabel(payment.Amount, isMonthly: true);

            if (BillingCycleConstants.IsYearly(ResolveEffectiveBillingCycle(payment)))
                return FormatCreditCardRecurrenceLabel(payment.Amount, isMonthly: false);

            var plan = payment.Subscription?.Contract?.CardPlanType;
            if (plan == ContractCardPlanType.CardMonthly)
                return FormatCreditCardRecurrenceLabel(payment.Amount, isMonthly: true);

            if (plan == ContractCardPlanType.Card1x)
                return FormatCreditCardRecurrenceLabel(payment.Amount, isMonthly: false);

            return null;
        }

        public static CreditCardCheckoutDisplayInfo ResolveCreditCardCheckoutDisplay(
            Payment payment,
            decimal snapshotPrice1x,
            decimal snapshotPriceMonthly,
            bool showCard1x,
            bool showCardMonthly,
            bool useSnapshotPrices)
        {
            var planOptions = ResolveCreditCardCheckoutPlanOptions(payment, showCard1x, showCardMonthly);
            var effectiveShow1x = planOptions.ShowCard1x;
            var effectiveShowMonthly = planOptions.ShowCardMonthly;
            var planOption = ResolveCreditCardCheckoutPlanOption(
                payment, snapshotPrice1x, snapshotPriceMonthly, effectiveShow1x, effectiveShowMonthly,
                planOptions.DefaultPlanOption);
            var isMonthly = planOption == "monthly";
            var locked = ResolveLockedMonthlyChargeAmount(payment);

            decimal displayAmount;
            if (!IsFirstContractPayment(payment) && payment.Amount > 0)
                displayAmount = payment.Amount;
            else if (isMonthly && locked.HasValue)
                displayAmount = locked.Value;
            else if (useSnapshotPrices)
                displayAmount = isMonthly ? snapshotPriceMonthly : snapshotPrice1x;
            else
                displayAmount = payment.Amount;

            return new CreditCardCheckoutDisplayInfo(
                planOption,
                displayAmount,
                FormatCreditCardRecurrenceLabel(displayAmount, isMonthly));
        }
        /// <summary>
        /// Infere plano de cartão no checkout: "monthly" (recorrência mensal) ou "1x" (anual à vista).
        /// Usa SnapshotBillingCycle / Subscription.BillingCycle como fonte primária; fallback: CardPlanType, parcela recorrente e valor vs snapshot.
        /// </summary>
        public static string ResolveCreditCardCheckoutPlanOption(
            Payment payment,
            decimal snapshotPrice1x,
            decimal snapshotPriceMonthly,
            bool showCard1x,
            bool showCardMonthly,
            string? preferredDefault = null)
        {
            if (!showCard1x && !showCardMonthly)
                return preferredDefault ?? "1x";

            if (!showCard1x)
                return "monthly";

            if (!showCardMonthly)
                return "1x";

            var billingCycle = ResolveEffectiveBillingCycle(payment);
            if (BillingCycleConstants.IsMonthly(billingCycle))
                return "monthly";

            if (BillingCycleConstants.IsYearly(billingCycle))
                return "1x";

            var contractPlan = payment.Subscription?.Contract?.CardPlanType;

            if (contractPlan == ContractCardPlanType.CardMonthly)
                return "monthly";

            if (contractPlan == ContractCardPlanType.Card1x)
                return "1x";

            if (IsRecurringAutoDebitInstallment(payment))
                return "monthly";

            var amount = payment.Amount;
            var diff1x = Math.Abs(amount - snapshotPrice1x);
            var diffMonthly = Math.Abs(amount - snapshotPriceMonthly);
            const decimal tolerance = 0.02m;

            if (diffMonthly <= tolerance && diffMonthly < diff1x)
                return "monthly";

            if (diff1x <= tolerance && diff1x < diffMonthly)
                return "1x";

            if (diffMonthly <= tolerance && diff1x <= tolerance)
                return IsRecurringAutoDebitInstallment(payment) ? "monthly" : (preferredDefault ?? "1x");

            if (diffMonthly <= tolerance)
                return "monthly";

            if (diff1x <= tolerance)
                return "1x";

            if (contractPlan?.IsMonthlyInstallmentPlan() == true)
                return "monthly";

            if (contractPlan?.IsAnnualBillingCycle() == true)
                return "1x";

            return preferredDefault ?? "monthly";
        }
        public static string GetSummaryGuidance(Payment payment, DateTime? referenceBrazilNow = null)
        {
            var isOverdue = payment.Status == PaymentStatus.Pending
                && BrazilDateTime.IsCalendarDueDateOverdue(payment.DueDate, referenceBrazilNow);
            var plan = payment.Subscription?.Contract?.CardPlanType;
            var isAutoDebit = IsRecurringAutoDebitInstallment(payment);

            return payment.Status switch
            {
                PaymentStatus.Processing =>
                    "Seu pagamento está sendo processado. Não é necessário realizar nenhuma ação neste momento.",

                PaymentStatus.Failed =>
                    IsPixAutomaticAuthorizationFailure(payment)
                        ? (string.IsNullOrWhiteSpace(payment.FailureReason)
                            ? "A autorização PIX Automático não foi concluída. Gere um novo QR em Acerto de contas."
                            : payment.FailureReason.Trim())
                        : string.IsNullOrWhiteSpace(payment.FailureReason)
                            ? "A cobrança não foi concluída. Você pode atualizar o cartão e tentar o pagamento novamente."
                            : $"A cobrança não foi concluída: {payment.FailureReason.Trim()} Você pode atualizar o cartão e tentar novamente.",

                PaymentStatus.Cancelled =>
                    "Esta cobrança foi cancelada e não requer pagamento.",

                PaymentStatus.Refunded =>
                    "O valor desta cobrança foi estornado.",

                PaymentStatus.Pending when isAutoDebit && plan == ContractCardPlanType.CardMonthly =>
                    isOverdue
                        ? "A cobrança automática no cartão cadastrado está pendente. Regularize em Acerto de contas."
                        : "Esta parcela será debitada automaticamente no cartão cadastrado até a data de vencimento. Não é necessário pagar manualmente.",

                PaymentStatus.Pending when isAutoDebit && plan == ContractCardPlanType.PixMonthly =>
                    isOverdue
                        ? "O débito automático via PIX está pendente. Verifique sua autorização PIX Automático ou regularize em Acerto de contas."
                        : "Esta parcela será debitada automaticamente via PIX Automático próximo ao vencimento. Não é necessário pagar manualmente.",

                PaymentStatus.Pending when isOverdue =>
                    "Esta cobrança está vencida. Regularize o pagamento para manter seu contrato em dia.",

                PaymentStatus.Pending =>
                    "Aguardando confirmação do pagamento.",

                _ => string.Empty
            };
        }

        /// <summary>
        /// Opções de meio no checkout. Checkout inicial (1ª parcela): não filtra por ciclo.
        /// Acerto / parcelas 2+: mensal oculta anuais e vice-versa.
        /// </summary>
        public static (bool ShowPixAnnual, bool ShowPixMonthly, bool ShowPixSingle, bool ShowCard1x, bool ShowCardMonthly, bool ShowBoleto)
            FilterPaymentOptionsByBillingCycle(
                Payment payment,
                bool showPixAnnual,
                bool showPixMonthly,
                bool showPixSingle,
                bool showCard1x,
                bool showCardMonthly,
                bool showBoleto)
        {
            if (IsFirstContractPayment(payment))
                return (showPixAnnual, showPixMonthly, showPixSingle, showCard1x, showCardMonthly, showBoleto);

            if (IsMonthlyBillingCycle(payment))
            {
                return (
                    ShowPixAnnual: false,
                    ShowPixMonthly: showPixMonthly,
                    ShowPixSingle: showPixSingle,
                    ShowCard1x: false,
                    ShowCardMonthly: showCardMonthly,
                    ShowBoleto: showBoleto);
            }

            if (IsYearlyBillingCycle(payment))
            {
                return (
                    ShowPixAnnual: showPixAnnual,
                    ShowPixMonthly: false,
                    ShowPixSingle: showPixSingle,
                    ShowCard1x: showCard1x,
                    ShowCardMonthly: false,
                    ShowBoleto: showBoleto);
            }

            return (showPixAnnual, showPixMonthly, showPixSingle, showCard1x, showCardMonthly, showBoleto);
        }

        /// <summary>
        /// Valor selecionado no dropdown flat do acerto de contas (PixAnnual/PixMonthly/PIX/CreditCard/BankTransfer).
        /// </summary>
        public static string ResolveSettlementMethodSelectValue(Payment payment)
        {
            if (payment.Method == PaymentMethod.CreditCard)
                return "CreditCard";
            if (payment.Method == PaymentMethod.BankTransfer)
                return "BankTransfer";
            if (payment.Method != PaymentMethod.PIX)
                return payment.Method.ToString();

            var plan = payment.Subscription?.Contract?.CardPlanType;
            if (plan == ContractCardPlanType.PixAnnual)
                return "PixAnnual";
            if (plan == ContractCardPlanType.PixMonthly)
                return "PixMonthly";
            return "PIX";
        }

        public static string ResolveSettlementCycleLabel(Payment payment)
        {
            if (IsMonthlyBillingCycle(payment))
                return "Mensal";
            if (IsYearlyBillingCycle(payment))
                return "Anual";
            return "Conforme contrato";
        }

        public static bool CanChangePaymentMethod(Payment payment, DateTime? referenceBrazilNow = null)
        {
            if (payment.Status == PaymentStatus.Pending)
                return true;

            if (payment.Status == PaymentStatus.Failed
                && (CanAccessPaymentCheckout(payment, referenceBrazilNow)
                    || CanAccessPaymentSettlement(payment, referenceBrazilNow)))
                return true;

            return false;
        }

        private static PaymentNavigationResult ResolvePending(
            Payment payment,
            bool isOverdue,
            bool hasBillingError)
        {
            if (hasBillingError)
            {
                return ShouldAllowCheckout(payment, isOverdue, hasBillingError)
                    ? LinkPayableCheckout(
                        payment,
                        isOverdue ? "Pagar / atualizar" : "Tentar novamente",
                        "bi-arrow-clockwise")
                    : Link(PaymentClientDestination.PaymentSummary, "Ver detalhes", "bi-info-circle");
            }

            if (IsRecurringAutoDebitInstallment(payment))
            {
                if (ShouldAllowCheckout(payment, isOverdue, hasBillingError))
                {
                    return LinkPayableCheckout(
                        payment,
                        "Pagar / atualizar",
                        "bi-credit-card");
                }

                return Link(
                    PaymentClientDestination.PaymentSummary,
                    "Ver cobrança",
                    isOverdue ? "bi-exclamation-triangle" : "bi-calendar-check");
            }

            return LinkPayableCheckout(
                payment,
                isOverdue ? "Pagar agora" : "Pagar",
                "bi-credit-card");
        }

        private static PaymentNavigationResult LinkPayableCheckout(
            Payment payment,
            string label,
            string iconCss)
        {
            var destination = IsFirstContractPayment(payment)
                ? PaymentClientDestination.PaymentDetails
                : PaymentClientDestination.PaymentSettlement;
            return Link(destination, label, iconCss);
        }

        private static bool ShouldAllowCheckout(Payment payment, bool isOverdue, bool hasBillingError)
        {
            if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
                return false;

            if (!payment.SubscriptionId.HasValue)
                return true;

            if (!IsRecurringAutoDebitInstallment(payment))
                return true;

            // Qualquer Failed: retentar / trocar meio (acerto ou 1ª parcela).
            if (payment.Status == PaymentStatus.Failed)
                return true;

            // Débito automático vencido: permite regularizar.
            if (payment.Status == PaymentStatus.Pending && isOverdue)
                return true;

            if (hasBillingError)
                return true;

            return false;
        }

        private static bool HasBillingError(Payment payment) =>
            !string.IsNullOrWhiteSpace(payment.FailureReason);

        private static bool IsPixAutomaticAuthorizationFailure(Payment payment)
        {
            if (payment.Method != PaymentMethod.PIX)
                return false;

            var plan = payment.Subscription?.Contract?.CardPlanType;
            if (plan?.IsPixAutomatic() != true)
                return false;

            var reason = payment.FailureReason ?? string.Empty;
            return reason.Contains("Autorização PIX Automático", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Gere um novo QR", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveFailedActionLabel(Payment payment) =>
            IsPixAutomaticAuthorizationFailure(payment)
                ? "Gerar novo QR"
                : IsMonthlyBillingCycle(payment)
                    ? "Pagar / trocar meio"
                    : payment.Method == PaymentMethod.CreditCard
                        ? "Pagar / atualizar"
                        : "Tentar novamente";

        private static string ResolveFailedActionIcon(Payment payment) =>
            IsPixAutomaticAuthorizationFailure(payment)
                ? "bi-qr-code"
                : "bi-credit-card";

        private static PaymentNavigationResult Link(
            PaymentClientDestination destination,
            string label,
            string iconCss) =>
            new(destination, true, label, iconCss);
    }
}
