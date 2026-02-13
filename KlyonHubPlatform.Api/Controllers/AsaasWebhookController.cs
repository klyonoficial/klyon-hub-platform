using System.Threading;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using System.Text.Json;

namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// Recebe webhooks do Asaas (cobrança paga, confirmada, etc.) e atualiza o status do pagamento no sistema.
    /// Ref: https://docs.asaas.com/docs/guia-de-webhooks
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AsaasWebhookController : ControllerBase
    {
        private readonly IAsaasService _asaasService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<AsaasWebhookController> _logger;

        public AsaasWebhookController(
            IAsaasService asaasService,
            IPaymentRepository paymentRepository,
            IPaymentService paymentService,
            ILogger<AsaasWebhookController> logger)
        {
            _asaasService = asaasService;
            _paymentRepository = paymentRepository;
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

            var signature = Request.Headers["asaas-access-token"].FirstOrDefault();

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;
                if (!root.TryGetProperty("payment", out var paymentEl))
                {
                    _logger.LogWarning("Webhook Asaas sem objeto payment");
                    return Ok();
                }

                var paymentId = paymentEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var status = paymentEl.TryGetProperty("status", out var st) ? st.GetString() : null;

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

                // Eventos que indicam pagamento recebido/confirmado
                var paidEvents = new[] { "PAYMENT_RECEIVED", "PAYMENT_CONFIRMED", "PAYMENT_RECEIVED_IN_CASH" };
                if (paidEvents.Contains(eventType ?? "") && (status == "RECEIVED" || status == "CONFIRMED"))
                {
                    var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(paymentId);
                    if (payment != null && payment.Status != PaymentStatus.Paid)
                    {
                        await _paymentService.ConfirmPaymentAsync(payment.Id, paymentId);
                        _logger.LogInformation("Pagamento {PaymentId} confirmado via webhook Asaas", payment.Id);
                    }
                }
                else if (eventType == "PAYMENT_DELETED" || eventType == "PAYMENT_OVERDUE")
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
    }
}
