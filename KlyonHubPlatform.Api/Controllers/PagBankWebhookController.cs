using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagBankWebhookController : ControllerBase
    {
        private readonly IPagBankService _pagBankService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PagBankWebhookController> _logger;
        private readonly IAuditService? _auditService;

        public PagBankWebhookController(
            IPagBankService pagBankService,
            IPaymentRepository paymentRepository,
            IPaymentService paymentService,
            ILogger<PagBankWebhookController> logger,
            IAuditService? auditService = null)
        {
            _pagBankService = pagBankService;
            _paymentRepository = paymentRepository;
            _paymentService = paymentService;
            _logger = logger;
            _auditService = auditService;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook()
        {
            try
            {
                // Ler o corpo da requisição
                using var reader = new StreamReader(Request.Body);
                var payload = await reader.ReadToEndAsync();

                // Validar assinatura do webhook (se configurado)
                var signature = Request.Headers["X-PagSeguro-Signature"].FirstOrDefault();
                if (!string.IsNullOrEmpty(signature))
                {
                    var isValid = await _pagBankService.ValidateWebhookSignatureAsync(payload, signature);
                    if (!isValid)
                    {
                        _logger.LogWarning("Webhook recebido com assinatura inválida");
                        return Unauthorized();
                    }
                }

                // Deserializar notificação
                var notification = JsonSerializer.Deserialize<PagBankWebhookNotification>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (notification == null || notification.Resource == null)
                {
                    _logger.LogWarning("Webhook recebido com formato inválido");
                    return BadRequest();
                }

                _logger.LogInformation($"Webhook recebido: Evento={notification.Event}, ResourceId={notification.Resource.Id}, Status={notification.Resource.Status}");

                // Processar diferentes tipos de eventos
                switch (notification.Event.ToUpper())
                {
                    case "PAYMENT_PAID":
                    case "CHARGE_PAID":
                        await ProcessPaymentPaidAsync(notification.Resource);
                        break;

                    case "PAYMENT_CANCELED":
                    case "CHARGE_CANCELED":
                        await ProcessPaymentCanceledAsync(notification.Resource);
                        break;

                    case "PAYMENT_REFUNDED":
                    case "CHARGE_REFUNDED":
                        await ProcessPaymentRefundedAsync(notification.Resource);
                        break;

                    default:
                        _logger.LogInformation($"Evento não processado: {notification.Event}");
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do PagBank");
                return StatusCode(500, "Erro ao processar webhook");
            }
        }

        private async Task ProcessPaymentPaidAsync(PagBankWebhookResource resource)
        {
            try
            {
                // Buscar pagamento pelo ExternalPaymentId (ID do pedido PagBank)
                var payment = await _paymentRepository.GetByExternalPaymentIdAsync(resource.Id);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    return;
                }

                // Confirmar pagamento
                await _paymentService.ConfirmPaymentAsync(payment.Id, resource.Id);
                _logger.LogInformation($"Pagamento {payment.Id} confirmado via webhook PagBank");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento pago: {resource.Id}");
            }
        }

        private async Task ProcessPaymentCanceledAsync(PagBankWebhookResource resource)
        {
            try
            {
                var payment = await _paymentRepository.GetByExternalPaymentIdAsync(resource.Id);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    return;
                }

                payment.Status = PaymentStatus.Cancelled;
                await _paymentRepository.UpdateAsync(payment);
                _logger.LogInformation($"Pagamento {payment.Id} cancelado via webhook PagBank");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento cancelado: {resource.Id}");
            }
        }

        private async Task ProcessPaymentRefundedAsync(PagBankWebhookResource resource)
        {
            try
            {
                var payment = await _paymentRepository.GetByExternalPaymentIdAsync(resource.Id);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    return;
                }

                // Capturar valores antigos para auditoria
                var oldStatus = payment.Status;
                var oldRefundedAt = payment.UpdatedAt;

                payment.Status = PaymentStatus.Refunded;
                await _paymentRepository.UpdateAsync(payment);
                _logger.LogInformation($"Pagamento {payment.Id} estornado via webhook PagBank");

                // Log de auditoria: estorno de pagamento
                if (_auditService != null)
                {
                    var ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = Request.Headers["User-Agent"].ToString();
                    
                    await _auditService.LogActionAsync(
                        null, // Webhook do PagBank (sistema externo)
                        "Pagamento estornado",
                        "Payment",
                        payment.Id,
                        JsonSerializer.Serialize(new { Status = oldStatus.ToString(), RefundedAt = oldRefundedAt }),
                        JsonSerializer.Serialize(new { Status = payment.Status.ToString(), Amount = payment.Amount, ExternalPaymentId = resource.Id }),
                        ipAddress,
                        userAgent
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento estornado: {resource.Id}");
            }
        }
    }
}
