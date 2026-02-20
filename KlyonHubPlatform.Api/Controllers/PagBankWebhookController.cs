using KlyonHubPlatform.Api.Constants;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using System.Text.Json;
namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagBankWebhookController : ControllerBase
    {
        private readonly IPagBankService _pagBankService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPagBankWebhookLogRepository _webhookLogRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PagBankWebhookController> _logger;
        private readonly IAuditService? _auditService;
        public PagBankWebhookController(
            IPagBankService pagBankService,
            IPaymentRepository paymentRepository,
            IPagBankWebhookLogRepository webhookLogRepository,
            IPaymentService paymentService,
            ILogger<PagBankWebhookController> logger,
            IAuditService? auditService = null)
        {
            _pagBankService = pagBankService;
            _paymentRepository = paymentRepository;
            _webhookLogRepository = webhookLogRepository;
            _paymentService = paymentService;
            _logger = logger;
            _auditService = auditService;
        }
        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
        {
            string payload = string.Empty;
            PagBankWebhookLog? webhookLog = null;
            try
            {
                using var reader = new StreamReader(Request.Body);
                payload = await reader.ReadToEndAsync();
                var sourceIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                var signature = Request.Headers[HttpHeaderNames.PagBankAuthenticityToken].FirstOrDefault() 
                             ?? Request.Headers[HttpHeaderNames.PagBankSignature].FirstOrDefault();
                var notification = JsonSerializer.Deserialize<PagBankWebhookNotification>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (notification == null)
                {
                    _logger.LogWarning("Webhook recebido com formato inválido");
                    return BadRequest(ErrorJson.InvalidNotificationFormat);
                }
                var resourceId = notification.Resource?.Id ?? notification.Charges?.FirstOrDefault()?.Id ?? "";
                var referenceId = notification.Resource?.ReferenceId ?? notification.Charges?.FirstOrDefault()?.ReferenceId;
                var resourceStatus = notification.Resource?.Status ?? notification.Charges?.FirstOrDefault()?.Status ?? "";
                var eventId = notification.NotificationId ?? $"{notification.Event}_{resourceId}_{notification.CreatedAt:yyyyMMddHHmmss}";
                if (await _webhookLogRepository.ExistsEventIdAsync(eventId))
                {
                    _logger.LogInformation($"Webhook já processado anteriormente: EventId={eventId}");
                    return Ok(new { message = OkMessage.WebhookAlreadyProcessed });
                }
                webhookLog = new PagBankWebhookLog
                {
                    EventId = eventId,
                    EventType = notification.Event,
                    ResourceId = resourceId,
                    ReferenceId = referenceId,
                    ResourceStatus = resourceStatus,
                    RawPayload = payload,
                    ReceivedSignature = signature,
                    SourceIp = sourceIp,
                    ReceivedAt = DateTime.UtcNow,
                    ProcessingStatus = WebhookProcessingStatus.Received
                };
                var signatureValid = await _pagBankService.ValidateWebhookSignatureAsync(payload, signature ?? "");
                webhookLog.SignatureValid = signatureValid;
                if (!signatureValid)
                {
                    _logger.LogWarning($"Webhook recebido com assinatura inválida: EventId={eventId}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.InvalidSignature;
                    webhookLog.ErrorMessage = UnauthorizedMessage.InvalidSignature;
                    await _webhookLogRepository.CreateAsync(webhookLog);
                    return Unauthorized(UnauthorizedMessage.InvalidSignature);
                }
                if (string.IsNullOrEmpty(resourceId))
                {
                    _logger.LogWarning("Webhook recebido sem ResourceId");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                    webhookLog.ErrorMessage = "ResourceId não encontrado no payload";
                    await _webhookLogRepository.CreateAsync(webhookLog);
                    return BadRequest(ErrorJson.ResourceIdNotFound);
                }
                _logger.LogInformation($"Webhook recebido: Evento={notification.Event}, ResourceId={resourceId}, ReferenceId={referenceId}, Status={resourceStatus}");
                var webhookResource = new PagBankWebhookResource
                {
                    Id = resourceId,
                    ReferenceId = referenceId,
                    Status = resourceStatus,
                    Amount = notification.Resource?.Amount ?? notification.Charges?.FirstOrDefault()?.Amount,
                    PaidAt = notification.Resource?.PaidAt ?? notification.Charges?.FirstOrDefault()?.PaidAt
                };
                var webhookEvent = notification.Event.ToWebhookEvent();
                var processed = false;
                if (webhookEvent.IsPaidEvent())
                {
                    processed = await ProcessPaymentPaidAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsCanceledEvent())
                {
                    processed = await ProcessPaymentCanceledAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsRefundedEvent())
                {
                    processed = await ProcessPaymentRefundedAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsInAnalysisEvent())
                {
                    processed = await ProcessPaymentInAnalysisAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsDeclinedEvent())
                {
                    processed = await ProcessPaymentDeclinedAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsWaitingEvent())
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                    processed = true;
                    _logger.LogInformation($"Pagamento aguardando: ResourceId={resourceId}");
                }
                else
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = $"Evento não processado: {notification.Event}";
                    _logger.LogInformation($"Evento não processado: {notification.Event}");
                }
                webhookLog.ProcessedAt = DateTime.UtcNow;
                webhookLog.ProcessingAttempts++;
                await _webhookLogRepository.CreateAsync(webhookLog);
                return Ok(new { message = OkMessage.WebhookProcessed, eventId = eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do PagBank");
                if (webhookLog != null)
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                    webhookLog.ErrorMessage = ex.Message;
                    webhookLog.ProcessingAttempts++;
                    try
                    {
                        await _webhookLogRepository.CreateAsync(webhookLog);
                    }
                    catch { /* Ignorar erro ao salvar log */ }
                }
                return Ok(new { message = OkMessage.ErrorProcessed, error = ex.Message });
            }
        }
        private async Task<bool> ProcessPaymentPaidAsync(PagBankWebhookResource resource, PagBankWebhookLog webhookLog)
        {
            try
            {
                var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(resource.Id, resource.ReferenceId);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}, ReferenceId: {resource.ReferenceId}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.PaymentNotFound;
                    webhookLog.ErrorMessage = $"Pagamento não encontrado: ID={resource.Id}, RefId={resource.ReferenceId}";
                    return false;
                }
                webhookLog.PaymentId = payment.Id;
                if (payment.Status == PaymentStatus.Paid)
                {
                    _logger.LogInformation($"Pagamento {payment.Id} já está pago. Ignorando webhook.");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = "Pagamento já estava com status Paid";
                    return true;
                }
                await _paymentService.ConfirmPaymentAsync(payment.Id, resource.Id);
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                _logger.LogInformation($"Pagamento {payment.Id} confirmado via webhook PagBank");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento pago: {resource.Id}");
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                webhookLog.ErrorMessage = ex.Message;
                return false;
            }
        }
        private async Task<bool> ProcessPaymentCanceledAsync(PagBankWebhookResource resource, PagBankWebhookLog webhookLog)
        {
            try
            {
                var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(resource.Id, resource.ReferenceId);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.PaymentNotFound;
                    webhookLog.ErrorMessage = $"Pagamento não encontrado: ID={resource.Id}";
                    return false;
                }
                webhookLog.PaymentId = payment.Id;
                if (payment.Status == PaymentStatus.Cancelled)
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = "Pagamento já estava cancelado";
                    return true;
                }
                var oldStatus = payment.Status;
                payment.Status = PaymentStatus.Cancelled;
                await _paymentRepository.UpdateAsync(payment);
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                _logger.LogInformation($"Pagamento {payment.Id} cancelado via webhook PagBank");
                await LogAuditAsync("Pagamento cancelado via webhook", payment.Id, oldStatus, PaymentStatus.Cancelled);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento cancelado: {resource.Id}");
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                webhookLog.ErrorMessage = ex.Message;
                return false;
            }
        }
        private async Task<bool> ProcessPaymentRefundedAsync(PagBankWebhookResource resource, PagBankWebhookLog webhookLog)
        {
            try
            {
                var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(resource.Id, resource.ReferenceId);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.PaymentNotFound;
                    webhookLog.ErrorMessage = $"Pagamento não encontrado: ID={resource.Id}";
                    return false;
                }
                webhookLog.PaymentId = payment.Id;
                if (payment.Status == PaymentStatus.Refunded)
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = "Pagamento já estava estornado";
                    return true;
                }
                var oldStatus = payment.Status;
                payment.Status = PaymentStatus.Refunded;
                await _paymentRepository.UpdateAsync(payment);
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                _logger.LogInformation($"Pagamento {payment.Id} estornado via webhook PagBank");
                await LogAuditAsync("Pagamento estornado via webhook", payment.Id, oldStatus, PaymentStatus.Refunded);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento estornado: {resource.Id}");
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                webhookLog.ErrorMessage = ex.Message;
                return false;
            }
        }
        private async Task<bool> ProcessPaymentInAnalysisAsync(PagBankWebhookResource resource, PagBankWebhookLog webhookLog)
        {
            try
            {
                var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(resource.Id, resource.ReferenceId);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.PaymentNotFound;
                    webhookLog.ErrorMessage = $"Pagamento não encontrado: ID={resource.Id}";
                    return false;
                }
                webhookLog.PaymentId = payment.Id;
                if (payment.Status != PaymentStatus.Processing)
                {
                    payment.Status = PaymentStatus.Processing;
                    await _paymentRepository.UpdateAsync(payment);
                }
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                _logger.LogInformation($"Pagamento {payment.Id} em análise via webhook PagBank");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento em análise: {resource.Id}");
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                webhookLog.ErrorMessage = ex.Message;
                return false;
            }
        }
        private async Task<bool> ProcessPaymentDeclinedAsync(PagBankWebhookResource resource, PagBankWebhookLog webhookLog)
        {
            try
            {
                var payment = await _paymentRepository.FindPaymentByExternalIdOrReferenceAsync(resource.Id, resource.ReferenceId);
                if (payment == null)
                {
                    _logger.LogWarning($"Pagamento não encontrado para ExternalPaymentId: {resource.Id}");
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.PaymentNotFound;
                    webhookLog.ErrorMessage = $"Pagamento não encontrado: ID={resource.Id}";
                    return false;
                }
                webhookLog.PaymentId = payment.Id;
                if (payment.Status == PaymentStatus.Failed)
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = "Pagamento já estava com status Failed";
                    return true;
                }
                var oldStatus = payment.Status;
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = resource.PaymentResponse?.Message ?? "Pagamento recusado pelo PagBank";
                await _paymentRepository.UpdateAsync(payment);
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                _logger.LogInformation($"Pagamento {payment.Id} recusado via webhook PagBank");
                await LogAuditAsync("Pagamento recusado via webhook", payment.Id, oldStatus, PaymentStatus.Failed);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar pagamento recusado: {resource.Id}");
                webhookLog.ProcessingStatus = WebhookProcessingStatus.Failed;
                webhookLog.ErrorMessage = ex.Message;
                return false;
            }
        }
        private async Task LogAuditAsync(string action, int paymentId, PaymentStatus oldStatus, PaymentStatus newStatus)
        {
            if (_auditService == null) return;
            try
            {
                var ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers[HttpHeaderNames.UserAgent].ToString();
                await _auditService.LogActionAsync(
                    null,
                    action,
                    "Payment",
                    paymentId,
                    JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
                    JsonSerializer.Serialize(new { Status = newStatus.ToString() }),
                    ipAddress,
                    userAgent
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao registrar log de auditoria");
            }
        }
        [HttpGet("logs")]
        public async Task<IActionResult> GetWebhookLogs([FromQuery] int count = 50, CancellationToken cancellationToken = default)
        {
            try
            {
                var logs = await _webhookLogRepository.GetRecentLogsAsync(count);
                return Ok(logs.Select(l => new
                {
                    l.Id,
                    l.EventId,
                    l.EventType,
                    l.ResourceId,
                    l.ReferenceId,
                    l.ResourceStatus,
                    l.PaymentId,
                    ProcessingStatus = l.ProcessingStatus.ToString(),
                    l.ErrorMessage,
                    l.SignatureValid,
                    l.SourceIp,
                    l.ReceivedAt,
                    l.ProcessedAt,
                    l.ProcessingAttempts
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar logs de webhook");
                return StatusCode(500, "Erro ao consultar logs");
            }
        }
        #region Endpoints de Simulação (apenas para ambiente de desenvolvimento/sandbox)
        [HttpPost("simulate/paid")]
        public async Task<IActionResult> SimulatePaidWebhook([FromQuery] int paymentId, CancellationToken cancellationToken = default)
        {
            return await SimulateWebhookAsync(paymentId, PagBankWebhookEvent.ChargePaid, "PAID");
        }
        [HttpPost("simulate/canceled")]
        public async Task<IActionResult> SimulateCanceledWebhook([FromQuery] int paymentId, CancellationToken cancellationToken = default)
        {
            return await SimulateWebhookAsync(paymentId, PagBankWebhookEvent.ChargeCanceled, "CANCELED");
        }
        [HttpPost("simulate/refunded")]
        public async Task<IActionResult> SimulateRefundedWebhook([FromQuery] int paymentId, CancellationToken cancellationToken = default)
        {
            return await SimulateWebhookAsync(paymentId, PagBankWebhookEvent.ChargeRefunded, "REFUNDED");
        }
        [HttpPost("simulate/analysis")]
        public async Task<IActionResult> SimulateInAnalysisWebhook([FromQuery] int paymentId, CancellationToken cancellationToken = default)
        {
            return await SimulateWebhookAsync(paymentId, PagBankWebhookEvent.ChargeInAnalysis, "IN_ANALYSIS");
        }
        [HttpPost("simulate/declined")]
        public async Task<IActionResult> SimulateDeclinedWebhook([FromQuery] int paymentId, CancellationToken cancellationToken = default)
        {
            return await SimulateWebhookAsync(paymentId, PagBankWebhookEvent.ChargeDeclined, "DECLINED");
        }
        [HttpPost("simulate")]
        public async Task<IActionResult> SimulateWebhook([FromQuery] int paymentId, [FromQuery] string eventType, CancellationToken cancellationToken = default)
        {
            var webhookEvent = eventType.ToWebhookEvent();
            if (webhookEvent == PagBankWebhookEvent.Unknown)
            {
                return BadRequest(new { 
                    error = ErrorJson.InvalidEvent,
                    validEvents = new[] 
                    { 
                        "CHARGE_PAID", "CHARGE_CANCELED", "CHARGE_REFUNDED", 
                        "CHARGE_IN_ANALYSIS", "CHARGE_DECLINED", "CHARGE_WAITING",
                        "PAYMENT_PAID", "PAYMENT_CANCELED", "PAYMENT_REFUNDED",
                        "ORDER_PAID", "ORDER_CANCELED", "ORDER_REFUNDED"
                    }
                });
            }
            return await SimulateWebhookAsync(paymentId, webhookEvent, eventType.ToUpper().Replace("CHARGE_", "").Replace("PAYMENT_", "").Replace("ORDER_", ""));
        }
        private async Task<IActionResult> SimulateWebhookAsync(int paymentId, PagBankWebhookEvent webhookEvent, string status)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return NotFound(new { error = $"Pagamento {paymentId} não encontrado" });
                }
                var eventName = webhookEvent switch
                {
                    PagBankWebhookEvent.ChargePaid => "CHARGE_PAID",
                    PagBankWebhookEvent.ChargeCanceled => "CHARGE_CANCELED",
                    PagBankWebhookEvent.ChargeRefunded => "CHARGE_REFUNDED",
                    PagBankWebhookEvent.ChargeInAnalysis => "CHARGE_IN_ANALYSIS",
                    PagBankWebhookEvent.ChargeDeclined => "CHARGE_DECLINED",
                    PagBankWebhookEvent.ChargeWaiting => "CHARGE_WAITING",
                    _ => webhookEvent.ToString().ToUpper()
                };
                var simulatedPayload = new
                {
                    id = $"SIM_{Guid.NewGuid():N}",
                    @event = eventName,
                    created_at = DateTime.UtcNow,
                    resource = new
                    {
                        id = payment.ExternalPaymentId,
                        reference_id = payment.ExternalPaymentId,
                        status = status,
                        amount = new
                        {
                            value = (int)(payment.Amount * 100),
                            currency = "BRL"
                        },
                        paid_at = webhookEvent.IsPaidEvent() ? DateTime.UtcNow : (DateTime?)null
                    }
                };
                var payload = JsonSerializer.Serialize(simulatedPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation($"[SIMULAÇÃO] Webhook simulado: Event={eventName}, PaymentId={paymentId}, ExternalId={payment.ExternalPaymentId}");
                var webhookLog = new PagBankWebhookLog
                {
                    EventId = $"SIM_{Guid.NewGuid():N}",
                    EventType = eventName,
                    ResourceId = payment.ExternalPaymentId,
                    ReferenceId = payment.ExternalPaymentId,
                    ResourceStatus = status,
                    RawPayload = payload,
                    ReceivedSignature = "SIMULATED",
                    SignatureValid = true,
                    SourceIp = "127.0.0.1",
                    ReceivedAt = DateTime.UtcNow,
                    ProcessingStatus = WebhookProcessingStatus.Received,
                    PaymentId = payment.Id
                };
                var webhookResource = new PagBankWebhookResource
                {
                    Id = payment.ExternalPaymentId,
                    ReferenceId = payment.ExternalPaymentId,
                    Status = status
                };
                bool processed = false;
                if (webhookEvent.IsPaidEvent())
                {
                    processed = await ProcessPaymentPaidAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsCanceledEvent())
                {
                    processed = await ProcessPaymentCanceledAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsRefundedEvent())
                {
                    processed = await ProcessPaymentRefundedAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsInAnalysisEvent())
                {
                    processed = await ProcessPaymentInAnalysisAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsDeclinedEvent())
                {
                    processed = await ProcessPaymentDeclinedAsync(webhookResource, webhookLog);
                }
                else if (webhookEvent.IsWaitingEvent())
                {
                    webhookLog.ProcessingStatus = WebhookProcessingStatus.Processed;
                    processed = true;
                }
                webhookLog.ProcessedAt = DateTime.UtcNow;
                webhookLog.ProcessingAttempts++;
                await _webhookLogRepository.CreateAsync(webhookLog);
                var updatedPayment = await _paymentRepository.GetByIdAsync(paymentId);
                return Ok(new
                {
                    message = OkMessage.WebhookSimulatedSuccess,
                    simulated = true,
                    eventType = eventName,
                    paymentId = paymentId,
                    externalPaymentId = payment.ExternalPaymentId,
                    previousStatus = payment.Status.ToString(),
                    newStatus = updatedPayment?.Status.ToString(),
                    processed = processed,
                    webhookLogId = webhookLog.Id,
                    processingStatus = webhookLog.ProcessingStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao simular webhook para pagamento {paymentId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        #endregion
    }
}