using System.Text.Json;
using System.Threading;
using KlyonHubPlatform.Application.DTOs.FocusNFe;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Api.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;

namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// Controller para receber webhooks do Focus NFe
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FocusNFeWebhookController : ControllerBase
    {
        private readonly IFocusNFeService _focusNFeService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FocusNFeWebhookController> _logger;

        public FocusNFeWebhookController(
            IFocusNFeService focusNFeService,
            ApplicationDbContext context,
            ILogger<FocusNFeWebhookController> logger)
        {
            _focusNFeService = focusNFeService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint para receber notificações do Focus NFe (webhook)
        /// O Focus NFe envia POST quando há mudança de status na NFSe
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ReceberWebhook(CancellationToken cancellationToken = default)
        {
            try
            {
                // Ler o body da requisição
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation("Webhook Focus NFe recebido. Body: {Body}", body);

                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("Webhook Focus NFe recebido com body vazio");
                    return BadRequest(new { error = ErrorJson.EmptyBody });
                }

                // Deserializar o payload
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

                // Obter IP de origem
                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Processar o webhook
                var webhookLog = await _focusNFeService.ProcessarWebhookAsync(payload, sourceIp);

                _logger.LogInformation("Webhook Focus NFe processado. Ref: {Ref}, Status: {Status}, ProcessingStatus: {ProcessingStatus}", 
                    webhookLog.Ref, webhookLog.Status, webhookLog.ProcessingStatus);

                // Retornar 200 OK para o Focus NFe saber que recebemos
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
                
                // Mesmo com erro, retornamos 200 para evitar reenvios desnecessários
                // O erro foi logado para análise posterior
                return Ok(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Lista os webhooks recebidos do Focus NFe (para debug/auditoria)
        /// </summary>
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
                var query = _context.Set<FocusNFeWebhookLog>().AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<FocusWebhookProcessingStatus>(status, true, out var processingStatus))
                    {
                        query = query.Where(w => w.ProcessingStatus == processingStatus);
                    }
                }

                if (!string.IsNullOrEmpty(referencia))
                {
                    query = query.Where(w => w.Ref.Contains(referencia));
                }

                var total = await query.CountAsync();
                var logs = await query
                    .OrderByDescending(w => w.ReceivedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => new
                    {
                        w.Id,
                        w.Ref,
                        w.CnpjEmitente,
                        w.DocumentType,
                        w.Status,
                        w.Evento,
                        w.Numero,
                        w.CodigoVerificacao,
                        w.Url,
                        w.FiscalNoteId,
                        ProcessingStatus = w.ProcessingStatus.ToString(),
                        w.ErrorMessage,
                        w.SourceIp,
                        w.ReceivedAt,
                        w.ProcessedAt,
                        w.ProcessingAttempts
                    })
                    .ToListAsync();

                return Ok(new
                {
                    total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize),
                    logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar webhooks Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Consulta o status de uma NFSe no Focus NFe
        /// </summary>
        [HttpGet("consultar/{referencia}")]
        [Authorize(Roles = Administrator + "," + "Partner")]
        public async Task<IActionResult> ConsultarNFSe(string referencia, CancellationToken cancellationToken = default)
        {
            try
            {
                // Buscar a nota fiscal para obter a configuração
                var fiscalNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.IdempotencyKey == referencia || fn.AccessKey == referencia);

                if (fiscalNote == null)
                {
                    return NotFound(new { error = ErrorJson.FiscalNoteNotFound });
                }

                // Determinar se é nota de intermediação ou serviço
                string? apiToken = null;
                bool isProduction = false;

                if (fiscalNote.Type == "Intermediacao")
                {
                    var config = await _context.SefazConfigurations
                        .Where(c => c.IsActive)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (config != null)
                    {
                        apiToken = config.ApiToken;
                        isProduction = config.Environment == "Producao";
                    }
                }
                else
                {
                    var config = await _context.CoworkingSefazConfigurations
                        .Where(c => c.CoworkingId == fiscalNote.CoworkingId && c.IsActive)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (config != null)
                    {
                        apiToken = config.ApiToken;
                        isProduction = config.Environment == "Producao";
                    }
                }

                if (string.IsNullOrEmpty(apiToken))
                {
                    return BadRequest(new { error = ErrorJson.FocusNFeTokenNotConfigured });
                }

                var response = await _focusNFeService.ConsultarNFSeAsync(referencia, apiToken, isProduction);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar NFSe no Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint para simular um webhook (apenas em desenvolvimento)
        /// </summary>
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

        /// <summary>
        /// Configura a URL de webhook no Focus NFe
        /// </summary>
        [HttpPost("configurar")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> ConfigurarWebhook([FromBody] ConfigurarWebhookRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.WebhookUrl))
                {
                    return BadRequest(new { error = ErrorJson.WebhookUrlRequired });
                }

                // Buscar configuração da plataforma
                var config = await _context.SefazConfigurations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (config == null || string.IsNullOrEmpty(config.ApiToken))
                {
                    return BadRequest(new { error = ErrorJson.SefazConfigNotFoundOrToken });
                }

                var webhookConfig = new FocusWebhookConfig
                {
                    Nfse = request.WebhookUrl
                };

                var isProduction = config.Environment == "Producao";
                var success = await _focusNFeService.ConfigurarWebhookAsync(webhookConfig, config.ApiToken, isProduction);

                if (success)
                {
                    return Ok(new { success = true, message = OkMessage.WebhookConfiguredSuccess });
                }
                else
                {
                    return BadRequest(new { error = ErrorJson.ErrorConfiguringWebhookFocusNFe });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao configurar webhook Focus NFe");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Verifica a disponibilidade da API Focus NFe
        /// </summary>
        [HttpGet("status")]
        [Authorize(Roles = Administrator)]
        public async Task<IActionResult> VerificarStatus(CancellationToken cancellationToken = default)
        {
            try
            {
                var config = await _context.SefazConfigurations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

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
