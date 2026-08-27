using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using System.Text.Json;

namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// Endpoint legado/alternativo de webhook Asaas. Delega à mesma lógica de PaymentWebhook/asaas.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AsaasWebhookController : ControllerBase
    {
        private readonly IAsaasService _asaasService;
        private readonly IPaymentService _paymentService;
        private readonly IAsaasWebhookHandler _asaasWebhookHandler;
        private readonly ILogger<AsaasWebhookController> _logger;

        public AsaasWebhookController(
            IAsaasService asaasService,
            IPaymentService paymentService,
            IAsaasWebhookHandler asaasWebhookHandler,
            ILogger<AsaasWebhookController> logger)
        {
            _asaasService = asaasService;
            _paymentService = paymentService;
            _asaasWebhookHandler = asaasWebhookHandler;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
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

                if (result.PaymentIdForPixRegeneratedNotification.HasValue)
                {
                    var paymentId = result.PaymentIdForPixRegeneratedNotification.Value;
                    try
                    {
                        await _paymentService.HandlePixPaymentOverdueAsync(paymentId);
                        _logger.LogInformation("Pagamento PIX {PaymentId} vencido: novo código gerado via Asaas", paymentId);
                    }
                    catch (Exception exPix)
                    {
                        _logger.LogError(exPix, "Erro ao tratar vencimento PIX do pagamento {PaymentId}", paymentId);
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
    }
}
