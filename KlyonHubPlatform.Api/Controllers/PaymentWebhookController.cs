using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Constants;
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
        private readonly ILogger<PaymentWebhookController> _logger;
        public PaymentWebhookController(
            IPaymentService paymentService,
            IAsaasService asaasService,
            IPaymentRepository paymentRepository,
            ILogger<PaymentWebhookController> logger)
        {
            _paymentService = paymentService;
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
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
                if (!root.TryGetProperty(AsaasWebhookPayloadKeys.Payment, out var paymentEl))
                {
                    _logger.LogWarning("Webhook Asaas sem objeto payment");
                    return Ok();
                }
                var paymentId = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Id, out var idEl) ? idEl.GetString() : null;
                var status = paymentEl.TryGetProperty(AsaasWebhookPayloadKeys.Status, out var st) ? st.GetString() : null;
                if (string.IsNullOrEmpty(paymentId))
                {
                    _logger.LogWarning("Webhook Asaas sem payment.id");
                    return Ok();
                }
                _logger.LogInformation("Webhook Asaas: event={Event}, paymentId={PaymentId}, status={Status}", eventType, paymentId, status);
                var valid = await _asaasService.ValidateWebhookSignatureAsync(payload, signature ?? "");
                if (!valid)
                {
                    _logger.LogWarning("Webhook Asaas com assinatura inválida");
                    return Unauthorized();
                }
                var paidEvents = new[] { AsaasWebhookEvents.PaymentReceived, AsaasWebhookEvents.PaymentConfirmed, AsaasWebhookEvents.PaymentReceivedInCash };
                if (paidEvents.Contains(eventType ?? "") && (status == AsaasPaymentStatus.Received || status == AsaasPaymentStatus.Confirmed))
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId);
                    if (payment != null && payment.Status != PaymentStatus.Paid)
                    {
                        await _paymentService.ConfirmPaymentAsync(payment.Id, paymentId);
                        _logger.LogInformation("Pagamento {PaymentId} confirmado via webhook Asaas (api/PaymentWebhook/asaas)", payment.Id);
                    }
                }
                else if (eventType == AsaasWebhookEvents.PaymentDeleted || eventType == AsaasWebhookEvents.PaymentOverdue)
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId);
                    if (payment != null && payment.Status == PaymentStatus.Pending)
                    {
                        await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Cancelled, null);
                        _logger.LogInformation("Pagamento {PaymentId} cancelado/vencido via webhook Asaas (api/PaymentWebhook/asaas)", payment.Id);
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
                _logger.LogError(ex, "Erro ao processar webhook do Asaas");
                return StatusCode(500, new { error = ErrorJson.ErrorProcessingWebhook });
            }
            return Ok();
        }
    }
}