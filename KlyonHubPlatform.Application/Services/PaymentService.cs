using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Security.Claims;
namespace KlyonHubPlatform.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ICommissionService _commissionService;
        private readonly IContractRepository _contractRepository;
        private readonly ICoworkingRepository _coworkingRepository;
        private readonly IAsaasService _asaasService;
        private readonly ApplicationDbContext _context;
        private readonly IAddressDeclarationService? _addressDeclarationService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IFiscalOnboardingService? _fiscalOnboardingService;
        public PaymentService(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            ICommissionService commissionService,
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            IAsaasService asaasService,
            ApplicationDbContext context,
            ISystemConfigurationService systemConfigurationService,
            IAddressDeclarationService? addressDeclarationService = null,
            IAuditService? auditService = null,
            IHttpContextAccessor? httpContextAccessor = null,
            IFiscalOnboardingService? fiscalOnboardingService = null)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _commissionService = commissionService;
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _asaasService = asaasService;
            _context = context;
            _addressDeclarationService = addressDeclarationService;
            _systemConfigurationService = systemConfigurationService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
            _fiscalOnboardingService = fiscalOnboardingService;
        }

        /// <summary>Retorna o horário atual de Brasília (America/Sao_Paulo).</summary>
        private static DateTime GetBrazilianTime()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(-3);
            }
        }

        /// <summary>Converte um DateTime (assumido UTC) para horário de Brasília.</summary>
        private static DateTime ToBrazilianTime(DateTime utcDateTime)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                var utc = utcDateTime.Kind == DateTimeKind.Utc ? utcDateTime : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            }
            catch
            {
                return utcDateTime.AddHours(-3);
            }
        }

        /// <summary>Garante que PaidAt tenha data e hora em horário de Brasília. Se o valor vier só com data (00:00:00), usa a hora atual de Brasília.</summary>
        private static DateTime EnsurePaidAtWithTime(DateTime dateTime)
        {
            if (dateTime.TimeOfDay == TimeSpan.Zero)
                return dateTime.Date.Add(GetBrazilianTime().TimeOfDay);
            return ToBrazilianTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
        }

        private async Task LogAuditAsync(int? userId, string action, string entityType, int? entityId, object? oldValues = null, object? newValues = null)
        {
            if (_auditService == null) return;
            string? ipAddress = null;
            string? userAgent = null;
            if (_httpContextAccessor?.HttpContext != null)
            {
                ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
                userAgent = _httpContextAccessor.HttpContext.Request.Headers[HttpHeaderNames.UserAgent].ToString();
            }
            string? oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            string? newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;
            await _auditService.LogActionAsync(userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        }
        private async Task CancelOtherPendingPaymentContractsForClientAsync(int clientId, int activeContractId)
        {
            var activeContract = await _context.Contracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == activeContractId);
            var activeContractNumber = activeContract?.ContractNumber ?? DefaultDisplayStrings.NotAvailable;
            var cancellationReasonContract = "Cliente já possui um endereço fiscal ativo. Outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";
            var rejectionReasonPayment = "Cancelado pois outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";
            var others = await _context.Contracts
                .Include(c => c.Subscription)
                .Where(c => c.ClientId == clientId && c.Id != activeContractId && c.Status == ContractStatus.PendingPayment)
                .ToListAsync();
            var now = DateTime.UtcNow;
            var subscriptionIds = new List<int>();
            foreach (var c in others)
            {
                c.Status = ContractStatus.Cancelled;
                c.CancelledAt = now;
                c.CancellationReason = cancellationReasonContract;
                c.UpdatedAt = now;
                _context.Contracts.Update(c);
                if (c.Subscription != null)
                    subscriptionIds.Add(c.Subscription.Id);
            }
            if (subscriptionIds.Count > 0)
            {
                var paymentsToCancel = await _context.Payments
                    .Where(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)
                        && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing || p.Status == PaymentStatus.AwaitingValidation))
                    .ToListAsync();
                var actionBy = 0;
                var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var uid))
                    actionBy = uid;
                foreach (var p in paymentsToCancel)
                {
                    p.Status = PaymentStatus.Cancelled;
                    p.UpdatedAt = now;
                    _context.Payments.Update(p);
                    if (actionBy > 0)
                    {
                        _context.PaymentHistories.Add(new PaymentHistory
                        {
                            PaymentId = p.Id,
                            Action = PaymentHistoryActions.Cancelled,
                            RejectionReason = rejectionReasonPayment,
                            ActionBy = actionBy,
                            ActionAt = now
                        });
                    }
                }
            }
        }
        public async Task<Payment> CreatePaymentAsync(int? subscriptionId, decimal amount, PaymentMethod method)
        {
            DateTime dueDate;
            if (subscriptionId.HasValue)
            {
                var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId.Value);
                if (subscription == null)
                    throw new InvalidOperationException("Assinatura não encontrada");
                dueDate = subscription.NextBillingDate;
            }
            else
            {
                // PIX sem recorrência (ou outro fluxo sem assinatura): vencimento padrão
                dueDate = method == PaymentMethod.PIX
                    ? DateTime.UtcNow.AddHours(24)
                    : DateTime.UtcNow.AddDays(1);
            }
            var payment = new Payment
            {
                SubscriptionId = subscriptionId,
                Amount = amount,
                Method = method,
                Status = PaymentStatus.Pending,
                DueDate = dueDate,
                ExternalPaymentId = Guid.NewGuid().ToString()
            };
            if (method == PaymentMethod.PIX)
            {
                payment.PixExpiresAt = DateTime.UtcNow.AddHours(24);
            }
            return await _paymentRepository.CreateAsync(payment);
        }
        public async Task<Payment> ProcessPaymentAsync(int paymentId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var payment = await _paymentRepository.GetByIdAsync(paymentId);
                    if (payment == null)
                        throw new InvalidOperationException("Pagamento não encontrado");
                    payment.Status = PaymentStatus.Processing;
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);
                    await Task.Delay(1000);
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = GetBrazilianTime();
                    payment.TransactionId = Guid.NewGuid().ToString();
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);
                    if (payment.SubscriptionId.HasValue)
                    {
                        var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                        if (subscription != null)
                        {
                            subscription.LastBillingDate = DateTime.UtcNow;
                            var contractForCycle = await _context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
                            subscription.NextBillingDate = contractForCycle?.CardPlanType == ContractCardPlanType.Card1x || contractForCycle?.CardPlanType == ContractCardPlanType.PixAnnual
                                ? subscription.LastBillingDate.Value.AddYears(1)
                                : subscription.LastBillingDate.Value.AddMonths(1);
                            subscription.FailedPaymentAttempts = 0;
                            if (subscription.Status == SubscriptionStatus.PastDue)
                            {
                                subscription.Status = SubscriptionStatus.Active;
                            }
                            subscription.UpdatedAt = DateTime.UtcNow;
                            _context.Subscriptions.Update(subscription);
                            var contract = await _context.Contracts
                                .Include(c => c.CoworkingAddress)
                                .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
                            if (contract != null && (contract.Status == ContractStatus.Suspended || contract.Status == ContractStatus.PendingPayment))
                            {
                                var previousPaidPayments = await _context.Payments
                                    .Where(p => p.SubscriptionId == subscription.Id &&
                                               p.Id != paymentId &&
                                               p.Status == PaymentStatus.Paid)
                                    .CountAsync();
                                if (previousPaidPayments == 0)
                                {
                                    var originalStatus = contract.Status;
                                    contract.Status = ContractStatus.Active;
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                    if (contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                                    {
                                        contract.CoworkingAddress.CurrentContracts++;
                                        if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                                        {
                                            contract.CoworkingAddress.IsAvailable = false;
                                        }
                                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
                                    }
                                    if (_addressDeclarationService != null && contract.ExpiryDate.HasValue)
                                    {
                                        try
                                        {
                                            await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                                        }
                                    }
                                    await CancelOtherPendingPaymentContractsForClientAsync(contract.ClientId, contract.Id);
                                }
                                else
                                {
                                    if (contract.ExpiryDate.HasValue)
                                    {
                                        contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                    }
                                    else
                                    {
                                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    }
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                }
                            }
                            else if (contract != null && contract.Status == ContractStatus.Active)
                            {
                                if (contract.ExpiryDate.HasValue)
                                {
                                    contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                }
                                else
                                {
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                }
                                contract.UpdatedAt = DateTime.UtcNow;
                                _context.Contracts.Update(contract);
                            }
                        }
                    }
                    await _commissionService.CreateCommissionAsync(payment.Id);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        public async Task<Payment> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, string? transactionId = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            payment.Status = status;
            if (transactionId != null)
            {
                payment.TransactionId = transactionId;
            }
            if (status == PaymentStatus.Paid)
            {
                payment.PaidAt = GetBrazilianTime();
            }
            return await _paymentRepository.UpdateAsync(payment);
        }
        public async Task<Payment> UpdatePaymentMethodAsync(int paymentId, PaymentMethod method, ContractCardPlanType? pixPlanType = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ter o método alterado");
            Subscription? subscription = null;
            Contract? contract = null;
            if (payment.SubscriptionId.HasValue)
            {
                subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                if (subscription != null)
                    contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            }
            // Ao sair de PIX: preservar PixCode/PixConciliationIdentifier se for PIX recorrente anual para reaproveitar ao voltar (evita criar várias autorizações no Asaas)
            var preservePixForReuse = false;
            if (method != PaymentMethod.PIX)
            {
                if (contract?.CardPlanType == ContractCardPlanType.PixAnnual)
                    preservePixForReuse = true;
            }
            // Ao manter PIX recorrente anual e payload ainda válido na Subscription, reaproveitar
            if (method == PaymentMethod.PIX && pixPlanType == ContractCardPlanType.PixAnnual && contract?.CardPlanType == ContractCardPlanType.PixAnnual && subscription != null)
            {
                var subExpUtc = subscription.PixAutomaticPayloadExpiresAt.HasValue
                    ? (subscription.PixAutomaticPayloadExpiresAt.Value.Kind == DateTimeKind.Utc ? subscription.PixAutomaticPayloadExpiresAt.Value : DateTime.SpecifyKind(subscription.PixAutomaticPayloadExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                var isAnnualValid = !string.IsNullOrEmpty(subscription.PixAutomaticPayload) &&
                                    (!subExpUtc.HasValue || subExpUtc.Value > DateTime.UtcNow);
                if (isAnnualValid)
                    preservePixForReuse = true;
            }
            // PIX único (None): se já existir código PIX único válido no payment, preservar para reutilizar em GeneratePixCodeAsync (não apagar)
            if (method == PaymentMethod.PIX && (pixPlanType == ContractCardPlanType.None || !pixPlanType.HasValue))
            {
                var expiresAtUtc = payment.PixExpiresAt.HasValue
                    ? (payment.PixExpiresAt.Value.Kind == DateTimeKind.Utc ? payment.PixExpiresAt.Value : DateTime.SpecifyKind(payment.PixExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                var isPixSingleValid = !string.IsNullOrEmpty(payment.PixCode) && 
                                       (!expiresAtUtc.HasValue || expiresAtUtc.Value > DateTime.UtcNow);
                if (isPixSingleValid)
                    preservePixForReuse = true;
            }
            payment.Method = method;
            if (!preservePixForReuse && (pixPlanType == ContractCardPlanType.None || method != PaymentMethod.PIX))
            {
                payment.PixCode = null;
                payment.PixExpiresAt = null;
                payment.PixConciliationIdentifier = null;
                if (method == PaymentMethod.PIX && pixPlanType == ContractCardPlanType.None)
                    payment.ExternalPaymentId = string.Empty;
            }
            // Sempre persistir a alteração de método (ex.: para CreditCard/Boleto a condição acima não chama UpdateAsync)
            await _paymentRepository.UpdateAsync(payment);

            if (method == PaymentMethod.PIX && subscription != null && contract != null && pixPlanType.HasValue)
            {
                contract.CardPlanType = pixPlanType.Value;
                await _contractRepository.UpdateAsync(contract);
                // Importante: não limpar PixAutomaticAuthorizationId aqui.
                // Isso permite reaproveitar a autorização PIX Automático existente
                // mesmo que o usuário alterne temporariamente o tipo de PIX.
            }
            
            // PIX: gerar código na mesma requisição ao trocar forma (único ou recorrente anual), para o card exibir o QR ao clicar no radio; GET só reutiliza se já houver código válido.
            if (method == PaymentMethod.PIX)
            {
                await GeneratePixCodeAsync(paymentId, pixPlanType);
            }
            else if (method == PaymentMethod.BankTransfer)
            {
                var boletoExpUtc = payment.BoletoExpiresAt.HasValue
                    ? (payment.BoletoExpiresAt.Value.Kind == DateTimeKind.Utc ? payment.BoletoExpiresAt.Value : DateTime.SpecifyKind(payment.BoletoExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                var boletoValid = !string.IsNullOrEmpty(payment.BoletoCode) && (!boletoExpUtc.HasValue || boletoExpUtc.Value > DateTime.UtcNow);
                if (!boletoValid)
                    await GenerateBoletoCodeAsync(paymentId);
            }
            return await _paymentRepository.GetByIdAsync(paymentId) ?? payment;
        }
        public async Task ReopenCancelledPixPaymentAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Method != PaymentMethod.PIX)
                throw new InvalidOperationException("Apenas pagamentos PIX podem ser reabertos para novo código.");
            if (payment.Status != PaymentStatus.Cancelled)
                return;
            payment.Status = PaymentStatus.Pending;
            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.PixConciliationIdentifier = null;
            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                var contract = subscription != null ? await _contractRepository.GetByIdAsync(subscription.ContractId) : null;
                if (contract?.CardPlanType == ContractCardPlanType.PixAnnual && subscription != null)
                {
                    subscription.PixAutomaticPayload = null;
                    subscription.PixAutomaticPayloadExpiresAt = null;
                    subscription.PixAutomaticConciliationIdentifier = null;
                    await _subscriptionRepository.UpdateAsync(subscription);
                }
            }
            await _paymentRepository.UpdateAsync(payment);
        }

        public async Task HandlePixPaymentOverdueAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Method != PaymentMethod.PIX)
                throw new InvalidOperationException("Apenas pagamentos PIX podem ser tratados no vencimento.");
            var subscription = payment.Subscription;
            var contract = subscription?.Contract;
            if (subscription == null || contract == null)
                throw new InvalidOperationException("Pagamento sem assinatura ou contrato associado.");
            // Limpar códigos antigos; a data de vencimento será a data real do novo código retornada pelo Asaas em GeneratePixCodeAsync
            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.PixConciliationIdentifier = null;
            if (contract.CardPlanType != ContractCardPlanType.PixAnnual)
            {
                // PIX único: limpar id externo para que seja criada nova cobrança no Asaas
                payment.ExternalPaymentId = string.Empty;
            }
            else
            {
                // PIX Automático: invalidar payload armazenado na assinatura para forçar nova requisição de QR ao Asaas
                subscription.PixAutomaticPayload = null;
                subscription.PixAutomaticPayloadExpiresAt = null;
                subscription.PixAutomaticConciliationIdentifier = null;
                await _subscriptionRepository.UpdateAsync(subscription);
            }
            await _paymentRepository.UpdateAsync(payment);
            await GeneratePixCodeAsync(paymentId, contract.CardPlanType);
        }

        public async Task EnsurePixCodeValidAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null || payment.Method != PaymentMethod.PIX || payment.Status != PaymentStatus.Pending)
                return;
            var subscription = payment.Subscription;
            var contract = subscription?.Contract;
            if (subscription == null || contract == null)
                return;
            // PIX único: expiração em Payments.PixExpiresAt | PIX Automático: expiração em Subscriptions.PixAutomaticPayloadExpiresAt
            DateTime? expiresAt;
            if (contract.CardPlanType == ContractCardPlanType.PixAnnual)
            {
                expiresAt = subscription.PixAutomaticPayloadExpiresAt;
            }
            else
            {
                expiresAt = payment.PixExpiresAt;
            }
            var shouldRegenerate = !expiresAt.HasValue;
            if (!shouldRegenerate)
            {
                var exp = expiresAt!.Value;
                var expAsUtc = exp.Kind == DateTimeKind.Utc ? exp : DateTime.SpecifyKind(exp, DateTimeKind.Utc);
                var nowUtc = DateTime.UtcNow;
                shouldRegenerate = expAsUtc < nowUtc || (expAsUtc - nowUtc).TotalDays >= 365;
            }
            if (!shouldRegenerate)
                return;
            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.PixConciliationIdentifier = null;
            // PIX único: sempre limpar ExternalPaymentId para forçar nova cobrança no Asaas. Também limpar quando não tem PixConciliationIdentifier (cobrança era única, não automática).
            var isSinglePix = contract.CardPlanType != ContractCardPlanType.PixAnnual || string.IsNullOrEmpty(payment.PixConciliationIdentifier);
            if (isSinglePix)
                payment.ExternalPaymentId = string.Empty;
            else
            {
                subscription.PixAutomaticPayload = null;
                subscription.PixAutomaticPayloadExpiresAt = null;
                subscription.PixAutomaticConciliationIdentifier = null;
                await _subscriptionRepository.UpdateAsync(subscription);
            }
            await _paymentRepository.UpdateAsync(payment);
            // Usar o tipo do contrato para decidir o endpoint: PixAnnual -> /pix/automatic/authorizations; caso contrário -> /payments (PIX único). Não inferir pelo estado antigo do payment (ex.: usuário acabou de trocar para PIX recorrente anual).
            var planForRegeneration = contract.CardPlanType == ContractCardPlanType.PixAnnual ? ContractCardPlanType.PixAnnual : ContractCardPlanType.None;
            await GeneratePixCodeAsync(paymentId, planForRegeneration);
        }

        public async Task<string> GeneratePixCodeAsync(int paymentId, ContractCardPlanType? pixPlanType = null)
        {
            // Leitura direta do banco (AsNoTracking) para reuso: se já existir PixCode válido na tabela Payments, retornar sem chamar API.
            var pixFromDb = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Id == paymentId)
                .Select(p => new { p.PixCode, p.PixExpiresAt, p.PixConciliationIdentifier, p.Subscription })
                .FirstOrDefaultAsync();
            if (pixPlanType != null && pixPlanType == ContractCardPlanType.None && pixFromDb != null && !string.IsNullOrEmpty(pixFromDb.PixCode))
            {
                var expiresAtUtc = pixFromDb.PixExpiresAt.HasValue
                    ? (pixFromDb.PixExpiresAt.Value.Kind == DateTimeKind.Utc ? pixFromDb.PixExpiresAt.Value : DateTime.SpecifyKind(pixFromDb.PixExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                if (!expiresAtUtc.HasValue || expiresAtUtc.Value > DateTime.UtcNow)
                    return pixFromDb.PixCode;
            }
            // PIX recorrente anual: código e expiração vêm somente de Subscriptions (PixAutomaticPayload, PixAutomaticPayloadExpiresAt)
            else if (pixPlanType != null && pixPlanType == ContractCardPlanType.PixAnnual && pixFromDb != null && !string.IsNullOrEmpty(pixFromDb.Subscription?.PixAutomaticPayload))
            {
                var expiresAtUtc = pixFromDb.Subscription.PixAutomaticPayloadExpiresAt.HasValue
                    ? (pixFromDb.Subscription.PixAutomaticPayloadExpiresAt.Value.Kind == DateTimeKind.Utc ? pixFromDb.Subscription.PixAutomaticPayloadExpiresAt.Value : DateTime.SpecifyKind(pixFromDb.Subscription.PixAutomaticPayloadExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                if (!expiresAtUtc.HasValue || expiresAtUtc.Value > DateTime.UtcNow)
                    return pixFromDb.Subscription.PixAutomaticPayload;
            }

            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");
            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }
            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }
            var clientPhone = client.ContactPhone ?? client.User.Phone;
            var validation = _asaasService.ValidateCustomerDataForPayment(clientName, clientEmail, clientTaxId, clientPhone, payment.Amount);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            var street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado);
            var number = "S/N";
            var locality = DefaultDisplayStrings.NaoInformado;
            var city = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado);
            var regionCode = "SP";
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2 && BrazilianUfs.All.Contains(stateTemp.Substring(0, 2)))
                    regionCode = stateTemp.Substring(0, 2);
            }
            var postalCode = "00000000";
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode.Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                if (postalCodeTemp.Length >= 8 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp.Length > 8 ? postalCodeTemp.Substring(0, 8) : postalCodeTemp;
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp.PadLeft(8, '0');
            }
            var clientAddress = new PagBankAddress
            {
                Street = street,
                Number = number,
                Complement = null,
                Locality = locality,
                City = city,
                RegionCode = regionCode,
                Country = "BRA",
                PostalCode = postalCode
            };

            // Decisão explícita: PIX recorrente anual -> POST /api/v3/pix/automatic/authorizations | PIX (pagamento único) -> POST /api/v3/payments
            var effectivePixPlan = pixPlanType.HasValue ? pixPlanType.Value : (contract.CardPlanType ?? ContractCardPlanType.None);
            var usePixAutomatic = effectivePixPlan == ContractCardPlanType.PixAnnual;

            if (usePixAutomatic)
            {
                // Reaproveitar só se for realmente payload de PIX Automático (tem conciliação) e ainda válido
                // PIX Automático: código e expiração vêm somente de Subscriptions; não gravar em Payments.PixCode
                var hasValidStoredPayload = !string.IsNullOrEmpty(subscription.PixAutomaticPayload) &&
                    subscription.PixAutomaticPayloadExpiresAt.HasValue &&
                    subscription.PixAutomaticPayloadExpiresAt.Value > DateTime.UtcNow;
                if (hasValidStoredPayload)
                {
                    if (subscription.PixAutomaticPayloadExpiresAt.HasValue)
                    {
                        payment.DueDate = subscription.PixAutomaticPayloadExpiresAt.Value;
                        payment.PixCode = null;
                        payment.PixExpiresAt = null;
                        await _paymentRepository.UpdateAsync(payment);
                    }
                    return subscription.PixAutomaticPayload;
                }
                var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                    clientName, clientEmail, clientTaxId, clientPhone, clientAddress, "client_" + client.Id);
                var baseContractId = $"{contract.ContractNumber ?? ""}-{contract.Id}";
                if (baseContractId.Length > 35)
                    baseContractId = baseContractId[..35];
                
                // Garantir que o payment não carrega referência de cobrança PIX único (evita exibir/uso errado depois)
                //payment.ExternalPaymentId = string.Empty;
                
                // Tentar com contractId padrão primeiro (reaproveitar autorização existente no Asaas)
                try
                {
                    var authResponse = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        baseContractId,
                        payment.Amount,
                        $"Contrato {contract.ContractNumber} - PIX anual",
                        86400);
                    if (authResponse != null && !string.IsNullOrEmpty(authResponse.Id))
                    {
                        var expiresAt = AsaasDateParser.TryParse(authResponse.ImmediateQrCode?.ExpirationDate) ?? DateTime.UtcNow.AddHours(24);
                        var payload = !string.IsNullOrEmpty(authResponse.ImmediateQrCode?.Payload)
                            ? authResponse.ImmediateQrCode.Payload
                            : authResponse.Payload;
                        if (string.IsNullOrEmpty(payload))
                            throw new InvalidOperationException("Asaas PIX Automático retornou autorização sem payload do QR Code. Verifique a resposta da API.");
                        // PIX Automático: gravar somente em Subscriptions (PixAutomaticPayload, PixAutomaticPayloadExpiresAt); não gravar em Payments.PixCode
                        subscription.PixAutomaticAuthorizationId = authResponse.Id;
                        subscription.PixAutomaticPayload = payload;
                        subscription.PixAutomaticPayloadExpiresAt = expiresAt;
                        subscription.PixAutomaticConciliationIdentifier = authResponse.ImmediateQrCode?.ConciliationIdentifier;
                        await _subscriptionRepository.UpdateAsync(subscription);
                        payment.PixCode = null;
                        payment.PixExpiresAt = null;
                        payment.PixConciliationIdentifier = null;
                        payment.DueDate = expiresAt;
                        await _paymentRepository.UpdateAsync(payment);
                        return subscription.PixAutomaticPayload;
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Já existe uma autorização", StringComparison.OrdinalIgnoreCase))
                {
                    // Usuário voltou de cartão/PIX normal para PIX recorrente: criar nova autorização com contractId único
                    var uniqueContractId = $"{baseContractId}-{Guid.NewGuid().ToString("N")[..8]}";
                    if (uniqueContractId.Length > 35) uniqueContractId = uniqueContractId[..35];
                    var retryAuth = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        uniqueContractId,
                        payment.Amount,
                        $"Contrato {contract.ContractNumber} - PIX anual",
                        86400);
                    if (retryAuth != null && !string.IsNullOrEmpty(retryAuth.Id))
                    {
                        var expiresAtRetry = AsaasDateParser.TryParse(retryAuth.ImmediateQrCode?.ExpirationDate) ?? DateTime.UtcNow.AddHours(24);
                        var payloadRetry = !string.IsNullOrEmpty(retryAuth.ImmediateQrCode?.Payload) ? retryAuth.ImmediateQrCode.Payload : retryAuth.Payload;
                        if (string.IsNullOrEmpty(payloadRetry))
                            throw new InvalidOperationException("Asaas PIX Automático (retry) retornou autorização sem payload do QR Code.");
                        subscription.PixAutomaticAuthorizationId = retryAuth.Id;
                        subscription.PixAutomaticPayload = payloadRetry;
                        subscription.PixAutomaticPayloadExpiresAt = expiresAtRetry;
                        subscription.PixAutomaticConciliationIdentifier = retryAuth.ImmediateQrCode?.ConciliationIdentifier;
                        await _subscriptionRepository.UpdateAsync(subscription);
                        return subscription.PixAutomaticPayload;
                    }
                    throw new InvalidOperationException(
                        "Já existe uma autorização PIX recorrente para este contrato. O código anterior pode ter expirado. Entre em contato com o suporte para gerar um novo código se necessário.");
                }
                throw new InvalidOperationException(
                    "Não foi possível criar a autorização PIX Automático. Verifique no painel Asaas se a funcionalidade está habilitada e os logs da aplicação.");
            }

            // PIX único: reuso do banco já feito no início do método; daqui em diante só quando expirou ou não tem código. Opcional: tentar Asaas por ExternalPaymentId antes de criar nova cobrança.
            // Reaproveitar cobrança PIX já existente no Asaas só se tiver ExternalPaymentId e QR ainda válido
            if (!string.IsNullOrEmpty(payment.ExternalPaymentId))
            {
                var existingQr = await _asaasService.GetPixQrCodeAsync(payment.ExternalPaymentId);
                if (existingQr != null && !string.IsNullOrEmpty(existingQr.Payload))
                {
                    if (!string.IsNullOrEmpty(existingQr.ExpirationDate) && DateTime.TryParse(existingQr.ExpirationDate, out var exp))
                    {
                        payment.PixExpiresAt = exp;
                        payment.DueDate = exp;
                        if (exp > DateTime.UtcNow)
                        {
                            payment.PixCode = existingQr.Payload;
                            await _paymentRepository.UpdateAsync(payment);
                            return payment.PixCode;
                        }
                    }
                }
            }
            // PIX (pagamento único): POST /api/v3/payments — só chega aqui quando usePixAutomatic é false
            var pixReferenceId = !string.IsNullOrEmpty(payment.ExternalPaymentId)
                ? payment.ExternalPaymentId
                : $"pix_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: pixReferenceId,
                amount: payment.Amount,
                method: PaymentMethod.PIX,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                clientAddress: clientAddress,
                customerExternalReference: "client_" + client.Id
            );
            payment.ExternalPaymentId = orderResponse.Id;
            if (orderResponse.QrCodes != null && orderResponse.QrCodes.Any())
            {
                var qrCode = orderResponse.QrCodes.First();
                payment.PixCode = qrCode.Text;
                // Data de vencimento do PIX único: vem do Asaas (GetPixQrCodeAsync após POST /payments) em AsaasService.CreateOrderAsync
                payment.PixExpiresAt = qrCode.ExpirationDate;
                if (qrCode.ExpirationDate.HasValue)
                    payment.DueDate = qrCode.ExpirationDate.Value;
            }
            await _paymentRepository.UpdateAsync(payment);
            return payment.PixCode ?? "";
        }
        public async Task<string> GenerateBoletoCodeAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");
            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }
            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }
            var clientPhone = client.ContactPhone ?? client.User.Phone;
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            var street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado);
            var number = "S/N";
            var locality = DefaultDisplayStrings.NaoInformado;
            var city = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado);
            var regionCode = "SP";
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2)
                {
                    stateTemp = stateTemp.Substring(0, 2);
                    if (BrazilianUfs.All.Contains(stateTemp))
                    {
                        regionCode = stateTemp;
                    }
                }
            }
            var country = "BRA";
            var postalCode = "00000000";
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode
                    .Replace("-", "")
                    .Replace(".", "")
                    .Replace(" ", "")
                    .Trim();
                if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                {
                    postalCode = postalCodeTemp;
                }
                else if (postalCodeTemp.Length > 8)
                {
                    postalCode = postalCodeTemp.Substring(0, 8);
                }
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                {
                    postalCode = postalCodeTemp.PadLeft(8, '0');
                }
            }
            var clientAddress = new PagBankAddress
            {
                Street = street,
                Number = number,
                Complement = null,
                Locality = locality,
                City = city,
                RegionCode = regionCode,
                Country = country,
                PostalCode = postalCode
            };
            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.BankTransfer,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                clientAddress: clientAddress,
                customerExternalReference: "client_" + client.Id
            );
            payment.ExternalPaymentId = orderResponse.Id;
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                if (charge.PaymentMethod?.Boleto != null)
                {
                    var rawBoleto = charge.PaymentMethod.Boleto.FormattedBarcode ?? charge.PaymentMethod.Boleto.Barcode;
                    var (barcode44, digitableFormatted) = BoletoHelper.NormalizeBoletoValues(rawBoleto);
                    payment.BoletoBarcode = !string.IsNullOrEmpty(barcode44) ? barcode44 : rawBoleto;
                    payment.BoletoCode = !string.IsNullOrEmpty(digitableFormatted) ? digitableFormatted : rawBoleto;
                    payment.BoletoExpiresAt = charge.PaymentMethod.Boleto.DueDate;
                    var bankSlipLink = charge.PaymentMethod.Boleto.Links?.FirstOrDefault(l => string.Equals(l.Rel, "bankSlip", StringComparison.OrdinalIgnoreCase));
                    payment.BankSlipUrl = !string.IsNullOrWhiteSpace(bankSlipLink?.Href) ? bankSlipLink.Href.Trim() : null;
                    if (charge.PaymentMethod.Boleto.DueDate.HasValue)
                        payment.DueDate = charge.PaymentMethod.Boleto.DueDate.Value.Kind == DateTimeKind.Utc
                            ? charge.PaymentMethod.Boleto.DueDate.Value
                            : DateTime.SpecifyKind(charge.PaymentMethod.Boleto.DueDate.Value, DateTimeKind.Utc);
                }
            }
            await _paymentRepository.UpdateAsync(payment);
            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
        }
        public async Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null, string? remoteIp = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ser processados");
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");
            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }
            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }
            var clientPhone = client.ContactPhone ?? client.User.Phone;
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            var postalCode = "00000000";
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode.Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp;
                else if (postalCodeTemp.Length > 8)
                    postalCode = postalCodeTemp.Substring(0, 8);
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp.PadLeft(8, '0');
            }
            if (postalCode == "00000000" || string.IsNullOrWhiteSpace(client.ZipCode))
            {
                throw new InvalidOperationException("CEP não informado. Atualize em 'Meu Perfil' com o CEP do titular do cartão.");
            }
            var regionCode = "SP";
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2 && BrazilianUfs.All.Contains(stateTemp.Substring(0, 2)))
                    regionCode = stateTemp.Substring(0, 2);
            }
            var clientAddress = new PagBankAddress
            {
                Street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado),
                Number = "S/N",
                Complement = null,
                Locality = DefaultDisplayStrings.NaoInformado,
                City = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado),
                RegionCode = regionCode,
                Country = "BRA",
                PostalCode = postalCode
            };
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }
            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: amountToCharge,
                method: PaymentMethod.CreditCard,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                card: card,
                installments: installments,
                clientAddress: clientAddress,
                remoteIp: remoteIp
            );
            payment.ExternalPaymentId = orderResponse.Id;
            payment.Status = PaymentStatus.Processing;
            var installmentCount = installments ?? 1;
            payment.InstallmentCount = installmentCount;
            payment.InstallmentValue = Math.Round(amountToCharge / installmentCount, 2);
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                payment.Status = charge.Status.ToUpper() == "PAID" ? PaymentStatus.Paid :
                                 charge.Status.ToUpper() == "DECLINED" ? PaymentStatus.Failed :
                                 PaymentStatus.Processing;
                if (charge.PaidAt.HasValue)
                {
                    payment.PaidAt = EnsurePaidAtWithTime(charge.PaidAt.Value);
                }
                if (charge.PaymentMethod?.Card != null)
                {
                    payment.TransactionId = charge.Id;
                }
            }
            await _paymentRepository.UpdateAsync(payment);
            if (payment.Status == PaymentStatus.Paid)
            {
                await ConfirmPaymentAsync(payment.Id, payment.TransactionId);
            }
            return payment;
        }

        public async Task<Payment> ProcessPaymentWithSavedCardAsync(int paymentId, int creditCardId, int? installments = null, string? remoteIp = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ser processados");
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");
            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var card = await _context.CreditCards
                .FirstOrDefaultAsync(c => c.Id == creditCardId && c.ClientId == client.Id);
            if (card == null)
                throw new InvalidOperationException("Cartão não encontrado.");
            if (string.IsNullOrEmpty(card.AsaasToken))
                throw new InvalidOperationException("Este cartão não está habilitado para pagamento rápido. Remova e cadastre-o novamente para usar esta opção.");
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? "";
            if (string.IsNullOrWhiteSpace(clientName))
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil'.");
            var clientEmail = client.ContactEmail ?? client.User?.Email ?? "";
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil'.");
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil'.");
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            var postalCode = "00000000";
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode.Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp;
                else if (postalCodeTemp.Length > 8)
                    postalCode = postalCodeTemp.Substring(0, 8);
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp.PadLeft(8, '0');
            }
            if (postalCode == "00000000" || string.IsNullOrWhiteSpace(client.ZipCode))
                throw new InvalidOperationException("CEP não informado. Atualize em 'Meu Perfil' com o CEP do titular do cartão.");
            var regionCode = "SP";
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2 && BrazilianUfs.All.Contains(stateTemp.Substring(0, 2)))
                    regionCode = stateTemp.Substring(0, 2);
            }
            var clientAddress = new PagBankAddress
            {
                Street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado),
                Number = "S/N",
                Complement = null,
                Locality = DefaultDisplayStrings.NaoInformado,
                City = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado),
                RegionCode = regionCode,
                Country = "BRA",
                PostalCode = postalCode
            };
            var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                clientName, clientEmail, clientTaxId, clientPhone, clientAddress, "client_" + client.Id);
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }
            var inst = installments ?? 1;
            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: amountToCharge,
                method: PaymentMethod.CreditCard,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                card: null,
                installments: inst,
                clientAddress: clientAddress,
                creditCardToken: card.AsaasToken,
                asaasCustomerIdForToken: asaasCustomerId,
                remoteIp: remoteIp
            );
            payment.ExternalPaymentId = orderResponse.Id;
            payment.Status = PaymentStatus.Processing;
            payment.InstallmentCount = inst;
            payment.InstallmentValue = Math.Round(amountToCharge / inst, 2);
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                payment.Status = charge.Status.ToUpper() == "PAID" ? PaymentStatus.Paid :
                                 charge.Status.ToUpper() == "DECLINED" ? PaymentStatus.Failed :
                                 PaymentStatus.Processing;
                if (charge.PaidAt.HasValue)
                    payment.PaidAt = EnsurePaidAtWithTime(charge.PaidAt.Value);
                if (charge.PaymentMethod?.Card != null)
                    payment.TransactionId = charge.Id;
            }
            await _paymentRepository.UpdateAsync(payment);
            if (payment.Status == PaymentStatus.Paid)
                await ConfirmPaymentAsync(payment.Id, payment.TransactionId);
            return payment;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync()
        {
            return await _paymentRepository.GetPendingPaymentsAsync();
        }
        public async Task<Payment> ConfirmPaymentAsync(int paymentId, string? transactionId = null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var payment = await _paymentRepository.GetByIdAsync(paymentId);
                    if (payment == null)
                        throw new InvalidOperationException("Pagamento não encontrado");
                    if (payment.Status == PaymentStatus.Paid)
                    {
                        await transaction.CommitAsync();
                        return payment;
                    }
                    var oldStatus = payment.Status;
                    var oldPaidAt = payment.PaidAt;
                    var oldTransactionId = payment.TransactionId;
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = GetBrazilianTime();
                    if (!string.IsNullOrEmpty(transactionId))
                    {
                        payment.TransactionId = transactionId;
                    }
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);
                    int? userId = null;
                    if (_httpContextAccessor?.HttpContext?.User != null)
                    {
                        var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedUserId))
                        {
                            userId = parsedUserId;
                        }
                    }
                    await LogAuditAsync(
                        userId,
                        "Pagamento aprovado",
                        "Payment",
                        payment.Id,
                        new { Status = oldStatus.ToString(), PaidAt = oldPaidAt, TransactionId = oldTransactionId },
                        new { Status = payment.Status.ToString(), PaidAt = payment.PaidAt, TransactionId = payment.TransactionId, Amount = payment.Amount }
                    );
                    _context.PaymentHistories.Add(new PaymentHistory
                    {
                        PaymentId = payment.Id,
                        Action = PaymentHistoryActions.Approved,
                        ActionBy = userId,
                        ActionAt = DateTime.UtcNow
                    });
                    if (payment.SubscriptionId.HasValue)
                    {
                        var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                        if (subscription != null)
                        {
                            subscription.LastBillingDate = DateTime.UtcNow;
                            var contractForCycle = await _context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
                            subscription.NextBillingDate = contractForCycle?.CardPlanType == ContractCardPlanType.Card1x || contractForCycle?.CardPlanType == ContractCardPlanType.PixAnnual
                                ? subscription.LastBillingDate.Value.AddYears(1)
                                : subscription.LastBillingDate.Value.AddMonths(1);
                            subscription.FailedPaymentAttempts = 0;
                            if (subscription.Status == SubscriptionStatus.PastDue || subscription.Status == SubscriptionStatus.Suspended)
                            {
                                subscription.Status = SubscriptionStatus.Active;
                            }
                            subscription.UpdatedAt = DateTime.UtcNow;
                            _context.Subscriptions.Update(subscription);
                            var contract = await _context.Contracts
                                .Include(c => c.CoworkingAddress)
                                .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
                            if (contract != null && (contract.Status == ContractStatus.Suspended || contract.Status == ContractStatus.PendingPayment))
                            {
                                var previousPaidPayments = await _context.Payments
                                    .Where(p => p.SubscriptionId == subscription.Id &&
                                               p.Id != paymentId &&
                                               p.Status == PaymentStatus.Paid)
                                    .CountAsync();
                                if (previousPaidPayments == 0)
                                {
                                    var originalStatus = contract.Status;
                                    contract.Status = ContractStatus.Active;
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                    if (contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                                    {
                                        contract.CoworkingAddress.CurrentContracts++;
                                        if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                                        {
                                            contract.CoworkingAddress.IsAvailable = false;
                                        }
                                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
                                    }
                                    if (_addressDeclarationService != null && contract.ExpiryDate.HasValue)
                                    {
                                        try
                                        {
                                            await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                                        }
                                    }
                                    await CancelOtherPendingPaymentContractsForClientAsync(contract.ClientId, contract.Id);
                                }
                                else
                                {
                                    if (contract.ExpiryDate.HasValue)
                                    {
                                        contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                    }
                                    else
                                    {
                                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    }
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                }
                            }
                            else if (contract != null && contract.Status == ContractStatus.Active)
                            {
                                if (contract.ExpiryDate.HasValue)
                                {
                                    contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                }
                                else
                                {
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                }
                                contract.UpdatedAt = DateTime.UtcNow;
                                _context.Contracts.Update(contract);
                            }
                        }
                    }
                    if (payment.FiscalOnboardingLeadId.HasValue && _fiscalOnboardingService != null)
                    {
                        await _fiscalOnboardingService.HandleAdhesionPaymentConfirmedAsync(payment.Id, CancellationToken.None);
                    }
                    if (!payment.FiscalOnboardingLeadId.HasValue)
                    {
                        await _commissionService.CreateCommissionAsync(payment.Id);
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        public async Task<List<Payment>> GetPaymentsByClientIdAsync(int clientId)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ClientId == clientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        public async Task<Payment?> GetPaymentByIdAsync(int paymentId)
        {
            return await _paymentRepository.GetByIdAsync(paymentId);
        }
        private string GenerateCRC16(string data)
        {
            ushort crc = 0xFFFF;
            foreach (char c in data)
            {
                crc ^= (ushort)(c << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return (crc ^ 0xFFFF).ToString("X4");
        }
    }
}