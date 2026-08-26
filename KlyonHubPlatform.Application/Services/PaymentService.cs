using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Security.Claims;
using FluentValidation;
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
        private readonly IDocumentService? _documentService;
        private readonly IContractDocumentsWorkQueue? _contractDocumentsWorkQueue;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IFiscalOnboardingService? _fiscalOnboardingService;
        private readonly IPaymentRealtimeNotifier? _paymentRealtimeNotifier;
        private readonly IValidator<CreditCardHolderBillingInput> _cardHolderBillingValidator;
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<PaymentService> _logger;
        public PaymentService(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            ICommissionService commissionService,
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            IAsaasService asaasService,
            ApplicationDbContext context,
            ISystemConfigurationService systemConfigurationService,
            IValidator<CreditCardHolderBillingInput> cardHolderBillingValidator,
            IPromoCodeService promoCodeService,
            IAddressDeclarationService? addressDeclarationService = null,
            IDocumentService? documentService = null,
            IContractDocumentsWorkQueue? contractDocumentsWorkQueue = null,
            IAuditService? auditService = null,
            IHttpContextAccessor? httpContextAccessor = null,
            IFiscalOnboardingService? fiscalOnboardingService = null,
            IPaymentRealtimeNotifier? paymentRealtimeNotifier = null,
            ILogger<PaymentService>? logger = null)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _commissionService = commissionService;
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _asaasService = asaasService;
            _context = context;
            _addressDeclarationService = addressDeclarationService;
            _documentService = documentService;
            _contractDocumentsWorkQueue = contractDocumentsWorkQueue;
            _systemConfigurationService = systemConfigurationService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
            _fiscalOnboardingService = fiscalOnboardingService;
            _paymentRealtimeNotifier = paymentRealtimeNotifier;
            _cardHolderBillingValidator = cardHolderBillingValidator;
            _promoCodeService = promoCodeService;
            _logger = logger ?? NullLogger<PaymentService>.Instance;
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

        private (string PostalCode, string AddressNumber) RequireCardHolderBilling(string? postalCode, string? addressNumber, bool addressHasNoNumber)
        {
            var input = new CreditCardHolderBillingInput
            {
                PostalCode = postalCode,
                AddressNumber = addressNumber,
                AddressHasNoNumber = addressHasNoNumber
            };
            var validation = _cardHolderBillingValidator.Validate(input);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Errors[0].ErrorMessage);
            if (!CreditCardHolderBilling.TryNormalize(postalCode, addressNumber, addressHasNoNumber, out var digits, out var number, out var error))
                throw new InvalidOperationException(error);
            return (digits, number);
        }

        /// <summary>
        /// Usa billing do request quando válido; senão, CEP/número já salvos no cartão.
        /// Se o cartão ainda não tinha billing e o request trouxe, persiste no cartão.
        /// </summary>
        private async Task<(string PostalCode, string AddressNumber)> ResolveBillingForSavedCardAsync(
            CreditCard card,
            string? postalCode,
            string? addressNumber,
            bool addressHasNoNumber,
            CancellationToken cancellationToken = default)
        {
            string holderPostal;
            string holderNumber;
            var fromRequest = CreditCardHolderBilling.TryNormalize(postalCode, addressNumber, addressHasNoNumber, out holderPostal, out holderNumber, out _);
            if (!fromRequest)
            {
                if (CreditCardHolderBilling.TryReadStored(card.PostalCode, card.AddressNumber, out holderPostal, out holderNumber))
                    return (holderPostal, holderNumber);
                return RequireCardHolderBilling(postalCode, addressNumber, addressHasNoNumber);
            }

            if (!CreditCardHolderBilling.TryReadStored(card.PostalCode, card.AddressNumber, out _, out _))
            {
                card.PostalCode = holderPostal;
                card.AddressNumber = holderNumber;
                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return (holderPostal, holderNumber);
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
        public async Task<Payment> CreatePaymentAsync(
            int? subscriptionId,
            decimal amount,
            PaymentMethod method,
            decimal snapshotListPrice = 0,
            string? promoCodeSnapshot = null,
            decimal snapshotDiscountAmount = 0,
            int? promoCodeId = null)
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
                SnapshotListPrice = snapshotListPrice > 0 ? snapshotListPrice : amount,
                PromoCodeSnapshot = promoCodeSnapshot,
                SnapshotDiscountAmount = snapshotDiscountAmount,
                PromoCodeId = promoCodeId,
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
                            subscription.NextBillingDate = contractForCycle?.CardPlanType.IsAnnualBillingCycle() == true
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
                                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
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
        public async Task<Payment> UpdatePaymentMethodAsync(
            int paymentId,
            PaymentMethod method,
            ContractCardPlanType? pixPlanType = null,
            ContractCardPlanType? previousCardPlanType = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            // Paid: não altera método nem cancela no Asaas
            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ter o método alterado");

            var previousMethod = payment.Method;
            var methodChanged = previousMethod != method;
            Subscription? subscription = null;
            Contract? contract = null;
            if (payment.SubscriptionId.HasValue)
            {
                subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                if (subscription != null)
                    contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            }

            // previousCardPlanType: plano antes do Sync no ClientController (CardPlanType no DB pode já ser o novo).
            var effectivePreviousPlan = previousCardPlanType ?? contract?.CardPlanType;
            var pixPlanChanged = method == PaymentMethod.PIX
                && previousMethod == PaymentMethod.PIX
                && pixPlanType.HasValue
                && effectivePreviousPlan.HasValue
                && effectivePreviousPlan != pixPlanType;

            // Cancelar no Asaas ao sair do método OU ao trocar ciclo PIX (anual ↔ mensal ↔ único) mantendo Method=PIX.
            // Nota cartão: se ExternalSubscriptionId for sub_*, IAsaasService não expõe DELETE /subscriptions hoje —
            // troca Card1x ↔ CardMonthly no checkout só ajusta valor local; cobrança em ProcessPaymentWithCard.
            if (methodChanged || pixPlanChanged)
            {
                var cancelContextMethod = previousMethod == PaymentMethod.PIX || pixPlanChanged
                    ? PaymentMethod.PIX
                    : previousMethod;
                await TryCancelPendingAsaasChargeOnMethodChangeAsync(
                    payment,
                    subscription,
                    cancelContextMethod,
                    pixPlanChanged ? "troca de plano PIX" : null);

                payment.ExternalPaymentId = string.Empty;

                if (previousMethod == PaymentMethod.PIX || pixPlanChanged)
                {
                    ClearLocalPixCheckoutArtifacts(payment, subscription);
                    if (subscription != null)
                        await _subscriptionRepository.UpdateAsync(subscription);
                }
                else if (previousMethod == PaymentMethod.BankTransfer && methodChanged)
                {
                    payment.BoletoCode = null;
                    payment.BoletoBarcode = null;
                    payment.BoletoExpiresAt = null;
                    payment.BankSlipUrl = null;
                }
            }

            var preservePixForReuse = false;
            // Só reaproveitar payload/código se o plano PIX NÃO mudou nesta requisição.
            if (!pixPlanChanged && method == PaymentMethod.PIX && pixPlanType.IsPixAutomatic() && subscription != null)
            {
                var expectedCycle = pixPlanType.IsAnnualBillingCycle() ? "Yearly" : "Monthly";
                var cycleMatches = string.Equals(subscription.BillingCycle, expectedCycle, StringComparison.OrdinalIgnoreCase);
                var amountMatches = subscription.MonthlyAmount == payment.Amount;
                var subExpUtc = subscription.PixAutomaticPayloadExpiresAt.HasValue
                    ? (subscription.PixAutomaticPayloadExpiresAt.Value.Kind == DateTimeKind.Utc ? subscription.PixAutomaticPayloadExpiresAt.Value : DateTime.SpecifyKind(subscription.PixAutomaticPayloadExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                var isAutomaticValid = !string.IsNullOrEmpty(subscription.PixAutomaticPayload) &&
                                    (!subExpUtc.HasValue || subExpUtc.Value > DateTime.UtcNow);
                if (isAutomaticValid && cycleMatches && amountMatches)
                    preservePixForReuse = true;
            }
            if (!pixPlanChanged && method == PaymentMethod.PIX && (pixPlanType == ContractCardPlanType.None || !pixPlanType.HasValue))
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
            await _paymentRepository.UpdateAsync(payment);

            if (method == PaymentMethod.PIX && subscription != null && contract != null && pixPlanType.HasValue)
            {
                contract.CardPlanType = pixPlanType.Value;
                await _contractRepository.UpdateAsync(contract);
            }

            // PIX: gerar código na mesma requisição ao trocar forma (único ou automático); cancelamento já feito acima.
            if (method == PaymentMethod.PIX)
            {
                await GeneratePixCodeAsync(paymentId, pixPlanType, previousCardPlanType: null);
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

        private static void ClearLocalPixCheckoutArtifacts(Payment payment, Subscription? subscription)
        {
            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.PixConciliationIdentifier = null;
            payment.ExternalPaymentId = string.Empty;
            if (subscription == null)
                return;
            subscription.PixAutomaticPayload = null;
            subscription.PixAutomaticPayloadExpiresAt = null;
            subscription.PixAutomaticConciliationIdentifier = null;
            subscription.PixAutomaticAuthorizationId = null;
        }

        /// <summary>
        /// Best-effort: cancela no Asaas charge <c>pay_*</c> pendente e, se o contexto for PIX,
        /// a autorização PIX Automático. Usado ao sair do método ou ao trocar plano PIX (ciclo/único).
        /// Falha na API não bloqueia; apenas registra log. Não é chamado se Paid.
        /// </summary>
        private async Task TryCancelPendingAsaasChargeOnMethodChangeAsync(
            Payment payment,
            Subscription? subscription,
            PaymentMethod previousMethod,
            string? reasonSuffix = null)
        {
            var previousMethodLabel = previousMethod switch
            {
                PaymentMethod.PIX => "PIX",
                PaymentMethod.BankTransfer => "Boleto",
                PaymentMethod.CreditCard => "Cartão",
                _ => previousMethod.ToString()
            };
            var reasonLabel = string.IsNullOrEmpty(reasonSuffix)
                ? $"ao sair de {previousMethodLabel}"
                : $"{reasonSuffix} (contexto {previousMethodLabel})";

            var externalId = payment.ExternalPaymentId?.Trim();
            var hasPendingPayCharge = !string.IsNullOrEmpty(externalId)
                && externalId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase);
            var hasBoletoArtifacts = previousMethod == PaymentMethod.BankTransfer
                && (!string.IsNullOrEmpty(payment.BoletoCode) || !string.IsNullOrEmpty(payment.BoletoBarcode));

            if (hasPendingPayCharge)
            {
                try
                {
                    await _asaasService.CancelChargeAsync(externalId!);
                    _logger.LogInformation(
                        "Cobrança Asaas {ExternalPaymentId} cancelada {Reason} (paymentId={PaymentId})",
                        externalId, reasonLabel, payment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao cancelar cobrança Asaas {ExternalPaymentId} {Reason} (paymentId={PaymentId}). Fluxo segue.",
                        externalId, reasonLabel, payment.Id);
                }
            }
            else if (hasBoletoArtifacts)
            {
                _logger.LogInformation(
                    "Saída de Boleto sem ExternalPaymentId pay_* (paymentId={PaymentId}); limpeza local de BoletoCode/Barcode.",
                    payment.Id);
            }

            if (previousMethod != PaymentMethod.PIX)
                return;

            var authId = subscription?.PixAutomaticAuthorizationId?.Trim();
            if (string.IsNullOrEmpty(authId))
                return;

            try
            {
                await _asaasService.CancelPixAutomaticAuthorizationAsync(authId);
                _logger.LogInformation(
                    "Autorização PIX Automático {AuthorizationId} cancelada {Reason} (paymentId={PaymentId})",
                    authId, reasonLabel, payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao cancelar autorização PIX Automático {AuthorizationId} {Reason} (paymentId={PaymentId}). Fluxo segue; ids locais serão limpos.",
                    authId, reasonLabel, payment.Id);
            }
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
                if (contract?.CardPlanType.IsPixAutomatic() == true && subscription != null)
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
            if (!contract.CardPlanType.IsPixAutomatic())
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
            if (contract.CardPlanType.IsPixAutomatic())
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
            var isSinglePix = !contract.CardPlanType.IsPixAutomatic() || string.IsNullOrEmpty(payment.PixConciliationIdentifier);
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
            // Usar o tipo do contrato: PixAnnual/PixMonthly -> /pix/automatic/authorizations; caso contrário -> /payments (PIX único).
            var planForRegeneration = contract.CardPlanType.IsPixAutomatic()
                ? contract.CardPlanType
                : ContractCardPlanType.None;
            await GeneratePixCodeAsync(paymentId, planForRegeneration);
        }

        public async Task<string> GeneratePixCodeAsync(
            int paymentId,
            ContractCardPlanType? pixPlanType = null,
            ContractCardPlanType? previousCardPlanType = null)
        {
            var pixPlanChanged = previousCardPlanType.HasValue
                && pixPlanType.HasValue
                && previousCardPlanType != pixPlanType;

            // Leitura direta do banco (AsNoTracking) para reuso: se já existir PixCode válido na tabela Payments, retornar sem chamar API.
            // Importante: SyncCheckoutAmount deve invalidar PixCode/PixAutomaticPayload quando Amount/plano mudam.
            // Não reutilizar se o plano PIX mudou nesta requisição (anual ↔ mensal ↔ único).
            var pixFromDb = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Id == paymentId)
                .Select(p => new { p.PixCode, p.PixExpiresAt, p.PixConciliationIdentifier, p.Subscription })
                .FirstOrDefaultAsync();
            if (!pixPlanChanged && pixPlanType != null && pixPlanType == ContractCardPlanType.None && pixFromDb != null && !string.IsNullOrEmpty(pixFromDb.PixCode))
            {
                var expiresAtUtc = pixFromDb.PixExpiresAt.HasValue
                    ? (pixFromDb.PixExpiresAt.Value.Kind == DateTimeKind.Utc ? pixFromDb.PixExpiresAt.Value : DateTime.SpecifyKind(pixFromDb.PixExpiresAt.Value, DateTimeKind.Utc))
                    : (DateTime?)null;
                if (!expiresAtUtc.HasValue || expiresAtUtc.Value > DateTime.UtcNow)
                    return pixFromDb.PixCode;
            }
            // PIX Automático (mensal/anual): código e expiração vêm somente de Subscriptions (PixAutomaticPayload, PixAutomaticPayloadExpiresAt)
            else if (!pixPlanChanged && pixPlanType != null && pixPlanType.IsPixAutomatic() && pixFromDb != null && !string.IsNullOrEmpty(pixFromDb.Subscription?.PixAutomaticPayload))
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

            // Troca de plano PIX via GeneratePixCode (sem UpdatePaymentMethod): cancelar pay_*/auth e limpar ids.
            if (pixPlanChanged)
            {
                await TryCancelPendingAsaasChargeOnMethodChangeAsync(
                    payment, subscription, PaymentMethod.PIX, "troca de plano PIX");
                ClearLocalPixCheckoutArtifacts(payment, subscription);
                await _subscriptionRepository.UpdateAsync(subscription);
                await _paymentRepository.UpdateAsync(payment);
            }

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var clientName = PaymentGatewayCustomer.ResolveName(client);
            var clientEmail = PaymentGatewayCustomer.ResolveEmail(client);
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Informe um e-mail válido na tela de cadastro.");
            }
            var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client);
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Informe um documento válido na tela de cadastro.");
            }
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            var validation = _asaasService.ValidateCustomerDataForPayment(clientName, clientEmail, clientTaxId, clientPhone, payment.Amount);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);
            var clientAddress = PaymentGatewayCustomer.ResolveAddress(client);

            // Decisão explícita: PIX Automático (mensal/anual) -> POST /api/v3/pix/automatic/authorizations | PIX (pagamento único) -> POST /api/v3/payments
            var effectivePixPlan = pixPlanType.HasValue ? pixPlanType.Value : (contract.CardPlanType ?? ContractCardPlanType.None);
            var usePixAutomatic = effectivePixPlan.IsPixAutomatic();
            var asaasFrequency = effectivePixPlan == ContractCardPlanType.PixMonthly ? "MONTHLY" : "ANNUALLY";
            var periodLabel = effectivePixPlan == ContractCardPlanType.PixMonthly ? "mensal" : "anual";

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
                        $"Contrato {contract.ContractNumber} - PIX {periodLabel}",
                        asaasFrequency,
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
                        payment.PixConciliationIdentifier = subscription.PixAutomaticConciliationIdentifier;
                        payment.DueDate = expiresAt;
                        await _paymentRepository.UpdateAsync(payment);
                        return subscription.PixAutomaticPayload;
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Já existe uma autorização", StringComparison.OrdinalIgnoreCase))
                {
                    // Usuário voltou de cartão/PIX normal para PIX Automático: criar nova autorização com contractId único
                    var uniqueContractId = $"{baseContractId}-{Guid.NewGuid().ToString("N")[..8]}";
                    if (uniqueContractId.Length > 35) uniqueContractId = uniqueContractId[..35];
                    var retryAuth = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        uniqueContractId,
                        payment.Amount,
                        $"Contrato {contract.ContractNumber} - PIX {periodLabel}",
                        asaasFrequency,
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
                        payment.PixConciliationIdentifier = subscription.PixAutomaticConciliationIdentifier;
                        payment.DueDate = expiresAtRetry;
                        await _paymentRepository.UpdateAsync(payment);
                        return subscription.PixAutomaticPayload;
                    }
                    throw new InvalidOperationException(
                        "Já existe uma autorização PIX Automático para este contrato. O código anterior pode ter expirado. Entre em contato com o suporte para gerar um novo código se necessário.");
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
            var clientName = PaymentGatewayCustomer.ResolveName(client);
            var clientEmail = PaymentGatewayCustomer.ResolveEmail(client);
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Informe um e-mail válido na tela de cadastro.");
            }
            var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client);
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Informe um documento válido na tela de cadastro.");
            }
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            var clientAddress = PaymentGatewayCustomer.ResolveAddress(client);
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
        public async Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null, string? remoteIp = null, string? postalCode = null, string? addressNumber = null, bool addressHasNoNumber = false)
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
            var clientName = PaymentGatewayCustomer.ResolveName(client);
            var clientEmail = PaymentGatewayCustomer.ResolveEmail(client);
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Informe um e-mail válido na tela de cadastro.");
            }
            var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client);
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Informe um documento válido na tela de cadastro.");
            }
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            var (holderPostalCode, holderAddressNumber) = RequireCardHolderBilling(postalCode, addressNumber, addressHasNoNumber);
            var clientAddress = PaymentGatewayCustomer.BuildCardHolderAddress(client, holderPostalCode, holderAddressNumber);
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }

            var orderResponse = await CreateAsaasCreditCardChargeAsync(
                payment,
                subscription,
                contract,
                amountToCharge,
                clientName,
                clientEmail,
                clientTaxId,
                clientPhone,
                clientAddress,
                card,
                creditCardToken: null,
                asaasCustomerIdForToken: null,
                remoteIp,
                customerExternalReference: "client_" + client.Id);

            await ApplyCreditCardOrderResponseAsync(payment, subscription, contract, orderResponse, amountToCharge, installments);
            return payment;
        }

        public async Task<Payment> ProcessPaymentWithSavedCardAsync(int paymentId, int creditCardId, int? installments = null, string? remoteIp = null, string? postalCode = null, string? addressNumber = null, bool addressHasNoNumber = false)
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
            var clientName = PaymentGatewayCustomer.ResolveName(client);
            var clientEmail = PaymentGatewayCustomer.ResolveEmail(client) ?? "";
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
                throw new InvalidOperationException("Email do cliente inválido. Informe um e-mail válido na tela de cadastro.");
            var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client);
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Informe um documento válido na tela de cadastro.");
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            var (holderPostalCode, holderAddressNumber) = await ResolveBillingForSavedCardAsync(card, postalCode, addressNumber, addressHasNoNumber);
            var clientAddress = PaymentGatewayCustomer.BuildCardHolderAddress(client, holderPostalCode, holderAddressNumber);
            var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                clientName, clientEmail, clientTaxId, clientPhone, clientAddress, "client_" + client.Id);
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }

            var orderResponse = await CreateAsaasCreditCardChargeAsync(
                payment,
                subscription,
                contract,
                amountToCharge,
                clientName,
                clientEmail,
                clientTaxId,
                clientPhone,
                clientAddress,
                card: null,
                creditCardToken: card.AsaasToken,
                asaasCustomerIdForToken: asaasCustomerId,
                remoteIp,
                customerExternalReference: "client_" + client.Id);

            await ApplyCreditCardOrderResponseAsync(payment, subscription, contract, orderResponse, amountToCharge, installments);
            return payment;
        }

        /// <summary>
        /// CardMonthly → assinatura Asaas MONTHLY; Card1x → assinatura Asaas YEARLY.
        /// Ambos usam POST /subscriptions (recorrência real), não POST /payments com installmentCount.
        /// </summary>
        private async Task<OrderResponse> CreateAsaasCreditCardChargeAsync(
            Payment payment,
            Subscription subscription,
            Contract contract,
            decimal amountToCharge,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone,
            PagBankAddress clientAddress,
            PagBankCard? card,
            string? creditCardToken,
            string? asaasCustomerIdForToken,
            string? remoteIp,
            string customerExternalReference)
        {
            var plan = contract.CardPlanType;
            var useAsaasSubscription = plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.Card1x;
            if (useAsaasSubscription)
            {
                var cycle = plan == ContractCardPlanType.CardMonthly
                    ? AsaasSubscriptionCycle.Monthly
                    : AsaasSubscriptionCycle.Yearly;
                return await _asaasService.CreateCreditCardSubscriptionAsync(
                    referenceId: payment.ExternalPaymentId,
                    amount: amountToCharge,
                    cycle: cycle,
                    clientName: clientName,
                    clientEmail: clientEmail,
                    clientTaxId: clientTaxId,
                    clientPhone: clientPhone,
                    card: card,
                    clientAddress: clientAddress,
                    creditCardToken: creditCardToken,
                    asaasCustomerIdForToken: asaasCustomerIdForToken,
                    remoteIp: remoteIp,
                    customerExternalReference: customerExternalReference);
            }

            return await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: amountToCharge,
                method: PaymentMethod.CreditCard,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                card: card,
                installments: null,
                clientAddress: clientAddress,
                creditCardToken: creditCardToken,
                asaasCustomerIdForToken: asaasCustomerIdForToken,
                remoteIp: remoteIp,
                customerExternalReference: customerExternalReference);
        }

        private async Task ApplyCreditCardOrderResponseAsync(
            Payment payment,
            Subscription subscription,
            Contract contract,
            OrderResponse orderResponse,
            decimal amountToCharge,
            int? installments)
        {
            payment.ExternalPaymentId = orderResponse.Id;
            payment.Status = PaymentStatus.Processing;
            if (!string.IsNullOrEmpty(orderResponse.SubscriptionId))
            {
                subscription.ExternalSubscriptionId = orderResponse.SubscriptionId;
                var isMonthly = contract.CardPlanType == ContractCardPlanType.CardMonthly;
                subscription.BillingCycle = isMonthly ? "Monthly" : "Yearly";
                subscription.MonthlyAmount = payment.Amount;
                subscription.LastBillingDate = DateTime.UtcNow;
                subscription.NextBillingDate = isMonthly
                    ? DateTime.UtcNow.AddMonths(1)
                    : DateTime.UtcNow.AddYears(1);
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);
                // Assinatura Asaas: não é parcelamento (installment).
                payment.InstallmentCount = null;
                payment.InstallmentValue = null;
            }
            else
            {
                var installmentCount = installments.HasValue && installments.Value > 1 ? installments.Value : (int?)null;
                payment.InstallmentCount = installmentCount;
                payment.InstallmentValue = installmentCount.HasValue
                    ? Math.Round(amountToCharge / installmentCount.Value, 2)
                    : null;
            }

            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                payment.Status = charge.Status.ToUpper() == "PAID" ? PaymentStatus.Paid :
                                 charge.Status.ToUpper() == "DECLINED" ? PaymentStatus.Failed :
                                 PaymentStatus.Processing;
                if (charge.PaidAt.HasValue)
                    payment.PaidAt = EnsurePaidAtWithTime(charge.PaidAt.Value);
                if (charge.PaymentMethod?.Card != null || charge.Status.ToUpper() == "PAID")
                    payment.TransactionId = charge.Id;
            }

            await _paymentRepository.UpdateAsync(payment);
            if (payment.Status == PaymentStatus.Paid)
                await ConfirmPaymentAsync(payment.Id, payment.TransactionId);
        }

        public async Task<CreditCardTokenizeResult> TokenizeCreditCardForClientAsync(
            int clientId,
            string cardNumber,
            string expiryMonth,
            string expiryYear,
            string cvv,
            string holderName,
            string? postalCode,
            string? addressNumber,
            bool addressHasNoNumber,
            string remoteIp,
            CancellationToken cancellationToken = default)
        {
            var (holderPostalCode, holderAddressNumber) = RequireCardHolderBilling(postalCode, addressNumber, addressHasNoNumber);
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");
            var clientName = PaymentGatewayCustomer.ResolveName(client);
            var clientEmail = PaymentGatewayCustomer.ResolveEmail(client);
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
                throw new InvalidOperationException("Email do cliente inválido. Informe um e-mail válido na tela de cadastro.");
            var clientTaxId = PaymentGatewayCustomer.ResolveTaxIdDigits(client);
            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Informe um documento válido na tela de cadastro.");
            var clientPhone = client.ContactPhone ?? client.User?.Phone;
            var clientAddress = PaymentGatewayCustomer.BuildCardHolderAddress(client, holderPostalCode, holderAddressNumber);
            var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                clientName, clientEmail, clientTaxId, clientPhone, clientAddress, "client_" + client.Id);
            var token = await _asaasService.TokenizeCreditCardAsync(
                asaasCustomerId,
                cardNumber,
                expiryMonth,
                expiryYear,
                cvv,
                holderName.Trim(),
                clientEmail,
                clientTaxId,
                holderPostalCode,
                holderAddressNumber,
                clientPhone,
                remoteIp);
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Não foi possível tokenizar o cartão. Verifique os dados e tente novamente.");
            return new CreditCardTokenizeResult
            {
                Token = token,
                PostalCode = holderPostalCode,
                AddressNumber = holderAddressNumber
            };
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

                    // Cartão (e alguns webhooks) podem marcar Paid antes de chamar Confirm.
                    // Nesse caso ainda precisamos ativar o contrato / comissão se estiverem pendentes.
                    var alreadyPaid = payment.Status == PaymentStatus.Paid;
                    if (alreadyPaid)
                    {
                        var healed = await HealAlreadyPaidPaymentSideEffectsAsync(payment, paymentId, transactionId);
                        if (healed)
                        {
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            if (_paymentRealtimeNotifier != null)
                                await _paymentRealtimeNotifier.NotifyPaymentConfirmedAsync(paymentId);
                        }
                        else
                        {
                            await transaction.CommitAsync();
                        }
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
                    await ApplySubscriptionAndContractOnPaidAsync(payment, paymentId);
                    if (payment.FiscalOnboardingLeadId.HasValue && _fiscalOnboardingService != null)
                    {
                        await _fiscalOnboardingService.HandleAdhesionPaymentConfirmedAsync(payment.Id, CancellationToken.None);
                    }
                    if (!payment.FiscalOnboardingLeadId.HasValue)
                    {
                        await _commissionService.CreateCommissionAsync(payment.Id);
                    }
                    if (payment.PromoCodeId.HasValue)
                    {
                        await _promoCodeService.TryRecordRedemptionForPaymentAsync(payment.Id, payment.PromoCodeId.Value);
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    if (_paymentRealtimeNotifier != null)
                        await _paymentRealtimeNotifier.NotifyPaymentConfirmedAsync(paymentId);
                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// Quando o pagamento já está Paid (ex.: charge de cartão setou Paid antes do Confirm),
        /// aplica efeitos colaterais faltantes: ativar contrato PendingPayment, histórico, comissão.
        /// Retorna true se algo foi alterado.
        /// </summary>
        private async Task<bool> HealAlreadyPaidPaymentSideEffectsAsync(Payment payment, int paymentId, string? transactionId)
        {
            var changed = false;
            if (!string.IsNullOrEmpty(transactionId) && string.IsNullOrEmpty(payment.TransactionId))
            {
                payment.TransactionId = transactionId;
                payment.UpdatedAt = DateTime.UtcNow;
                _context.Payments.Update(payment);
                changed = true;
            }

            var hasApprovedHistory = await _context.PaymentHistories
                .AnyAsync(h => h.PaymentId == payment.Id && h.Action == PaymentHistoryActions.Approved);
            if (!hasApprovedHistory)
            {
                int? userId = null;
                var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var parsedUserId))
                    userId = parsedUserId;
                _context.PaymentHistories.Add(new PaymentHistory
                {
                    PaymentId = payment.Id,
                    Action = PaymentHistoryActions.Approved,
                    ActionBy = userId,
                    ActionAt = DateTime.UtcNow
                });
                changed = true;
            }

            var contractActivatedOrUpdated = await ApplySubscriptionAndContractOnPaidAsync(payment, paymentId, onlyIfContractNeedsActivation: true);
            if (contractActivatedOrUpdated)
                changed = true;

            if (payment.FiscalOnboardingLeadId.HasValue && _fiscalOnboardingService != null)
            {
                await _fiscalOnboardingService.HandleAdhesionPaymentConfirmedAsync(payment.Id, CancellationToken.None);
            }
            else
            {
                var hasCommission = await _context.Commissions.AnyAsync(c => c.PaymentId == payment.Id);
                if (!hasCommission)
                {
                    await _commissionService.CreateCommissionAsync(payment.Id);
                    changed = true;
                }
            }

            if (payment.PromoCodeId.HasValue)
            {
                var hadPromoRedemption = await _context.PaymentHistories
                    .AnyAsync(h => h.PaymentId == payment.Id && h.Action == PaymentHistoryActions.PromoRedeemed);
                if (!hadPromoRedemption)
                {
                    await _promoCodeService.TryRecordRedemptionForPaymentAsync(payment.Id, payment.PromoCodeId.Value);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Atualiza assinatura e ativa/renova contrato após pagamento confirmado.
        /// Se <paramref name="onlyIfContractNeedsActivation"/>, só age quando o contrato ainda está PendingPayment/Suspended
        /// (evita estender ExpiryDate em reentradas idempotentes de webhook).
        /// </summary>
        private async Task<bool> ApplySubscriptionAndContractOnPaidAsync(Payment payment, int paymentId, bool onlyIfContractNeedsActivation = false)
        {
            if (!payment.SubscriptionId.HasValue)
                return false;

            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                return false;

            var contract = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);

            if (onlyIfContractNeedsActivation)
            {
                if (contract == null ||
                    (contract.Status != ContractStatus.PendingPayment && contract.Status != ContractStatus.Suspended))
                    return false;
            }

            subscription.LastBillingDate = DateTime.UtcNow;
            var contractForCycle = contract ?? await _context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
            subscription.NextBillingDate = contractForCycle?.CardPlanType.IsAnnualBillingCycle() == true
                ? subscription.LastBillingDate.Value.AddYears(1)
                : subscription.LastBillingDate.Value.AddMonths(1);
            subscription.FailedPaymentAttempts = 0;
            if (subscription.Status == SubscriptionStatus.PastDue || subscription.Status == SubscriptionStatus.Suspended)
            {
                subscription.Status = SubscriptionStatus.Active;
            }
            subscription.UpdatedAt = DateTime.UtcNow;
            _context.Subscriptions.Update(subscription);

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
                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
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
            else if (contract != null && contract.Status == ContractStatus.Active && !onlyIfContractNeedsActivation)
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

            return true;
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