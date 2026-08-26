using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using System.Text.Json;

namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AsaasWebhookController : ControllerBase
    {
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<AsaasWebhookController> _logger;

        public AsaasWebhookController(
            IAsaasService asaasService,
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            IPaymentService paymentService,
            ILogger<AsaasWebhookController> logger)
        {
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
        {
            string payload;
            try
            {
                using var reader = new StreamReader(Request.Body);
                payload = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ler corpo do webhook Asaas");
                return BadRequest();
            }

            var signature = Request.Headers[HttpHeaderNames.AsaasAccessToken].FirstOrDefault();
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var eventType = root.TryGetProperty(AsaasWebhookPayloadKeys.Event, out var ev) ? ev.GetString() : null;
                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var paymentEl))
                {
                    _logger.LogWarning("Webhook Asaas sem objeto payment");
                    return Ok();
                }

                var paymentId = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var idEl) ? idEl.GetString() : null;
                var status = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Status, out var st) ? st.GetString() : null;
                var asaasSubscriptionId = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Subscription, out var subEl)
                    ? subEl.GetString()
                    : null;

                if (string.IsNullOrEmpty(paymentId))
                {
                    _logger.LogWarning("Webhook Asaas sem payment.id");
                    return Ok();
                }

                _logger.LogInformation(
                    "Webhook Asaas: event={Event}, paymentId={PaymentId}, status={Status}, subscription={Subscription}",
                    eventType, paymentId, status, asaasSubscriptionId);

                var valid = await _asaasService.ValidateWebhookSignatureAsync(payload, signature ?? "");
                if (!valid)
                {
                    _logger.LogWarning("Webhook Asaas com assinatura inválida");
                    return Unauthorized();
                }

                var paidEvents = new[]
                {
                    AsaasWebhookEvents.PaymentReceived,
                    AsaasWebhookEvents.PaymentConfirmed,
                    AsaasWebhookEvents.PaymentReceivedInCash
                };

                if (paidEvents.Contains(eventType ?? "") && (status == AsaasPaymentStatus.Received || status == AsaasPaymentStatus.Confirmed))
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId);
                    if (payment == null && !string.IsNullOrEmpty(asaasSubscriptionId))
                    {
                        payment = await CreateLocalPaymentFromAsaasSubscriptionRenewalAsync(
                            asaasSubscriptionId,
                            paymentId,
                            paymentEl,
                            cancellationToken);
                    }

                    if (payment != null)
                    {
                        await _paymentService.ConfirmPaymentAsync(payment.Id, paymentId);
                        _logger.LogInformation("Pagamento {PaymentId} confirmado via webhook Asaas", payment.Id);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Webhook Asaas pago sem pagamento local: paymentId={PaymentId}, subscription={Subscription}",
                            paymentId, asaasSubscriptionId);
                    }
                }
                else if (eventType == AsaasWebhookEvents.PaymentDeleted || eventType == AsaasWebhookEvents.PaymentOverdue)
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId);
                    if (payment != null && payment.Status == PaymentStatus.Pending)
                    {
                        await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                        _logger.LogInformation("Pagamento {PaymentId} cancelado/vencido via webhook Asaas", payment.Id);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Webhook Asaas com payload inválido");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook Asaas");
                return StatusCode(500);
            }

            return Ok();
        }

        /// <summary>
        /// Renovações de assinatura de cartão no Asaas chegam com payment.id novo e payment.subscription = sub_...
        /// Cria o Payment local vinculado à assinatura para Confirmar e manter o contrato em dia.
        /// </summary>
        private async Task<Domain.Entities.Payment?> CreateLocalPaymentFromAsaasSubscriptionRenewalAsync(
            string asaasSubscriptionId,
            string asaasPaymentId,
            JsonElement paymentEl,
            CancellationToken cancellationToken)
        {
            var subscription = await _subscriptionRepository.GetByExternalSubscriptionIdAsync(asaasSubscriptionId, cancellationToken);
            if (subscription == null)
                return null;

            var amount = subscription.MonthlyAmount;
            if (paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Value, out var valueEl)
                && valueEl.TryGetDecimal(out var webhookValue)
                && webhookValue > 0)
            {
                amount = webhookValue;
            }

            var payment = await _paymentService.CreatePaymentAsync(
                subscription.Id,
                amount,
                PaymentMethod.CreditCard);
            payment.ExternalPaymentId = asaasPaymentId;
            payment.Status = PaymentStatus.Pending;
            await _paymentRepository.UpdateAsync(payment);

            subscription.LastBillingDate = DateTime.UtcNow;
            var isMonthly = string.Equals(subscription.BillingCycle, "Monthly", StringComparison.OrdinalIgnoreCase)
                || subscription.Contract?.CardPlanType == ContractCardPlanType.CardMonthly;
            subscription.NextBillingDate = isMonthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1);
            subscription.FailedPaymentAttempts = 0;
            if (subscription.Status == SubscriptionStatus.PastDue)
                subscription.Status = SubscriptionStatus.Active;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _subscriptionRepository.UpdateAsync(subscription);

            _logger.LogInformation(
                "Pagamento local {PaymentId} criado para renovação Asaas {AsaasPaymentId} da assinatura {SubscriptionId}",
                payment.Id, asaasPaymentId, asaasSubscriptionId);

            return payment;
        }
    }
}
