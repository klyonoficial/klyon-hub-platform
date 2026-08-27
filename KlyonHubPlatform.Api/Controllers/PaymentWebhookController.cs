using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using System.Text.Json;
namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRealtimeNotifier _paymentNotifier;
        private readonly IAsaasWebhookHandler _asaasWebhookHandler;
        private readonly ILogger<PaymentWebhookController> _logger;

        public PaymentWebhookController(
            IPaymentService paymentService,
            IAsaasService asaasService,
            IPaymentRealtimeNotifier paymentNotifier,
            IAsaasWebhookHandler asaasWebhookHandler,
            ILogger<PaymentWebhookController> logger)
        {
            _paymentService = paymentService;
            _asaasService = asaasService;
            _paymentNotifier = paymentNotifier;
            _asaasWebhookHandler = asaasWebhookHandler;
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

                var result = await _asaasWebhookHandler.HandleEventAsync(root, eventType, cancellationToken);

                if (result.PaymentIdForCancelledNotification.HasValue)
                    await _paymentNotifier.NotifyPaymentCancelledAsync(result.PaymentIdForCancelledNotification.Value);

                if (result.PaymentIdForPixRegeneratedNotification.HasValue)
                {
                    var paymentId = result.PaymentIdForPixRegeneratedNotification.Value;
                    try
                    {
                        await _paymentService.HandlePixPaymentOverdueAsync(paymentId);
                        _logger.LogInformation(
                            "Pagamento PIX {PaymentId} vencido: data de vencimento atualizada e novo código gerado via Asaas (api/PaymentWebhook/asaas)",
                            paymentId);
                        await _paymentNotifier.NotifyPaymentPixCodeRegeneratedAsync(paymentId);
                    }
                    catch (Exception exPix)
                    {
                        _logger.LogError(exPix, "Erro ao tratar vencimento PIX do pagamento {PaymentId}; cancelando.", paymentId);
                        await _paymentService.UpdatePaymentStatusAsync(paymentId, Domain.Core.Enums.PaymentStatus.Cancelled, null);
                        await _paymentNotifier.NotifyPaymentCancelledAsync(paymentId);
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
