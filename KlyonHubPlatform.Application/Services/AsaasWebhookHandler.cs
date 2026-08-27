using System.Globalization;
using System.Text.Json;
using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Centraliza criação/atualização de Payments locais a partir de cobranças recorrentes Asaas
    /// (renovação cartão sub_*, PIX Automático mensal/anual).
    /// </summary>
    public class AsaasWebhookHandler : IAsaasWebhookHandler
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<AsaasWebhookHandler> _logger;

        public AsaasWebhookHandler(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            IPaymentService paymentService,
            ILogger<AsaasWebhookHandler> logger)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<AsaasWebhookHandleResult> HandleEventAsync(
            JsonElement root,
            string? eventType,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(eventType))
                return AsaasWebhookHandleResult.NotHandled();

            if (IsPixAuthorizationEvent(eventType))
            {
                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Authorization, out var authEl))
                {
                    _logger.LogWarning("Webhook Asaas {Event} sem objeto authorization", eventType);
                    return AsaasWebhookHandleResult.Done();
                }

                await HandlePixAuthorizationEventAsync(authEl, eventType, cancellationToken);
                return AsaasWebhookHandleResult.Done();
            }

            if (IsPixInstructionEvent(eventType))
            {
                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.PaymentInstruction, out var instructionEl)
                    && !root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out _))
                {
                    _logger.LogWarning("Webhook Asaas {Event} sem paymentInstruction", eventType);
                    return AsaasWebhookHandleResult.Done();
                }

                var paymentIdForCancelled = await HandlePixPaymentInstructionEventAsync(
                    root, eventType, cancellationToken);
                return AsaasWebhookHandleResult.Done(paymentIdForCancelledNotification: paymentIdForCancelled);
            }

            if (IsSubscriptionEvent(eventType))
            {
                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Subscription, out var subscriptionEl))
                {
                    _logger.LogWarning("Webhook Asaas {Event} sem objeto subscription", eventType);
                    return AsaasWebhookHandleResult.Done();
                }

                await HandleSubscriptionEventAsync(subscriptionEl, eventType, cancellationToken);
                return AsaasWebhookHandleResult.Done();
            }

            if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var paymentEl))
            {
                if (IsKnownAsaasEvent(eventType))
                    _logger.LogInformation("Webhook Asaas {Event} recebido sem payment; ignorado (log only).", eventType);

                return AsaasWebhookHandleResult.NotHandled();
            }

            return await HandlePaymentEventAsync(root, paymentEl, eventType, cancellationToken);
        }

        public async Task<Payment?> ResolveLocalPaymentAsync(
            string asaasPaymentId,
            string? externalReference,
            string? pixQrCodeId,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(
                asaasPaymentId, externalReference, cancellationToken);
            if (payment != null)
                return payment;

            if (!string.IsNullOrEmpty(pixQrCodeId))
            {
                payment = await _paymentRepository.GetByPixConciliationIdentifierAsync(pixQrCodeId, cancellationToken);
            }

            return payment;
        }

        public async Task<Payment?> EnsureLocalPaymentFromAsaasChargeAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalReference = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");
            var asaasSubscriptionId = GetString(paymentEl, AsaasWebhookPayloadKeys.Subscription);

            var existing = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalReference, pixQrCodeId, cancellationToken);
            if (existing != null)
                return await SyncAsaasChargeMetadataAsync(existing, asaasPaymentId, paymentEl, cancellationToken);

            if (!string.IsNullOrEmpty(asaasSubscriptionId)
                && asaasSubscriptionId.StartsWith("sub_", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateSubscriptionRenewalPaymentAsync(
                    asaasSubscriptionId,
                    asaasPaymentId,
                    paymentEl,
                    cancellationToken);
            }

            var billingType = GetString(paymentEl, AsaasWebhookPayloadKeys.BillingType);
            if (string.Equals(billingType, AsaasBillingType.Pix, StringComparison.OrdinalIgnoreCase))
            {
                var pixSubscription = await LoadPixAutomaticSubscriptionFromReferenceAsync(externalReference, cancellationToken);
                if (pixSubscription != null)
                {
                    return await CreateOrLinkPixAutomaticRenewalAsync(
                        pixSubscription,
                        asaasPaymentId,
                        paymentEl,
                        cancellationToken);
                }
            }

            return null;
        }

        public async Task<Payment?> SyncAsaasChargeMetadataAsync(
            Payment payment,
            string asaasPaymentId,
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasStatus = GetString(paymentEl, AsaasWebhookPayloadKeys.Status);
            if (!ApplyAsaasChargeFieldUpdates(payment, asaasPaymentId, paymentEl, asaasStatus))
                return payment;

            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogInformation(
                "Pagamento local {PaymentId} sincronizado com cobrança Asaas {AsaasPaymentId}",
                payment.Id, asaasPaymentId);
            return payment;
        }

        public async Task<Payment?> HandlePaymentOverdueAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalReference = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");

            var payment = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalReference, pixQrCodeId, cancellationToken);
            if (payment == null)
                payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);

            if (payment == null)
                return null;

            payment = await SyncAsaasChargeMetadataAsync(payment, asaasPaymentId, paymentEl, cancellationToken)
                ?? payment;

            if (payment.Status == PaymentStatus.Paid
                || payment.Status == PaymentStatus.Processing
                || payment.Status == PaymentStatus.AwaitingValidation)
            {
                return payment;
            }

            var billingType = GetString(paymentEl, AsaasWebhookPayloadKeys.BillingType);
            var asaasSubscriptionId = GetString(paymentEl, AsaasWebhookPayloadKeys.Subscription);
            var isSubscriptionCreditCard = string.Equals(
                billingType, AsaasBillingType.CreditCard, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(asaasSubscriptionId)
                && asaasSubscriptionId.StartsWith("sub_", StringComparison.OrdinalIgnoreCase);

            if (payment.Status == PaymentStatus.Cancelled && isSubscriptionCreditCard)
            {
                payment.Status = PaymentStatus.Pending;
                await _paymentRepository.UpdateAsync(payment);
                _logger.LogInformation(
                    "Pagamento {PaymentId} reaberto como Pending após PAYMENT_OVERDUE (cartão assinatura {SubscriptionId})",
                    payment.Id, asaasSubscriptionId);
            }
            else if (payment.Status != PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Pending;
                await _paymentRepository.UpdateAsync(payment);
            }

            return payment;
        }

        public async Task<Payment?> HandlePaymentUpdatedAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalReference = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");
            var asaasStatus = GetString(paymentEl, AsaasWebhookPayloadKeys.Status);

            var payment = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalReference, pixQrCodeId, cancellationToken);
            if (payment == null)
                payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);

            if (payment == null)
                return null;

            var changed = ApplyAsaasChargeFieldUpdates(payment, asaasPaymentId, paymentEl, asaasStatus);
            if (changed)
            {
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                _logger.LogInformation(
                    "Pagamento local {PaymentId} sincronizado via PAYMENT_UPDATED (Asaas {AsaasPaymentId}, DueDate={DueDate}, Status={Status})",
                    payment.Id, asaasPaymentId, payment.DueDate, payment.Status);
            }

            if (IsAsaasPaidStatus(asaasStatus))
            {
                await _paymentService.ConfirmPaymentAsync(payment.Id, asaasPaymentId);
                payment = await _paymentRepository.GetByIdAsync(payment.Id, cancellationToken) ?? payment;
                _logger.LogInformation(
                    "Pagamento {PaymentId} confirmado via PAYMENT_UPDATED (status Asaas {AsaasStatus})",
                    payment.Id, asaasStatus);
            }
            else if (string.Equals(asaasStatus, AsaasPaymentStatus.Deleted, StringComparison.OrdinalIgnoreCase)
                && payment.Status == PaymentStatus.Pending)
            {
                await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                payment = await _paymentRepository.GetByIdAsync(payment.Id, cancellationToken) ?? payment;
                _logger.LogInformation(
                    "Pagamento {PaymentId} cancelado via PAYMENT_UPDATED (status DELETED)",
                    payment.Id);
            }
            else if (IsAsaasRejectedStatus(asaasStatus))
            {
                payment = await HandlePaymentRejectedAsync(
                    paymentEl, AsaasWebhookEvents.PaymentUpdated, cancellationToken) ?? payment;
            }

            return payment;
        }

        public async Task<Payment?> HandlePaymentRejectedAsync(
            JsonElement paymentEl,
            string? eventType = null,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalReference = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");
            var asaasStatus = GetString(paymentEl, AsaasWebhookPayloadKeys.Status);

            var payment = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalReference, pixQrCodeId, cancellationToken);
            if (payment == null)
                payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);

            if (payment == null)
                return null;

            // Idempotência: já Failed com o mesmo pay_* — não reincrementa tentativas.
            if (payment.Status == PaymentStatus.Failed
                && string.Equals(payment.ExternalPaymentId, asaasPaymentId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(payment.FailureReason))
                {
                    payment.FailureReason = ResolveRejectionFailureReason(eventType, asaasStatus);
                    await _paymentRepository.UpdateAsync(payment, cancellationToken);
                }

                _logger.LogInformation(
                    "Pagamento {PaymentId} já Failed para Asaas {AsaasPaymentId}; rejeição ignorada (idempotente).",
                    payment.Id, asaasPaymentId);
                return payment;
            }

            if (payment.Status == PaymentStatus.Paid
                || payment.Status == PaymentStatus.AwaitingValidation)
            {
                _logger.LogInformation(
                    "Pagamento {PaymentId} em status {Status}; rejeição Asaas {AsaasPaymentId} ignorada.",
                    payment.Id, payment.Status, asaasPaymentId);
                return payment;
            }

            ApplyAsaasChargeFieldUpdates(payment, asaasPaymentId, paymentEl, asaasStatus: null);

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = ResolveRejectionFailureReason(eventType, asaasStatus);
            if (!string.Equals(payment.ExternalPaymentId, asaasPaymentId, StringComparison.OrdinalIgnoreCase))
                payment.ExternalPaymentId = asaasPaymentId;

            await _paymentRepository.UpdateAsync(payment, cancellationToken);

            if (payment.SubscriptionId.HasValue)
            {
                var sub = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value, cancellationToken);
                if (sub != null)
                {
                    sub.Status = SubscriptionStatus.PastDue;
                    sub.FailedPaymentAttempts++;
                    await _subscriptionRepository.UpdateAsync(sub);
                    _logger.LogInformation(
                        "Assinatura {SubscriptionId} marcada PastDue após rejeição (tentativas={Attempts}).",
                        sub.Id, sub.FailedPaymentAttempts);
                }
            }

            _logger.LogInformation(
                "Pagamento {PaymentId} marcado Failed via {Event} (Asaas {AsaasPaymentId}, status={AsaasStatus}): {Reason}",
                payment.Id, eventType ?? "REJECTED", asaasPaymentId, asaasStatus, payment.FailureReason);

            return payment;
        }

        private async Task<Payment?> CreateSubscriptionRenewalPaymentAsync(
            string asaasSubscriptionId,
            string asaasPaymentId,
            JsonElement paymentEl,
            CancellationToken cancellationToken)
        {
            var existingByExternal = await _paymentRepository.GetByExternalPaymentIdAsync(asaasPaymentId, cancellationToken);
            if (existingByExternal != null)
                return existingByExternal;

            var subscription = await _subscriptionRepository.GetByExternalSubscriptionIdAsync(asaasSubscriptionId, cancellationToken);
            if (subscription == null)
            {
                _logger.LogWarning(
                    "Webhook Asaas: assinatura não encontrada para ExternalSubscriptionId={SubscriptionId}",
                    asaasSubscriptionId);
                return null;
            }

            var dueDate = ParseDueDate(paymentEl) ?? subscription.NextBillingDate;
            var duplicateCycle = subscription.Payments?
                .Where(p =>
                    p.Method == PaymentMethod.CreditCard
                    && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                    && (p.DueDate.Date == dueDate.Date
                        || string.Equals(p.ExternalPaymentId, asaasPaymentId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
            if (duplicateCycle != null)
            {
                return await SyncAsaasChargeMetadataAsync(duplicateCycle, asaasPaymentId, paymentEl, cancellationToken);
            }

            var amount = ResolveAmount(paymentEl, subscription.MonthlyAmount);
            var listPrice = subscription.Contract?.SnapshotListPrice ?? 0m;
            var payment = await _paymentService.CreatePaymentAsync(
                subscription.Id,
                amount,
                PaymentMethod.CreditCard,
                snapshotListPrice: listPrice);
            payment.ExternalPaymentId = asaasPaymentId;
            payment.DueDate = dueDate;
            payment.Status = PaymentStatus.Pending;
            await _paymentRepository.UpdateAsync(payment);

            _logger.LogInformation(
                "Pagamento local {PaymentId} criado para renovação cartão Asaas {AsaasPaymentId} (assinatura {SubscriptionId})",
                payment.Id, asaasPaymentId, subscription.Id);

            return payment;
        }

        private async Task<Payment?> CreateOrLinkPixAutomaticRenewalAsync(
            Subscription subscription,
            string asaasPaymentId,
            JsonElement paymentEl,
            CancellationToken cancellationToken)
        {
            var existingByExternal = await _paymentRepository.GetByExternalPaymentIdAsync(asaasPaymentId, cancellationToken);
            if (existingByExternal != null)
                return existingByExternal;

            var dueDate = ParseDueDate(paymentEl) ?? subscription.NextBillingDate;
            var pendingForCycle = subscription.Payments?
                .Where(p =>
                    p.Method == PaymentMethod.PIX
                    && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                    && p.DueDate.Date == dueDate.Date)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (pendingForCycle != null)
            {
                return await SyncAsaasChargeMetadataAsync(pendingForCycle, asaasPaymentId, paymentEl, cancellationToken);
            }

            var amount = ResolveAmount(paymentEl, subscription.MonthlyAmount);
            var listPrice = subscription.Contract?.SnapshotListPrice ?? 0m;
            var payment = await _paymentService.CreatePaymentAsync(
                subscription.Id,
                amount,
                PaymentMethod.PIX,
                snapshotListPrice: listPrice);
            payment.ExternalPaymentId = asaasPaymentId;
            payment.DueDate = dueDate;
            payment.Status = PaymentStatus.Pending;
            await _paymentRepository.UpdateAsync(payment);

            _logger.LogInformation(
                "Pagamento local {PaymentId} criado para renovação PIX Automático Asaas {AsaasPaymentId} (assinatura {SubscriptionId})",
                payment.Id, asaasPaymentId, subscription.Id);

            return payment;
        }

        private async Task<Subscription?> LoadPixAutomaticSubscriptionFromReferenceAsync(
            string? externalReference,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(externalReference) || !int.TryParse(externalReference, out var localPaymentId))
                return null;

            var referenced = await _paymentRepository.GetByIdAsync(localPaymentId, cancellationToken);
            var subscription = referenced?.Subscription;
            if (subscription == null && referenced?.SubscriptionId != null)
                subscription = await _subscriptionRepository.GetByIdAsync(referenced.SubscriptionId.Value, cancellationToken);

            if (subscription == null)
                return null;

            if (string.IsNullOrEmpty(subscription.PixAutomaticAuthorizationId))
                return null;

            if (subscription.Contract?.CardPlanType.IsPixAutomatic() != true)
                return null;

            return subscription;
        }

        private static bool ApplyAsaasChargeFieldUpdates(
            Payment payment,
            string asaasPaymentId,
            JsonElement paymentEl,
            string? asaasStatus)
        {
            var changed = false;

            if (!string.IsNullOrEmpty(asaasPaymentId)
                && !string.Equals(payment.ExternalPaymentId, asaasPaymentId, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(payment.ExternalPaymentId)
                    || !payment.ExternalPaymentId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase)))
            {
                payment.ExternalPaymentId = asaasPaymentId;
                changed = true;
            }

            var webhookDueDate = ParseDueDate(paymentEl);
            if (webhookDueDate.HasValue && payment.DueDate.Date != webhookDueDate.Value.Date)
            {
                payment.DueDate = webhookDueDate.Value;
                changed = true;
            }

            if (paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Value, out var valueEl)
                && valueEl.TryGetDecimal(out var webhookValue)
                && webhookValue > 0
                && payment.Amount != webhookValue)
            {
                payment.Amount = webhookValue;
                changed = true;
            }

            if (!string.IsNullOrEmpty(asaasStatus))
            {
                var mappedStatus = MapAsaasStatusToLocal(asaasStatus, payment.Status);
                if (mappedStatus.HasValue && payment.Status != mappedStatus.Value)
                {
                    payment.Status = mappedStatus.Value;
                    changed = true;
                }
            }

            return changed;
        }

        private static PaymentStatus? MapAsaasStatusToLocal(string asaasStatus, PaymentStatus currentStatus)
        {
            if (string.Equals(asaasStatus, AsaasPaymentStatus.Pending, StringComparison.OrdinalIgnoreCase)
                || string.Equals(asaasStatus, AsaasPaymentStatus.Overdue, StringComparison.OrdinalIgnoreCase))
            {
                if (currentStatus == PaymentStatus.Paid
                    || currentStatus == PaymentStatus.Processing
                    || currentStatus == PaymentStatus.AwaitingValidation)
                {
                    return null;
                }

                return PaymentStatus.Pending;
            }

            return null;
        }

        private static bool IsAsaasPaidStatus(string? asaasStatus)
        {
            return string.Equals(asaasStatus, AsaasPaymentStatus.Received, StringComparison.OrdinalIgnoreCase)
                || string.Equals(asaasStatus, AsaasPaymentStatus.Confirmed, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAsaasRejectedStatus(string? asaasStatus)
        {
            return string.Equals(asaasStatus, AsaasPaymentStatus.Refused, StringComparison.OrdinalIgnoreCase)
                || string.Equals(asaasStatus, AsaasPaymentStatus.Dunning, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRejectionFailureReason(string? eventType, string? asaasStatus)
        {
            if (string.Equals(
                    eventType,
                    AsaasWebhookEvents.PaymentCreditCardCaptureRefused,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Captura do cartão recusada pelo emissor";
            }

            if (string.Equals(asaasStatus, AsaasPaymentStatus.Refused, StringComparison.OrdinalIgnoreCase))
                return "Cobrança no cartão recusada pelo emissor";

            if (string.Equals(asaasStatus, AsaasPaymentStatus.Dunning, StringComparison.OrdinalIgnoreCase))
                return "Cobrança no cartão recusada pelo emissor";

            return "Cobrança no cartão recusada pelo emissor";
        }

        private static decimal ResolveAmount(JsonElement paymentEl, decimal fallback)
        {
            if (paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Value, out var valueEl)
                && valueEl.TryGetDecimal(out var webhookValue)
                && webhookValue > 0)
            {
                return webhookValue;
            }

            return fallback;
        }

        private static DateTime? ParseDueDate(JsonElement paymentEl)
        {
            if (!paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.DueDate, out var dueEl))
                return null;

            var raw = dueEl.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;

            if (DateTime.TryParse(raw, new CultureInfo("pt-BR"), DateTimeStyles.None, out parsed))
                return parsed;

            return null;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
        }

        private async Task<AsaasWebhookHandleResult> HandlePaymentEventAsync(
            JsonElement root,
            JsonElement paymentEl,
            string eventType,
            CancellationToken cancellationToken)
        {
            var paymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            var status = GetString(paymentEl, AsaasWebhookPayloadKeys.Status);
            var externalRef = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var asaasSubscriptionId = GetString(paymentEl, AsaasWebhookPayloadKeys.Subscription);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");

            if (string.IsNullOrEmpty(paymentId))
            {
                _logger.LogWarning("Webhook Asaas {Event} sem payment.id", eventType);
                return AsaasWebhookHandleResult.Done();
            }

            _logger.LogInformation(
                "Webhook Asaas: event={Event}, paymentId={PaymentId}, externalRef={ExternalRef}, subscription={Subscription}, pixQrCodeId={PixQrCodeId}, status={Status}",
                eventType, paymentId, externalRef, asaasSubscriptionId, pixQrCodeId, status);

            if (eventType == AsaasWebhookEvents.PaymentCreated)
            {
                var created = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);
                if (created != null)
                {
                    _logger.LogInformation(
                        "Pagamento local {LocalPaymentId} registrado via PAYMENT_CREATED",
                        created.Id);
                }

                return AsaasWebhookHandleResult.Done();
            }

            if (eventType == AsaasWebhookEvents.PaymentRejected
                || eventType == AsaasWebhookEvents.PaymentCreditCardCaptureRefused)
            {
                var payment = await HandlePaymentRejectedAsync(paymentEl, eventType, cancellationToken);
                if (payment == null)
                {
                    _logger.LogWarning(
                        "Webhook Asaas {Event}: pagamento não encontrado/criado para asaasPaymentId={PaymentId}, externalRef={ExternalRef}, subscription={Subscription}.",
                        eventType, paymentId, externalRef, asaasSubscriptionId);
                }
                else
                {
                    _logger.LogInformation(
                        "Pagamento {LocalPaymentId} rejeitado via {Event}: Status={Status}, FailureReason={Reason}",
                        payment.Id, eventType, payment.Status, payment.FailureReason);
                }

                return AsaasWebhookHandleResult.Done();
            }

            if (eventType == AsaasWebhookEvents.PaymentUpdated)
            {
                var payment = await HandlePaymentUpdatedAsync(paymentEl, cancellationToken);
                if (payment == null)
                {
                    _logger.LogWarning(
                        "Webhook Asaas PAYMENT_UPDATED: pagamento não encontrado/criado para asaasPaymentId={PaymentId}, externalRef={ExternalRef}.",
                        paymentId, externalRef);
                }
                else
                {
                    _logger.LogInformation(
                        "Pagamento {LocalPaymentId} atualizado via PAYMENT_UPDATED: DueDate={DueDate}, Status={Status}",
                        payment.Id, payment.DueDate, payment.Status);
                }

                var notifyCancelled = payment?.Status == PaymentStatus.Cancelled ? payment.Id : (int?)null;
                return AsaasWebhookHandleResult.Done(paymentIdForCancelledNotification: notifyCancelled);
            }

            if (eventType == AsaasWebhookEvents.PaymentRefunded)
            {
                await HandlePaymentRefundedAsync(paymentEl, cancellationToken);
                return AsaasWebhookHandleResult.Done();
            }

            if (eventType == AsaasWebhookEvents.PaymentRestored)
            {
                await HandlePaymentRestoredAsync(paymentEl, cancellationToken);
                return AsaasWebhookHandleResult.Done();
            }

            var paidEvents = new[]
            {
                AsaasWebhookEvents.PaymentReceived,
                AsaasWebhookEvents.PaymentConfirmed,
                AsaasWebhookEvents.PaymentReceivedInCash
            };

            if (paidEvents.Contains(eventType)
                && (status == AsaasPaymentStatus.Received || status == AsaasPaymentStatus.Confirmed))
            {
                var payment = await ResolveLocalPaymentAsync(paymentId, externalRef, pixQrCodeId, cancellationToken);
                if (payment == null)
                    payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);
                else
                    payment = await SyncAsaasChargeMetadataAsync(payment, paymentId, paymentEl, cancellationToken);

                if (payment != null)
                {
                    await _paymentService.ConfirmPaymentAsync(payment.Id, paymentId);
                    _logger.LogInformation("Pagamento {LocalPaymentId} confirmado via webhook Asaas ({Event})", payment.Id, eventType);
                }
                else
                {
                    _logger.LogWarning(
                        "Webhook Asaas pago sem pagamento local: paymentId={PaymentId}, subscription={Subscription}, externalRef={ExternalRef}",
                        paymentId, asaasSubscriptionId, externalRef);
                }

                return AsaasWebhookHandleResult.Done();
            }

            if (eventType == AsaasWebhookEvents.PaymentDeleted)
            {
                var payment = await ResolveLocalPaymentAsync(paymentId, externalRef, pixQrCodeId, cancellationToken);
                if (payment != null && payment.Status == PaymentStatus.Pending)
                {
                    await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                    _logger.LogInformation("Pagamento {LocalPaymentId} cancelado via PAYMENT_DELETED", payment.Id);
                    return AsaasWebhookHandleResult.Done(paymentIdForCancelledNotification: payment.Id);
                }

                return AsaasWebhookHandleResult.Done();
            }

            if (eventType == AsaasWebhookEvents.PaymentOverdue)
            {
                _logger.LogInformation(
                    "Webhook Asaas PAYMENT_OVERDUE recebido: asaasPaymentId={AsaasPaymentId}, externalRef={ExternalRef}.",
                    paymentId, externalRef);

                var payment = await HandlePaymentOverdueAsync(paymentEl, cancellationToken);
                if (payment == null)
                {
                    _logger.LogWarning(
                        "Webhook Asaas PAYMENT_OVERDUE: pagamento não encontrado/criado para asaasPaymentId={AsaasPaymentId}, externalRef={ExternalRef}.",
                        paymentId, externalRef);
                    return AsaasWebhookHandleResult.Done();
                }

                if (payment.Method == PaymentMethod.PIX && payment.Status == PaymentStatus.Pending)
                    return AsaasWebhookHandleResult.Done(paymentIdForPixRegeneratedNotification: payment.Id);

                _logger.LogInformation(
                    "Pagamento {LocalPaymentId} vencido via PAYMENT_OVERDUE: DueDate={DueDate}, status={Status}",
                    payment.Id, payment.DueDate, payment.Status);
                return AsaasWebhookHandleResult.Done();
            }

            if (IsKnownAsaasEvent(eventType))
                _logger.LogInformation("Webhook Asaas {Event} com payment recebido; sem ação adicional.", eventType);

            return AsaasWebhookHandleResult.Done();
        }

        private async Task HandlePixAuthorizationEventAsync(
            JsonElement authEl,
            string eventType,
            CancellationToken cancellationToken)
        {
            var (subscription, pendingPayment) = await ResolvePixAuthorizationContextAsync(authEl, cancellationToken);
            var authId = GetString(authEl, AsaasWebhookPayloadKeys.Id);
            var authStatus = GetString(authEl, AsaasWebhookPayloadKeys.Status);

            _logger.LogInformation(
                "Webhook PIX Automático autorização: event={Event}, authId={AuthId}, status={Status}, subscriptionId={SubscriptionId}, paymentId={PaymentId}",
                eventType, authId, authStatus, subscription?.Id, pendingPayment?.Id);

            if (eventType == AsaasWebhookEvents.PixAutomaticAuthorizationCreated)
            {
                if (subscription != null && !string.IsNullOrEmpty(authId)
                    && !string.Equals(subscription.PixAutomaticAuthorizationId, authId, StringComparison.OrdinalIgnoreCase))
                {
                    subscription.PixAutomaticAuthorizationId = authId;
                    SyncPixAuthorizationQrMetadata(subscription, authEl);
                    await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
                    _logger.LogInformation(
                        "Assinatura {SubscriptionId}: PixAutomaticAuthorizationId sincronizado via AUTHORIZATION_CREATED ({AuthId}).",
                        subscription.Id, authId);
                }
                else
                {
                    _logger.LogInformation(
                        "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CREATED recebido (authId={AuthId}); idempotência/log only.",
                        authId);
                }

                return;
            }

            if (eventType == AsaasWebhookEvents.PixAutomaticAuthorizationActivated)
            {
                if (pendingPayment != null)
                {
                    if (pendingPayment.Status == PaymentStatus.Paid)
                    {
                        _logger.LogInformation(
                            "Pagamento {PaymentId} já Paid; AUTHORIZATION_ACTIVATED ignorado (idempotente).",
                            pendingPayment.Id);
                        return;
                    }

                    await _paymentService.ConfirmPaymentAsync(pendingPayment.Id, null);
                    _logger.LogInformation(
                        "Pagamento {PaymentId} confirmado via PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED",
                        pendingPayment.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "AUTHORIZATION_ACTIVATED sem pagamento Pending local (authId={AuthId}).",
                        authId);
                }

                return;
            }

            if (eventType == AsaasWebhookEvents.PixAutomaticAuthorizationRefused
                || eventType == AsaasWebhookEvents.PixAutomaticAuthorizationExpired)
            {
                var failureReason = eventType == AsaasWebhookEvents.PixAutomaticAuthorizationRefused
                    ? AsaasPixAuthorizationFailureReasons.Refused
                    : AsaasPixAuthorizationFailureReasons.Expired;

                await ApplyPixAuthorizationFailureAsync(
                    subscription, pendingPayment, failureReason, cancellationToken);
                return;
            }

            if (eventType == AsaasWebhookEvents.PixAutomaticAuthorizationCancelled)
            {
                if (subscription != null)
                    ClearPixAuthorizationArtifacts(subscription, pendingPayment);

                if (pendingPayment != null
                    && pendingPayment.Status == PaymentStatus.Pending)
                {
                    pendingPayment.Status = PaymentStatus.Cancelled;
                    pendingPayment.FailureReason = AsaasPixAuthorizationFailureReasons.Cancelled;
                    await _paymentRepository.UpdateAsync(pendingPayment, cancellationToken);
                    _logger.LogInformation(
                        "Pagamento {PaymentId} cancelado via AUTHORIZATION_CANCELLED.",
                        pendingPayment.Id);
                }
                else if (pendingPayment != null
                    && pendingPayment.Status == PaymentStatus.Failed
                    && string.IsNullOrWhiteSpace(pendingPayment.FailureReason))
                {
                    pendingPayment.FailureReason = AsaasPixAuthorizationFailureReasons.Cancelled;
                    await _paymentRepository.UpdateAsync(pendingPayment, cancellationToken);
                }

                if (subscription != null)
                    await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

                return;
            }

            _logger.LogInformation("Evento PIX autorização {Event} recebido; sem ação adicional.", eventType);
        }

        private async Task<int?> HandlePixPaymentInstructionEventAsync(
            JsonElement root,
            string eventType,
            CancellationToken cancellationToken)
        {
            if (eventType == AsaasWebhookEvents.PixAutomaticPaymentInstructionRefused)
            {
                var refusedPayment = await HandlePixPaymentInstructionRefusedAsync(root, cancellationToken);
                return refusedPayment?.Id;
            }

            if (!root.TryGetProperty(AsaasWebhookPayloadKeys.PaymentInstruction, out var instructionEl))
            {
                _logger.LogWarning("Webhook {Event} sem paymentInstruction.", eventType);
                return null;
            }

            var asaasPaymentId = ResolveInstructionPaymentId(root, instructionEl);
            if (string.IsNullOrEmpty(asaasPaymentId))
            {
                _logger.LogInformation(
                    "Webhook {Event} sem paymentId na instrução; log only.",
                    eventType);
                return null;
            }

            var payment = await _paymentRepository.GetByExternalPaymentIdAsync(asaasPaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning(
                    "Webhook {Event}: pagamento local não encontrado para pay_* {PaymentId}.",
                    eventType, asaasPaymentId);
                return null;
            }

            if (eventType == AsaasWebhookEvents.PixAutomaticPaymentInstructionCreated
                || eventType == AsaasWebhookEvents.PixAutomaticPaymentInstructionScheduled)
            {
                var instructionDueDate = ParseDueDate(instructionEl);
                if (instructionDueDate.HasValue && payment.DueDate.Date != instructionDueDate.Value.Date)
                {
                    payment.DueDate = instructionDueDate.Value;
                    await _paymentRepository.UpdateAsync(payment, cancellationToken);
                    _logger.LogInformation(
                        "Pagamento {PaymentId}: DueDate sincronizado via {Event} para {DueDate}.",
                        payment.Id, eventType, payment.DueDate);
                }

                if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Processing)
                    return null;

                if (payment.Status != PaymentStatus.Pending)
                {
                    payment.Status = PaymentStatus.Pending;
                    await _paymentRepository.UpdateAsync(payment, cancellationToken);
                }

                _logger.LogInformation(
                    "Instrução PIX {Event} vinculada ao pagamento {PaymentId} (Pending).",
                    eventType, payment.Id);
                return null;
            }

            if (eventType == AsaasWebhookEvents.PixAutomaticPaymentInstructionCancelled)
            {
                if (payment.Status == PaymentStatus.Pending)
                {
                    await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, asaasPaymentId);
                    _logger.LogInformation(
                        "Pagamento {PaymentId} cancelado via INSTRUCTION_CANCELLED.",
                        payment.Id);
                    return payment.Id;
                }

                _logger.LogInformation(
                    "INSTRUCTION_CANCELLED para pagamento {PaymentId} em status {Status}; ignorado.",
                    payment.Id, payment.Status);
            }

            return null;
        }

        private async Task<Payment?> HandlePixPaymentInstructionRefusedAsync(
            JsonElement root,
            CancellationToken cancellationToken)
        {
            string? asaasPaymentId = null;
            if (root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var payEl))
                asaasPaymentId = GetString(payEl, AsaasWebhookPayloadKeys.Id);

            if (string.IsNullOrEmpty(asaasPaymentId)
                && root.TryGetProperty(AsaasWebhookPayloadKeys.PaymentInstruction, out var instrEl))
            {
                asaasPaymentId = ResolveInstructionPaymentId(root, instrEl);
            }

            if (string.IsNullOrEmpty(asaasPaymentId))
            {
                _logger.LogWarning("INSTRUCTION_REFUSED sem paymentId identificável.");
                return null;
            }

            var payment = await _paymentRepository.GetByExternalPaymentIdAsync(asaasPaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning(
                    "INSTRUCTION_REFUSED: pagamento local não encontrado para pay_* {PaymentId}.",
                    asaasPaymentId);
                return null;
            }

            if (payment.Status == PaymentStatus.Failed
                && string.Equals(payment.ExternalPaymentId, asaasPaymentId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Pagamento {PaymentId} já Failed para INSTRUCTION_REFUSED; ignorado (idempotente).",
                    payment.Id);
                return payment;
            }

            if (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.AwaitingValidation)
            {
                _logger.LogInformation(
                    "Pagamento {PaymentId} em {Status}; INSTRUCTION_REFUSED ignorado.",
                    payment.Id, payment.Status);
                return payment;
            }

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Débito PIX Automático recusado (ex.: saldo insuficiente).";
                payment.ExternalPaymentId = asaasPaymentId;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);

                if (payment.SubscriptionId.HasValue)
                {
                    var sub = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value, cancellationToken);
                    if (sub != null)
                    {
                        sub.Status = SubscriptionStatus.PastDue;
                        sub.FailedPaymentAttempts++;
                        await _subscriptionRepository.UpdateAsync(sub, cancellationToken);
                    }
                }

                _logger.LogInformation(
                    "Pagamento {PaymentId} marcado Failed via INSTRUCTION_REFUSED; assinatura PastDue.",
                    payment.Id);
            }

            return payment;
        }

        private async Task HandleSubscriptionEventAsync(
            JsonElement subscriptionEl,
            string eventType,
            CancellationToken cancellationToken)
        {
            var asaasSubscriptionId = GetString(subscriptionEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasSubscriptionId))
            {
                _logger.LogWarning("Webhook {Event} sem subscription.id", eventType);
                return;
            }

            var subscription = await _subscriptionRepository.GetByExternalSubscriptionIdAsync(
                asaasSubscriptionId, cancellationToken);

            if (eventType == AsaasWebhookEvents.SubscriptionCreated)
            {
                if (subscription != null)
                {
                    _logger.LogInformation(
                        "SUBSCRIPTION_CREATED: assinatura local {SubscriptionId} já vinculada a {ExternalId}.",
                        subscription.Id, asaasSubscriptionId);
                }
                else
                {
                    _logger.LogInformation(
                        "SUBSCRIPTION_CREATED recebido para {ExternalId}; sem assinatura local (log only).",
                        asaasSubscriptionId);
                }

                return;
            }

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Webhook {Event}: assinatura local não encontrada para ExternalSubscriptionId={ExternalId}.",
                    eventType, asaasSubscriptionId);
                return;
            }

            if (eventType == AsaasWebhookEvents.SubscriptionInactivated
                || eventType == AsaasWebhookEvents.SubscriptionDeleted)
            {
                if (subscription.Status == SubscriptionStatus.Cancelled)
                {
                    _logger.LogInformation(
                        "Assinatura {SubscriptionId} já Cancelled; {Event} ignorado (idempotente).",
                        subscription.Id, eventType);
                    return;
                }

                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.CancelledAt = DateTime.UtcNow;
                subscription.CancellationReason = eventType == AsaasWebhookEvents.SubscriptionDeleted
                    ? "Assinatura removida no Asaas (SUBSCRIPTION_DELETED)."
                    : "Assinatura inativada no Asaas (SUBSCRIPTION_INACTIVATED).";
                await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
                _logger.LogInformation(
                    "Assinatura {SubscriptionId} marcada Cancelled via {Event} (contrato não alterado).",
                    subscription.Id, eventType);
                return;
            }

            if (eventType == AsaasWebhookEvents.SubscriptionUpdated)
            {
                var changed = false;
                var nextDueDate = ParseDueDateFromProperty(subscriptionEl, AsaasWebhookPayloadKeys.NextDueDate);
                if (nextDueDate.HasValue && subscription.NextBillingDate.Date != nextDueDate.Value.Date)
                {
                    subscription.NextBillingDate = nextDueDate.Value;
                    changed = true;
                }

                if (subscriptionEl.TryGetProperty(AsaasWebhookPayloadKeys.Value, out var valueEl)
                    && valueEl.TryGetDecimal(out var webhookValue)
                    && webhookValue > 0
                    && subscription.MonthlyAmount != webhookValue)
                {
                    subscription.MonthlyAmount = webhookValue;
                    changed = true;
                }

                var asaasStatus = GetString(subscriptionEl, AsaasWebhookPayloadKeys.Status);
                if (string.Equals(asaasStatus, AsaasSubscriptionStatus.Inactive, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(asaasStatus, AsaasSubscriptionStatus.Expired, StringComparison.OrdinalIgnoreCase))
                {
                    if (subscription.Status != SubscriptionStatus.Cancelled)
                    {
                        subscription.Status = SubscriptionStatus.Cancelled;
                        subscription.CancelledAt = DateTime.UtcNow;
                        subscription.CancellationReason = $"Assinatura {asaasStatus} no Asaas (SUBSCRIPTION_UPDATED).";
                        changed = true;
                    }
                }

                if (changed)
                {
                    await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
                    _logger.LogInformation(
                        "Assinatura {SubscriptionId} sincronizada via SUBSCRIPTION_UPDATED (NextBillingDate={NextBilling}, Amount={Amount}, Status={Status}).",
                        subscription.Id, subscription.NextBillingDate, subscription.MonthlyAmount, subscription.Status);
                }
                else
                {
                    _logger.LogInformation(
                        "SUBSCRIPTION_UPDATED para assinatura {SubscriptionId}; sem alterações locais.",
                        subscription.Id);
                }
            }
        }

        private async Task<Payment?> HandlePaymentRefundedAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalRef = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");

            var payment = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalRef, pixQrCodeId, cancellationToken);
            if (payment == null)
                payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);

            if (payment == null)
                return null;

            if (payment.Status == PaymentStatus.Refunded)
            {
                _logger.LogInformation(
                    "Pagamento {PaymentId} já Refunded; PAYMENT_REFUNDED ignorado (idempotente).",
                    payment.Id);
                return payment;
            }

            ApplyAsaasChargeFieldUpdates(payment, asaasPaymentId, paymentEl, AsaasPaymentStatus.Refunded);
            payment.Status = PaymentStatus.Refunded;
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            _logger.LogInformation(
                "Pagamento {PaymentId} marcado Refunded via PAYMENT_REFUNDED (Asaas {AsaasPaymentId}).",
                payment.Id, asaasPaymentId);
            return payment;
        }

        private async Task<Payment?> HandlePaymentRestoredAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default)
        {
            var asaasPaymentId = GetString(paymentEl, AsaasWebhookPayloadKeys.Id);
            if (string.IsNullOrEmpty(asaasPaymentId))
                return null;

            var externalRef = GetString(paymentEl, AsaasWebhookPayloadKeys.ExternalReference);
            var pixQrCodeId = GetString(paymentEl, "pixQrCodeId");

            var payment = await ResolveLocalPaymentAsync(
                asaasPaymentId, externalRef, pixQrCodeId, cancellationToken);
            if (payment == null)
                payment = await EnsureLocalPaymentFromAsaasChargeAsync(paymentEl, cancellationToken);

            if (payment == null)
                return null;

            if (payment.Status == PaymentStatus.Paid
                || payment.Status == PaymentStatus.Processing
                || payment.Status == PaymentStatus.AwaitingValidation)
            {
                _logger.LogInformation(
                    "Pagamento {PaymentId} em {Status}; PAYMENT_RESTORED ignorado.",
                    payment.Id, payment.Status);
                return payment;
            }

            payment = await SyncAsaasChargeMetadataAsync(payment, asaasPaymentId, paymentEl, cancellationToken)
                ?? payment;

            if (payment.Status == PaymentStatus.Cancelled || payment.Status == PaymentStatus.Failed)
            {
                payment.Status = PaymentStatus.Pending;
                payment.FailureReason = null;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                _logger.LogInformation(
                    "Pagamento {PaymentId} reaberto como Pending via PAYMENT_RESTORED.",
                    payment.Id);
            }
            else if (payment.Status != PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Pending;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                _logger.LogInformation(
                    "Pagamento {PaymentId} normalizado para Pending via PAYMENT_RESTORED.",
                    payment.Id);
            }

            return payment;
        }

        private async Task ApplyPixAuthorizationFailureAsync(
            Subscription? subscription,
            Payment? pendingPayment,
            string failureReason,
            CancellationToken cancellationToken)
        {
            if (subscription != null)
                ClearPixAuthorizationArtifacts(subscription, pendingPayment);

            if (pendingPayment != null)
            {
                if (pendingPayment.Status == PaymentStatus.Failed
                    && string.Equals(pendingPayment.FailureReason, failureReason, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "Pagamento {PaymentId} já Failed com mesmo motivo; ignorado (idempotente).",
                        pendingPayment.Id);
                }
                else if (pendingPayment.Status != PaymentStatus.Paid
                    && pendingPayment.Status != PaymentStatus.AwaitingValidation)
                {
                    pendingPayment.Status = PaymentStatus.Failed;
                    pendingPayment.FailureReason = failureReason;
                    await _paymentRepository.UpdateAsync(pendingPayment, cancellationToken);
                    _logger.LogInformation(
                        "Pagamento {PaymentId} marcado Failed: {Reason}",
                        pendingPayment.Id, failureReason);
                }
            }

            if (subscription != null)
                await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }

        private async Task<(Subscription? subscription, Payment? pendingPayment)> ResolvePixAuthorizationContextAsync(
            JsonElement authEl,
            CancellationToken cancellationToken)
        {
            Subscription? subscription = null;
            Payment? pendingPayment = null;

            var authId = GetString(authEl, AsaasWebhookPayloadKeys.Id);
            if (!string.IsNullOrEmpty(authId))
                subscription = await _subscriptionRepository.GetByPixAutomaticAuthorizationIdAsync(authId, cancellationToken);

            var conciliationId = GetStringFromNested(authEl, AsaasWebhookPayloadKeys.ImmediateQrCode, AsaasWebhookPayloadKeys.ConciliationIdentifier);
            if (subscription == null && !string.IsNullOrEmpty(conciliationId))
            {
                var paymentByConciliation = await _paymentRepository.GetByPixConciliationIdentifierAsync(
                    conciliationId, cancellationToken);
                if (paymentByConciliation?.SubscriptionId != null)
                {
                    pendingPayment = paymentByConciliation;
                    subscription = paymentByConciliation.Subscription
                        ?? await _subscriptionRepository.GetByIdAsync(paymentByConciliation.SubscriptionId.Value, cancellationToken);
                }
            }

            var asaasContractId = GetString(authEl, AsaasWebhookPayloadKeys.ContractId);
            if (subscription == null && !string.IsNullOrEmpty(asaasContractId))
            {
                var localContractId = TryParseLocalContractIdFromAsaasContractId(asaasContractId);
                if (localContractId.HasValue)
                    subscription = await _subscriptionRepository.GetByContractIdAsync(localContractId.Value, cancellationToken);
            }

            if (subscription == null && !string.IsNullOrEmpty(conciliationId))
            {
                subscription = await _subscriptionRepository.GetByPixAutomaticConciliationIdentifierAsync(
                    conciliationId, cancellationToken);
            }

            pendingPayment ??= subscription?.Payments?
                .Where(p => p.Status == PaymentStatus.Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (pendingPayment == null && subscription != null)
            {
                pendingPayment = subscription.Payments?
                    .Where(p => p.Status == PaymentStatus.Failed || p.Status == PaymentStatus.Processing)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
            }

            return (subscription, pendingPayment);
        }

        private static void SyncPixAuthorizationQrMetadata(Subscription subscription, JsonElement authEl)
        {
            var conciliationId = GetStringFromNested(authEl, AsaasWebhookPayloadKeys.ImmediateQrCode, AsaasWebhookPayloadKeys.ConciliationIdentifier);
            if (!string.IsNullOrEmpty(conciliationId))
                subscription.PixAutomaticConciliationIdentifier = conciliationId;

            var expirationRaw = GetStringFromNested(authEl, AsaasWebhookPayloadKeys.ImmediateQrCode, "expirationDate");
            if (!string.IsNullOrWhiteSpace(expirationRaw)
                && DateTime.TryParse(expirationRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiration))
            {
                subscription.PixAutomaticPayloadExpiresAt = expiration;
            }
        }

        private static void ClearPixAuthorizationArtifacts(Subscription subscription, Payment? payment)
        {
            subscription.PixAutomaticAuthorizationId = null;
            subscription.PixAutomaticPayload = null;
            subscription.PixAutomaticPayloadExpiresAt = null;
            subscription.PixAutomaticConciliationIdentifier = null;

            if (payment == null)
                return;

            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.PixConciliationIdentifier = null;
        }

        private static string? ResolveInstructionPaymentId(JsonElement root, JsonElement instructionEl)
        {
            var paymentId = GetString(instructionEl, AsaasWebhookPayloadKeys.PaymentId);
            if (!string.IsNullOrEmpty(paymentId))
                return paymentId;

            if (instructionEl.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var nestedPay)
                && nestedPay.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var nestedId))
            {
                return nestedId.GetString();
            }

            if (root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var payEl))
                return GetString(payEl, AsaasWebhookPayloadKeys.Id);

            return null;
        }

        private static int? TryParseLocalContractIdFromAsaasContractId(string asaasContractId)
        {
            var parts = asaasContractId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part.Length == 8 && part.All(Uri.IsHexDigit))
                    continue;

                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var contractId) && contractId > 0)
                    return contractId;
            }

            return null;
        }

        private static DateTime? ParseDueDateFromProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var dueEl))
                return null;

            var raw = dueEl.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;

            if (DateTime.TryParse(raw, new CultureInfo("pt-BR"), DateTimeStyles.None, out parsed))
                return parsed;

            return null;
        }

        private static string? GetStringFromNested(JsonElement element, string objectName, string propertyName)
        {
            return element.TryGetProperty(objectName, out var nested)
                ? GetString(nested, propertyName)
                : null;
        }

        private static bool IsPixAuthorizationEvent(string eventType) =>
            string.Equals(eventType, AsaasWebhookEvents.PixAutomaticAuthorizationCreated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticAuthorizationActivated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticAuthorizationCancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticAuthorizationExpired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticAuthorizationRefused, StringComparison.OrdinalIgnoreCase);

        private static bool IsPixInstructionEvent(string eventType) =>
            string.Equals(eventType, AsaasWebhookEvents.PixAutomaticPaymentInstructionCreated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticPaymentInstructionScheduled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticPaymentInstructionRefused, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PixAutomaticPaymentInstructionCancelled, StringComparison.OrdinalIgnoreCase);

        private static bool IsSubscriptionEvent(string eventType) =>
            string.Equals(eventType, AsaasWebhookEvents.SubscriptionCreated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.SubscriptionUpdated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.SubscriptionInactivated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.SubscriptionDeleted, StringComparison.OrdinalIgnoreCase);

        private static bool IsKnownAsaasEvent(string eventType) =>
            IsPixAuthorizationEvent(eventType)
            || IsPixInstructionEvent(eventType)
            || IsSubscriptionEvent(eventType)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentCreated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentUpdated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentReceived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentConfirmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentReceivedInCash, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentDeleted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentOverdue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentRejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentCreditCardCaptureRefused, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentRefunded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, AsaasWebhookEvents.PaymentRestored, StringComparison.OrdinalIgnoreCase);
    }
}
