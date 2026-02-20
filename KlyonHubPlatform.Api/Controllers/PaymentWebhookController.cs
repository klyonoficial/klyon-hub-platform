using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Constants;
namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentWebhookController> _logger;
        public PaymentWebhookController(
            IPaymentService paymentService,
            ILogger<PaymentWebhookController> logger)
        {
            _paymentService = paymentService;
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
        public async Task<IActionResult> AsaasWebhook([FromBody] dynamic webhookData, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Webhook do Asaas recebido");
                return Ok(OkMessage.WebhookReceivedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do Asaas");
                return StatusCode(500, new { error = ErrorJson.ErrorProcessingWebhook });
            }
        }
    }
}