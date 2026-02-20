using System.Text.Json;
using KlyonHubPlatform.Application.DTOs.FocusNFe;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Api.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FocusNFeWebhookController : ControllerBase
    {
        private readonly IFocusNFeService _focusNFeService;
        private readonly ILogger<FocusNFeWebhookController> _logger;

        public FocusNFeWebhookController(
            IFocusNFeService focusNFeService,
            ILogger<FocusNFeWebhookController> logger)
        {
            _focusNFeService = focusNFeService;
            _logger = logger;
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ReceberWebhook(CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                _logger.LogInformation("Webhook Focus NFe recebido. Body: {Body}", body);
                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("Webhook Focus NFe recebido com body vazio");
                    return BadRequest(new { error = ErrorJson.EmptyBody });
                }
                var payload = JsonSerializer.Deserialize<FocusWebhookPayload>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                if (payload == null)
                {
                    _logger.LogWarning("Webhook Focus NFe recebido com payload inválido");
                    return BadRequest(new { error = ErrorJson.InvalidPayload });
                }
                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                var webhookLog = await _focusNFeService.ProcessarWebhookAsync(payload, sourceIp);
                _logger.LogInformation("Webhook Focus NFe processado. Ref: {Ref}, Status: {Status}, ProcessingStatus: {ProcessingStatus}", 
                    webhookLog.Ref, webhookLog.Status, webhookLog.ProcessingStatus);
                return Ok(new 
                { 
                    success = true, 
                    message = OkMessage.WebhookProcessedSuccess,
                    ref_id = webhookLog.Ref,
                    processing_status = webhookLog.ProcessingStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook Focus NFe");
                return Ok(new { success = false, error = ex.Message });
            }
        }
        [HttpGet("logs")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> ListarWebhooks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? referencia = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _focusNFeService.ListarWebhooksAsync(page, pageSize, status, referencia, cancellationToken);
                return Ok(new
                {
                    total = result.Total,
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                    logs = result.Logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar webhooks Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("consultar/{referencia}")]
        [Authorize(Roles = Administrator + "," + "Partner")]
        public async Task<IActionResult> ConsultarNFSe(string referencia, CancellationToken cancellationToken = default)
        {
            try
            {
                var configResult = await _focusNFeService.ObterConfiguracaoParaConsultaAsync(referencia, cancellationToken);
                if (configResult == null)
                    return NotFound(new { error = ErrorJson.FiscalNoteNotFound });
                if (string.IsNullOrEmpty(configResult.ApiToken))
                    return BadRequest(new { error = ErrorJson.FocusNFeTokenNotConfigured });
                var response = await _focusNFeService.ConsultarNFSeAsync(referencia, configResult.ApiToken, configResult.IsProduction);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar NFSe no Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("simulate")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> SimularWebhook([FromBody] FocusWebhookPayload payload, CancellationToken cancellationToken = default)
        {
            try
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (environment == "Production")
                {
                    return BadRequest(new { error = ErrorJson.SimulationNotAllowedProduction });
                }
                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                var webhookLog = await _focusNFeService.ProcessarWebhookAsync(payload, sourceIp);
                return Ok(new
                {
                    success = true,
                    message = OkMessage.WebhookSimulatedProcessed,
                    webhookLog = new
                    {
                        webhookLog.Id,
                        webhookLog.Ref,
                        webhookLog.Status,
                        ProcessingStatus = webhookLog.ProcessingStatus.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao simular webhook Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("configurar")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> ConfigurarWebhook([FromBody] ConfigurarWebhookRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.WebhookUrl))
                    return BadRequest(new { error = ErrorJson.WebhookUrlRequired });
                var config = await _focusNFeService.ObterSefazConfigAtivaAsync(cancellationToken);
                if (config == null || string.IsNullOrEmpty(config.ApiToken))
                    return BadRequest(new { error = ErrorJson.SefazConfigNotFoundOrToken });
                var webhookConfig = new FocusWebhookConfig { Nfse = request.WebhookUrl };
                var isProduction = config.Environment == "Producao";
                var success = await _focusNFeService.ConfigurarWebhookAsync(webhookConfig, config.ApiToken, isProduction);
                if (success)
                    return Ok(new { success = true, message = OkMessage.WebhookConfiguredSuccess });
                return BadRequest(new { error = ErrorJson.ErrorConfiguringWebhookFocusNFe });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao configurar webhook Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("status")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> VerificarStatus(CancellationToken cancellationToken = default)
        {
            try
            {
                var config = await _focusNFeService.ObterSefazConfigAtivaAsync(cancellationToken);
                if (config == null || string.IsNullOrEmpty(config.ApiToken))
                {
                    return Ok(new
                    {
                        configured = false,
                        message = OkMessage.FocusNFeTokenNotConfigured
                    });
                }
                var isProduction = config.Environment == "Producao";
                var isAvailable = await _focusNFeService.VerificarDisponibilidadeAsync(config.ApiToken, isProduction);
                return Ok(new
                {
                    configured = true,
                    environment = config.Environment,
                    available = isAvailable,
                    message = isAvailable ? OkMessage.FocusNFeApiAvailable : OkMessage.FocusNFeApiUnavailable
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar status Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}