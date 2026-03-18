using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Api.Hubs;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using System.Text.Json;
namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly ILogger<PaymentWebhookController> _logger;

        public PaymentWebhookController(
            IPaymentService paymentService,
            IAsaasService asaasService,
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            IHubContext<PaymentHub> hubContext,
            ILogger<PaymentWebhookController> logger)
        {
            _paymentService = paymentService;
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _hubContext = hubContext;
            _logger = logger;
        }
        [HttpPost("stripe")]
        public async Task<IActionResult> StripeWebhook([FromBody] dynamic webhookData, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Webhook do Stripe recebido");
                return Ok(OkMessage.WebhookReceivedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do Stripe");
                return StatusCode(500, new { error = ErrorJson.ErrorProcessingWebhook });
            }
        }
        [HttpPost("pagseguro")]
        public async Task<IActionResult> PagSeguroWebhook([FromBody] dynamic webhookData, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Webhook do PagSeguro recebido");
                return Ok(OkMessage.WebhookReceivedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do PagSeguro");
                return StatusCode(500, new { error = ErrorJson.ErrorProcessingWebhook });
            }
        }
        [HttpPost("asaas")]
        public async Task<IActionResult> AsaasWebhook(CancellationToken cancellationToken = default)
        {
            string payload;
            try
            {
                using var reader = new StreamReader(Request.Body);
                payload = await reader.ReadToEndAsync(cancellationToken);
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
                var valid = await _asaasService.ValidateWebhookSignatureAsync(payload, signature ?? "");
                if (!valid)
                {
                    _logger.LogWarning("Webhook Asaas com assinatura inválida");
                    return Unauthorized();
                }

                if (eventType == AsaasWebhookEvents.PixAutomaticAuthorizationActivated && root.TryGetProperty("authorization", out var authEl))
                {
                    var authId = authEl.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var authIdEl) ? authIdEl.GetString() : null;
                    if (!string.IsNullOrEmpty(authId))
                    {
                        var subscription = await _subscriptionRepository.GetByPixAutomaticAuthorizationIdAsync(authId, cancellationToken);
                        var pendingPayment = subscription?.Payments?.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                        if (pendingPayment != null)
                        {
                            await _paymentService.ConfirmPaymentAsync(pendingPayment.Id, null);
                            _logger.LogInformation("Pagamento {PaymentId} confirmado via webhook PIX Automático (autorização ativada)", pendingPayment.Id);
                            try
                            {
                                await _hubContext.Clients.Group($"payment_{pendingPayment.Id}").SendAsync(PaymentHub.PaymentConfirmed, new { paymentId = pendingPayment.Id });
                            }
                            catch (Exception exHub)
                            {
                                _logger.LogWarning(exHub, "Falha ao notificar SignalR para pagamento {PaymentId}", pendingPayment.Id);
                            }
                        }
                    }
                    return Ok();
                }

                if (eventType == AsaasWebhookEvents.PixAutomaticPaymentInstructionRefused)
                {
                    string? asaasPaymentId = null;
                    if (root.TryGetProperty("payment", out var payEl))
                        asaasPaymentId = payEl.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var pid) ? pid.GetString() : null;
                    if (!string.IsNullOrEmpty(asaasPaymentId))
                    {
                        var payment = await _paymentRepository.GetByExternalPaymentIdAsync(asaasPaymentId, cancellationToken);
                        if (payment != null && payment.Status == PaymentStatus.Pending)
                        {
                            await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Failed, asaasPaymentId);
                            if (payment.SubscriptionId.HasValue)
                            {
                                var sub = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value, cancellationToken);
                                if (sub != null)
                                {
                                    sub.Status = SubscriptionStatus.PastDue;
                                    sub.FailedPaymentAttempts++;
                                    await _subscriptionRepository.UpdateAsync(sub);
                                }
                            }
                            _logger.LogInformation("Pagamento {PaymentId} recusado (PIX Automático sem saldo); assinatura em atraso.", payment.Id);
                        }
                    }
                    return Ok();
                }

                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var paymentEl))
                {
                    _logger.LogWarning("Webhook Asaas sem objeto payment");
                    return Ok();
                }
                var paymentId = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var idEl) ? idEl.GetString() : null;
                var status = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Status, out var st) ? st.GetString() : null;
                var externalRef = paymentEl.TryGetProperty("externalReference", out var refEl) ? refEl.GetString() : null;
                if (string.IsNullOrEmpty(paymentId))
                {
                    _logger.LogWarning("Webhook Asaas sem payment.id");
                    return Ok();
                }
                var pixQrCodeId = paymentEl.TryGetProperty("pixQrCodeId", out var qrEl) ? qrEl.GetString() : null;
                _logger.LogInformation("Webhook Asaas: event={Event}, paymentId={PaymentId}, externalRef={ExternalRef}, pixQrCodeId={PixQrCodeId}, status={Status}", eventType, paymentId, externalRef, pixQrCodeId, status);
                var paidEvents = new[] { AsaasWebhookEvents.PaymentReceived, AsaasWebhookEvents.PaymentConfirmed, AsaasWebhookEvents.PaymentReceivedInCash };
                if (paidEvents.Contains(eventType ?? "") && (status == AsaasPaymentStatus.Received || status == AsaasPaymentStatus.Confirmed))
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId, externalRef);
                    var foundByPixQrCodeId = false;
                    // PIX Automático: cobrança criada pelo Asaas ao pagar o QR vem com externalReference null; buscar por pixQrCodeId (conciliationIdentifier)
                    if (payment == null && !string.IsNullOrEmpty(pixQrCodeId))
                    {
                        payment = await _paymentRepository.GetByPixConciliationIdentifierAsync(pixQrCodeId, cancellationToken);
                        foundByPixQrCodeId = payment != null;
                    }
                    if (payment != null && payment.Status != PaymentStatus.Paid)
                    {
                        await _paymentService.ConfirmPaymentAsync(payment.Id, paymentId);
                        if (foundByPixQrCodeId)
                        {
                            payment = await _paymentRepository.GetByIdAsync(payment.Id, cancellationToken);
                            if (payment != null && payment.ExternalPaymentId != paymentId)
                            {
                                payment.ExternalPaymentId = paymentId;
                                await _paymentRepository.UpdateAsync(payment);
                            }
                        }
                        _logger.LogInformation("Pagamento {PaymentId} confirmado via webhook Asaas (api/PaymentWebhook/asaas)", payment.Id);
                        try
                        {
                            await _hubContext.Clients.Group($"payment_{payment.Id}").SendAsync(PaymentHub.PaymentConfirmed, new { paymentId = payment.Id });
                        }
                        catch (Exception exHub)
                        {
                            _logger.LogWarning(exHub, "Falha ao notificar SignalR para pagamento {PaymentId}", payment.Id);
                        }
                    }
                }
                else if (eventType == AsaasWebhookEvents.PaymentDeleted || eventType == AsaasWebhookEvents.PaymentOverdue)
                {
                    if (eventType == AsaasWebhookEvents.PaymentOverdue)
                        _logger.LogInformation("Webhook Asaas PAYMENT_OVERDUE recebido: asaasPaymentId={AsaasPaymentId}, externalRef={ExternalRef}. Buscando pagamento e gerando novo código PIX se for PIX pendente.", paymentId, externalRef);
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId, externalRef);
                    if (payment == null && !string.IsNullOrEmpty(pixQrCodeId))
                        payment = await _paymentRepository.GetByPixConciliationIdentifierAsync(pixQrCodeId, cancellationToken);
                    if (payment == null)
                    {
                        if (eventType == AsaasWebhookEvents.PaymentOverdue)
                            _logger.LogWarning("Webhook Asaas PAYMENT_OVERDUE: pagamento não encontrado para asaasPaymentId={AsaasPaymentId}, externalRef={ExternalRef}. Verifique se ExternalPaymentId ou referência está correto na base.", paymentId, externalRef);
                    }
                    else if (payment.Status == PaymentStatus.Pending)
                    {
                        if (eventType == AsaasWebhookEvents.PaymentOverdue && payment.Method == PaymentMethod.PIX)
                        {
                            try
                            {
                                await _paymentService.HandlePixPaymentOverdueAsync(payment.Id);
                                _logger.LogInformation("Pagamento PIX {PaymentId} vencido: data de vencimento atualizada e novo código gerado via Asaas (api/PaymentWebhook/asaas)", payment.Id);
                                try
                                {
                                    await _hubContext.Clients.Group($"payment_{payment.Id}").SendAsync(PaymentHub.PaymentPixCodeRegenerated, new { paymentId = payment.Id });
                                }
                                catch (Exception exHub)
                                {
                                    _logger.LogWarning(exHub, "Falha ao notificar SignalR (PIX regenerado) para pagamento {PaymentId}", payment.Id);
                                }
                            }
                            catch (Exception exPix)
                            {
                                _logger.LogError(exPix, "Erro ao tratar vencimento PIX do pagamento {PaymentId}; cancelando.", payment.Id);
                                await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                                try
                                {
                                    await _hubContext.Clients.Group($"payment_{payment.Id}").SendAsync(PaymentHub.PaymentCancelled, new { paymentId = payment.Id });
                                }
                                catch (Exception exHub)
                                {
                                    _logger.LogWarning(exHub, "Falha ao notificar SignalR (cancelado) para pagamento {PaymentId}", payment.Id);
                                }
                            }
                        }
                        else
                        {
                            await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                            _logger.LogInformation("Pagamento {PaymentId} cancelado/vencido via webhook Asaas (api/PaymentWebhook/asaas)", payment.Id);
                            try
                            {
                                await _hubContext.Clients.Group($"payment_{payment.Id}").SendAsync(PaymentHub.PaymentCancelled, new { paymentId = payment.Id });
                            }
                            catch (Exception exHub)
                            {
                                _logger.LogWarning(exHub, "Falha ao notificar SignalR (cancelado) para pagamento {PaymentId}", payment.Id);
                            }
                        }
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
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Erro ao processar webhook do Asaas: {Message}", ex.Message);
                return StatusCode(500, new { error = ErrorJson.ErrorProcessingWebhook, detail = innerMsg });
            }
            return Ok();
        }
    }
}