using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.Asaas;
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
            int? promoCodeId = null,
            string? snapshotBillingCycle = null)
        {
            DateTime dueDate;
            string? billingCycleSnapshot = snapshotBillingCycle;
            if (subscriptionId.HasValue)
            {
                var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId.Value);
                if (subscription == null)
                    throw new InvalidOperationException("Assinatura não encontrada");
                dueDate = subscription.NextBillingDate;
                if (string.IsNullOrWhiteSpace(billingCycleSnapshot))
                    billingCycleSnapshot = ResolveBillingCycleSnapshot(subscription);
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
                SnapshotBillingCycle = billingCycleSnapshot,
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
                            subscription.NextBillingDate = IsYearlyNextBilling(payment, subscription, contractForCycle)
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

            var isMonthlyCycle = PaymentNavigationHelper.IsMonthlyBillingCycle(payment)
                || BillingCycleConstants.IsMonthly(payment.SnapshotBillingCycle);
            var isYearlyCycle = PaymentNavigationHelper.IsYearlyBillingCycle(payment)
                || BillingCycleConstants.IsYearly(payment.SnapshotBillingCycle);
            var allowFailedMethodChange = payment.Status == PaymentStatus.Failed
                && (isMonthlyCycle || isYearlyCycle || PaymentNavigationHelper.IsFirstContractPayment(payment));
            if (payment.Status != PaymentStatus.Pending && !allowFailedMethodChange)
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

            var hadAsaasSubscription = IsAsaasManagedSubscriptionId(subscription?.ExternalSubscriptionId);
            var switchingToPixAutomatic = method == PaymentMethod.PIX && pixPlanType.IsPixAutomatic();
            var switchingToAsaasSubscriptionBilling = !switchingToPixAutomatic
                && (method == PaymentMethod.BankTransfer
                    || method == PaymentMethod.CreditCard
                    || (method == PaymentMethod.PIX && (pixPlanType == ContractCardPlanType.None || !pixPlanType.HasValue)));
            var updateAsaasBillingNow = switchingToAsaasSubscriptionBilling
                && method != PaymentMethod.CreditCard;

            // Troca de meio: parcela local Pending/Failed permanece pagável (não vira Cancelled).
            // Limpa IDs locais ANTES do DELETE remoto para webhooks (auth/pay_*) não cancelarem a parcela.
            // sub_* legada (PIX/Boleto assinatura): cancelar no Asaas ao migrar para PIX Automático.
            if (methodChanged || pixPlanChanged)
            {
            var willUpdateExistingAsaasSubscription = hadAsaasSubscription
                && switchingToAsaasSubscriptionBilling;
                var cancelContextMethod = previousMethod == PaymentMethod.PIX || pixPlanChanged
                    ? PaymentMethod.PIX
                    : previousMethod;

                // Snapshot dos IDs remotos antes da limpeza local (cenário acerto: cancelar pay_*/auth órfãos).
                var remotePayIdToCancel = !willUpdateExistingAsaasSubscription
                    ? payment.ExternalPaymentId?.Trim()
                    : null;
                var remoteAuthIdToCancel = (previousMethod == PaymentMethod.PIX || pixPlanChanged)
                    ? subscription?.PixAutomaticAuthorizationId?.Trim()
                    : null;

                if (!willUpdateExistingAsaasSubscription)
                    payment.ExternalPaymentId = string.Empty;

                if (previousMethod == PaymentMethod.PIX || pixPlanChanged)
                {
                    var keepExternalPayId = willUpdateExistingAsaasSubscription
                        ? payment.ExternalPaymentId
                        : null;
                    ClearLocalPixCheckoutArtifacts(payment, subscription);
                    if (!string.IsNullOrEmpty(keepExternalPayId))
                        payment.ExternalPaymentId = keepExternalPayId;
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

                // Persistência antecipada: webhook assíncrono vê ExternalPaymentId/auth já desvinculados.
                await _paymentRepository.UpdateAsync(payment);

                if (!willUpdateExistingAsaasSubscription)
                {
                    await TryCancelPendingAsaasChargeOnMethodChangeAsync(
                        payment,
                        subscription,
                        cancelContextMethod,
                        pixPlanChanged ? "troca de plano PIX" : null,
                        remotePayIdOverride: remotePayIdToCancel,
                        remoteAuthIdOverride: remoteAuthIdToCancel);
                }

                if (hadAsaasSubscription && switchingToPixAutomatic && subscription != null)
                {
                    await TryCancelAsaasSubscriptionAsync(subscription, "troca para PIX Automático");
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

            if (allowFailedMethodChange)
            {
                payment.Status = PaymentStatus.Pending;
                payment.FailureReason = null;
            }

            await _paymentRepository.UpdateAsync(payment);

            if (method == PaymentMethod.PIX && subscription != null && contract != null && pixPlanType.HasValue)
            {
                contract.CardPlanType = pixPlanType.Value;
                await _contractRepository.UpdateAsync(contract);
            }
            else if (method == PaymentMethod.BankTransfer && subscription != null && contract != null)
            {
                // Boleto via assinatura Asaas: não herdar CardMonthly/Pix* no contrato.
                if (contract.CardPlanType != ContractCardPlanType.None)
                {
                    contract.CardPlanType = ContractCardPlanType.None;
                    await _contractRepository.UpdateAsync(contract);
                }
            }

            // Assinatura Asaas já existente: trocar billingType/value/cycle (pendentes + próximas).
            // Cartão: UpdateSubscription ocorre em ProcessPaymentWithCreditCard (precisa dos dados do cartão).
            // Importante: também sincronizar quando só o ciclo/valor mudou (ex.: boleto anual→mensal),
            // senão o PUT fica com YEARLY/750 e a UI local com MONTHLY/85.
            if (hadAsaasSubscription
                && subscription != null
                && IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId)
                && updateAsaasBillingNow)
            {
                // Recarregar Amount/SnapshotBillingCycle após Sync do controller (ex.: boleto/PIX mensal 85).
                payment = await _paymentRepository.GetByIdAsync(paymentId) ?? payment;
                var billingType = method == PaymentMethod.BankTransfer
                    ? AsaasBillingType.Boleto
                    : AsaasBillingType.Pix;
                var cycle = ResolveSubscriptionCycleForAsaas(payment);
                var localCycle = cycle == AsaasSubscriptionCycle.Yearly
                    ? BillingCycleConstants.Yearly
                    : BillingCycleConstants.Monthly;
                var amountMismatch = Math.Abs(subscription.MonthlyAmount - payment.Amount) > 0.01m;
                var cycleMismatch = !string.Equals(subscription.BillingCycle, localCycle, StringComparison.OrdinalIgnoreCase);
                var shouldPutAsaas = methodChanged || pixPlanChanged || allowFailedMethodChange
                    || amountMismatch || cycleMismatch
                    || method == PaymentMethod.BankTransfer;

                if (shouldPutAsaas && payment.Amount > 0)
                {
                    try
                    {
                        _logger.LogInformation(
                            "PUT Asaas subscription {ExternalSubscriptionId}: billingType={BillingType}, value={Amount}, cycle={Cycle}, paymentId={PaymentId}, snapshotCycle={SnapshotCycle}",
                            subscription.ExternalSubscriptionId, billingType, payment.Amount, cycle, payment.Id, payment.SnapshotBillingCycle);

                        await _asaasService.UpdateSubscriptionAsync(
                            subscription.ExternalSubscriptionId!,
                            billingType: billingType,
                            value: payment.Amount,
                            cycle: cycle,
                            updatePendingPayments: true);
                        subscription.BillingCycle = localCycle;
                        subscription.MonthlyAmount = payment.Amount;
                        subscription.UpdatedAt = DateTime.UtcNow;
                        await _subscriptionRepository.UpdateAsync(subscription);

                        // updatePendingPayments às vezes NÃO converte pay_* PIX→BOLETO.
                        // Se ExternalPaymentId ainda é PIX, cancela e limpa para forçar nova cobrança BOLETO.
                        if (method == PaymentMethod.BankTransfer)
                            await TryAbandonStalePixChargeAfterBoletoSwitchAsync(payment);

                        _logger.LogInformation(
                            "Assinatura Asaas {ExternalSubscriptionId} atualizada para {BillingType}: value={Amount}, cycle={Cycle}, paymentId={PaymentId}",
                            subscription.ExternalSubscriptionId, billingType, payment.Amount, cycle, payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Falha ao atualizar billingType da assinatura Asaas {SubscriptionId} (paymentId={PaymentId}). Seguirá gerando cobrança localmente se necessário.",
                            subscription.ExternalSubscriptionId, payment.Id);
                    }
                }
            }

            // PIX: gerar código na mesma requisição ao trocar forma (único, assinatura ou automático); cancelamento já feito acima.
            if (method == PaymentMethod.PIX)
            {
                await GeneratePixCodeAsync(paymentId, pixPlanType, previousCardPlanType: null);
            }
            // Boleto: Method + UpdateSubscription já persistidos. Linha digitável é obtida no
            // PaymentDetails GET (polling ~15–20s + soft warning) para não travar o AJAX da troca.
            return await _paymentRepository.GetByIdAsync(paymentId) ?? payment;
        }

        private static bool IsAsaasManagedSubscriptionId(string? externalSubscriptionId) =>
            !string.IsNullOrWhiteSpace(externalSubscriptionId)
            && externalSubscriptionId.StartsWith("sub_", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Após PUT BOLETO, a cobrança pendente <c>pay_*</c> local pode continuar como PIX
        /// (updatePendingPayments não converte). Cancela e limpa ExternalPaymentId para
        /// GenerateBoletoCodeAsync aguardar/criar cobrança BOLETO real.
        /// </summary>
        private async Task TryAbandonStalePixChargeAfterBoletoSwitchAsync(Payment payment)
        {
            var payId = payment.ExternalPaymentId?.Trim();
            if (string.IsNullOrEmpty(payId) || !payId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var order = await _asaasService.GetOrderAsync(payId);
                var stillPix = order.QrCodes?.Any() == true
                    && (order.Charges == null || !order.Charges.Any(c =>
                        c.PaymentMethod?.Boleto != null
                        && (!string.IsNullOrEmpty(c.PaymentMethod.Boleto.FormattedBarcode)
                            || !string.IsNullOrEmpty(c.PaymentMethod.Boleto.Barcode))));
                if (!stillPix)
                {
                    // Já é boleto (com ou sem linha ainda): manter pay_* e sincronizar DueDate/artefatos.
                    var dueBefore = payment.DueDate;
                    ApplyBoletoArtifactsFromOrder(payment, order);
                    if (!string.IsNullOrEmpty(payment.BoletoCode)
                        || !string.IsNullOrEmpty(payment.BoletoBarcode)
                        || payment.DueDate.Date != dueBefore.Date)
                        await _paymentRepository.UpdateAsync(payment);
                    return;
                }

                _logger.LogWarning(
                    "Após troca para BOLETO, cobrança {ExternalPaymentId} ainda é PIX (paymentId={PaymentId}). Cancelando pay_* órfão.",
                    payId, payment.Id);
                try
                {
                    await _asaasService.CancelChargeAsync(payId);
                }
                catch (Exception cancelEx)
                {
                    _logger.LogWarning(cancelEx,
                        "Falha ao cancelar pay_* PIX {ExternalPaymentId} após troca para boleto (paymentId={PaymentId}).",
                        payId, payment.Id);
                }

                payment.ExternalPaymentId = string.Empty;
                payment.PixCode = null;
                payment.PixExpiresAt = null;
                payment.PixConciliationIdentifier = null;
                payment.BoletoCode = null;
                payment.BoletoBarcode = null;
                payment.BoletoExpiresAt = null;
                payment.BankSlipUrl = null;
                await _paymentRepository.UpdateAsync(payment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Não foi possível verificar se {ExternalPaymentId} ainda é PIX após BOLETO (paymentId={PaymentId}).",
                    payId, payment.Id);
            }
        }

        private static bool ShouldUseAsaasSubscriptionForPixOrBoleto(Payment payment, ContractCardPlanType? pixPlanType)
        {
            // PIX Automático: autorização, não assinatura Asaas.
            if (payment.Method == PaymentMethod.PIX && pixPlanType.IsPixAutomatic())
                return false;

            // PIX tradicional (None) e Boleto: sempre assinatura Asaas (MONTHLY ou YEARLY).
            if (payment.Method == PaymentMethod.BankTransfer)
                return true;

            if (payment.Method == PaymentMethod.PIX)
                return !pixPlanType.IsPixAutomatic();

            return false;
        }

        /// <summary>
        /// Alinha value/cycle da sub_* no Asaas ao Payment.Amount local (ex.: PIX mensal com Amount
        /// corrigido de SnapshotListPrice 750 → parcela 85). Atualiza cobranças pendentes.
        /// Retorna true se enviou UpdateSubscription ao Asaas.
        /// </summary>
        private async Task<bool> TrySyncAsaasSubscriptionAmountAsync(
            Subscription subscription,
            Payment payment,
            string billingType)
        {
            if (!IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
                return false;
            if (payment.Amount <= 0)
                return false;

            var cycle = ResolveSubscriptionCycleForAsaas(payment);
            var localCycle = cycle == AsaasSubscriptionCycle.Yearly
                ? BillingCycleConstants.Yearly
                : BillingCycleConstants.Monthly;
            var amountMismatch = Math.Abs(subscription.MonthlyAmount - payment.Amount) > 0.01m;
            var cycleMismatch = !string.Equals(subscription.BillingCycle, localCycle, StringComparison.OrdinalIgnoreCase);

            // Artefato de checkout ausente após Sync/heal/cupom: forçar PUT no Asaas mesmo se MonthlyAmount local
            // já foi corrigido (sub remota ainda pode estar com o valor/ciclo antigo).
            // Boleto: ExternalPaymentId vazio também força (pay_* invalidado após mudança de valor).
            var hasCheckoutArtifact = string.Equals(billingType, AsaasBillingType.Boleto, StringComparison.OrdinalIgnoreCase)
                ? !string.IsNullOrEmpty(payment.BoletoCode)
                    && !string.IsNullOrWhiteSpace(payment.ExternalPaymentId)
                : !string.IsNullOrEmpty(payment.PixCode);
            if (!amountMismatch && !cycleMismatch && hasCheckoutArtifact)
                return false;

            try
            {
                await _asaasService.UpdateSubscriptionAsync(
                    subscription.ExternalSubscriptionId!,
                    billingType: billingType,
                    value: payment.Amount,
                    cycle: cycle,
                    updatePendingPayments: true);
                subscription.MonthlyAmount = payment.Amount;
                subscription.BillingCycle = localCycle;
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);
                _logger.LogInformation(
                    "Assinatura Asaas {ExternalSubscriptionId} reconciliada: value={Amount}, cycle={Cycle}, paymentId={PaymentId}",
                    subscription.ExternalSubscriptionId, payment.Amount, cycle, payment.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao reconciliar valor da assinatura Asaas {ExternalSubscriptionId} (paymentId={PaymentId}, amount={Amount}).",
                    subscription.ExternalSubscriptionId, payment.Id, payment.Amount);
                return false;
            }
        }

        private static string ResolveSubscriptionCycleForAsaas(Payment payment)
        {
            // Preferir SnapshotBillingCycle (imutável após Sync); só então Subscription/helpers.
            if (BillingCycleConstants.IsMonthly(payment.SnapshotBillingCycle))
                return AsaasSubscriptionCycle.Monthly;
            if (BillingCycleConstants.IsYearly(payment.SnapshotBillingCycle))
                return AsaasSubscriptionCycle.Yearly;

            if (BillingCycleConstants.IsMonthly(payment.Subscription?.BillingCycle))
                return AsaasSubscriptionCycle.Monthly;
            if (BillingCycleConstants.IsYearly(payment.Subscription?.BillingCycle))
                return AsaasSubscriptionCycle.Yearly;

            if (PaymentNavigationHelper.IsMonthlyBillingCycle(payment))
                return AsaasSubscriptionCycle.Monthly;
            if (PaymentNavigationHelper.IsYearlyBillingCycle(payment))
                return AsaasSubscriptionCycle.Yearly;

            return AsaasSubscriptionCycle.Monthly;
        }

        /// <summary>
        /// Cancela assinatura <c>sub_*</c> no Asaas e desvincula ExternalSubscriptionId local.
        /// Usado ao migrar para PIX Automático ou recriar assinatura boleto/PIX tradicional.
        /// </summary>
        private async Task TryCancelAsaasSubscriptionAsync(Subscription subscription, string reason)
        {
            var subId = subscription.ExternalSubscriptionId?.Trim();
            if (!IsAsaasManagedSubscriptionId(subId))
                return;

            try
            {
                await _asaasService.CancelSubscriptionAsync(subId!);
                _logger.LogInformation(
                    "Assinatura Asaas {ExternalSubscriptionId} cancelada ({Reason}) subscriptionLocalId={SubscriptionId}",
                    subId, reason, subscription.Id);
                subscription.ExternalSubscriptionId = Guid.NewGuid().ToString();
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao cancelar assinatura Asaas {ExternalSubscriptionId} ({Reason}). Fluxo segue.",
                    subId, reason);
            }
        }

        private async Task PersistLocalAsaasSubscriptionAsync(
            Subscription subscription,
            string asaasSubscriptionId,
            decimal monthlyAmount,
            bool isMonthly)
        {
            subscription.ExternalSubscriptionId = asaasSubscriptionId;
            subscription.BillingCycle = isMonthly ? BillingCycleConstants.Monthly : BillingCycleConstants.Yearly;
            subscription.MonthlyAmount = monthlyAmount;
            subscription.LastBillingDate = DateTime.UtcNow;
            subscription.NextBillingDate = isMonthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1);
            subscription.UpdatedAt = DateTime.UtcNow;
            await _subscriptionRepository.UpdateAsync(subscription);
        }

        private static void ApplyPixArtifactsFromOrder(Payment payment, OrderResponse orderResponse)
        {
            payment.ExternalPaymentId = orderResponse.Id;
            if (orderResponse.QrCodes == null || !orderResponse.QrCodes.Any())
                return;
            var qrCode = orderResponse.QrCodes.First();
            payment.PixCode = qrCode.Text;
            payment.PixExpiresAt = qrCode.ExpirationDate;
            if (qrCode.ExpirationDate.HasValue)
                payment.DueDate = qrCode.ExpirationDate.Value;
        }

        private static void ApplyBoletoArtifactsFromOrder(Payment payment, OrderResponse orderResponse)
        {
            payment.ExternalPaymentId = orderResponse.Id;
            if (orderResponse.Charges == null || !orderResponse.Charges.Any())
                return;
            var charge = orderResponse.Charges.First();
            if (charge.PaymentMethod?.Boleto == null)
                return;
            var rawBoleto = charge.PaymentMethod.Boleto.FormattedBarcode ?? charge.PaymentMethod.Boleto.Barcode;
            var (barcode44, digitableFormatted) = BoletoHelper.NormalizeBoletoValues(rawBoleto);
            payment.BoletoBarcode = !string.IsNullOrEmpty(barcode44) ? barcode44 : rawBoleto;
            payment.BoletoCode = !string.IsNullOrEmpty(digitableFormatted) ? digitableFormatted : rawBoleto;
            var bankSlipLink = charge.PaymentMethod.Boleto.Links?.FirstOrDefault(l => string.Equals(l.Rel, "bankSlip", StringComparison.OrdinalIgnoreCase));
            payment.BankSlipUrl = !string.IsNullOrWhiteSpace(bankSlipLink?.Href) ? bankSlipLink.Href.Trim() : null;
            // dueDate Asaas é calendário (yyyy-MM-dd). Persistir Unspecified — Kind=Utc faz SaveChanges
            // converter para BRT e gravar o dia anterior (badge Vencido indevido).
            if (charge.PaymentMethod.Boleto.DueDate.HasValue)
            {
                var dueCalendar = BrazilDateTime.ToCalendarDate(charge.PaymentMethod.Boleto.DueDate.Value);
                payment.BoletoExpiresAt = dueCalendar;
                payment.DueDate = dueCalendar;
            }
        }

        /// <summary>
        /// Compara o valor da cobrança Asaas (centavos em <see cref="PagBankAmount.Value"/>) com o Amount local em BRL.
        /// Sem amount na resposta, considera match (não força cancelamento por mapeamento incompleto).
        /// </summary>
        private static bool OrderAmountMatchesPayment(OrderResponse order, decimal paymentAmountBrl)
        {
            var chargeAmount = order.Charges?.FirstOrDefault()?.Amount;
            if (chargeAmount == null || chargeAmount.Value <= 0)
                return true;

            var orderAmountBrl = chargeAmount.Value / 100m;
            return Math.Abs(orderAmountBrl - paymentAmountBrl) <= 0.01m;
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
        /// <paramref name="remotePayIdOverride"/> / <paramref name="remoteAuthIdOverride"/> permitem
        /// cancelar após limpeza local (evita race com webhook marcando Payment=Cancelled).
        /// Falha na API não bloqueia; apenas registra log. Não é chamado se Paid.
        /// Não cancela a parcela local — só recursos remotos órfãos.
        /// </summary>
        private async Task TryCancelPendingAsaasChargeOnMethodChangeAsync(
            Payment payment,
            Subscription? subscription,
            PaymentMethod previousMethod,
            string? reasonSuffix = null,
            string? remotePayIdOverride = null,
            string? remoteAuthIdOverride = null)
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

            var externalId = !string.IsNullOrWhiteSpace(remotePayIdOverride)
                ? remotePayIdOverride.Trim()
                : payment.ExternalPaymentId?.Trim();
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

            var authId = !string.IsNullOrWhiteSpace(remoteAuthIdOverride)
                ? remoteAuthIdOverride.Trim()
                : subscription?.PixAutomaticAuthorizationId?.Trim();
            if (string.IsNullOrEmpty(authId))
                return;

            try
            {
                await _asaasService.CancelPixAutomaticAuthorizationAsync(authId);
                _logger.LogInformation(
                    "Autorização PIX Automático {AuthorizationId} cancelada {Reason} (paymentId={PaymentId}); parcela local permanece pagável.",
                    authId, reasonLabel, payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao cancelar autorização PIX Automático {AuthorizationId} {Reason} (paymentId={PaymentId}). Fluxo segue; ids locais serão limpos.",
                    authId, reasonLabel, payment.Id);
            }
        }

        private static string BuildPixAutomaticBaseContractId(Contract contract)
        {
            var baseContractId = $"{contract.ContractNumber ?? ""}-{contract.Id}";
            if (baseContractId.Length > 35)
                baseContractId = baseContractId[..35];
            return baseContractId;
        }

        private async Task TryCancelStalePixAutomaticAuthorizationsForContractAsync(
            string asaasCustomerId,
            string baseContractId,
            string? exceptAuthorizationId = null)
        {
            try
            {
                await _asaasService.CancelPixAutomaticAuthorizationsForContractAsync(
                    asaasCustomerId, baseContractId, exceptAuthorizationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao cancelar autorizações PIX Automático stale para contractId={ContractId}.",
                    baseContractId);
            }
        }

        private async Task PersistPixAutomaticAuthorizationAsync(
            Subscription subscription,
            Payment payment,
            AsaasPixAutomaticAuthorizationResponse authResponse)
        {
            var expiresAt = AsaasDateParser.TryParse(authResponse.ImmediateQrCode?.ExpirationDate)
                ?? DateTime.UtcNow.AddHours(24);
            var payload = !string.IsNullOrEmpty(authResponse.ImmediateQrCode?.Payload)
                ? authResponse.ImmediateQrCode.Payload
                : authResponse.Payload;
            if (string.IsNullOrEmpty(payload))
                throw new InvalidOperationException("Asaas PIX Automático retornou autorização sem payload do QR Code. Verifique a resposta da API.");

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
            if (PaymentSubstitutionReasons.IsSubstitution(payment.FailureReason))
            {
                _logger.LogInformation(
                    "ReopenCancelledPixPayment ignorado: payment {PaymentId} cancelado por substituição ({Reason})",
                    paymentId, payment.FailureReason);
                return;
            }
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

            // Já existe parcela Paid na mesma assinatura: não gerar PIX (evita órfão + PUT sub de volta para PIX).
            if (payment.Status != PaymentStatus.Paid)
            {
                var paidSiblingId = await _context.Payments
                    .Where(p =>
                        p.SubscriptionId == payment.SubscriptionId
                        && p.Id != payment.Id
                        && p.Status == PaymentStatus.Paid)
                    .OrderByDescending(p => p.PaidAt ?? p.UpdatedAt)
                    .Select(p => (int?)p.Id)
                    .FirstOrDefaultAsync();
                if (paidSiblingId.HasValue)
                {
                    await DeleteOrphanPaymentLocallyAsync(payment, paidSiblingId.Value, cancelRemoteCharge: true);
                    await _context.SaveChangesAsync();
                    _logger.LogWarning(
                        "GeneratePixCode bloqueado: payment {PaymentId} excluído — assinatura {SubscriptionId} já tem Paid {PaidId}",
                        paymentId, payment.SubscriptionId, paidSiblingId.Value);
                    throw new InvalidOperationException(
                        "Esta cobrança foi substituída por um pagamento já confirmado. Atualize a lista de cobranças.");
                }
            }

            // Troca de plano PIX via GeneratePixCode (sem UpdatePaymentMethod): cancelar pay_*/auth remotos
            // após limpeza local — parcela permanece pagável (webhooks não devem Cancelled).
            if (pixPlanChanged)
            {
                var remotePayId = payment.ExternalPaymentId?.Trim();
                var remoteAuthId = subscription.PixAutomaticAuthorizationId?.Trim();
                ClearLocalPixCheckoutArtifacts(payment, subscription);
                await _subscriptionRepository.UpdateAsync(subscription);
                await _paymentRepository.UpdateAsync(payment);
                await TryCancelPendingAsaasChargeOnMethodChangeAsync(
                    payment,
                    subscription,
                    PaymentMethod.PIX,
                    "troca de plano PIX",
                    remotePayIdOverride: remotePayId,
                    remoteAuthIdOverride: remoteAuthId);
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
                    return subscription.PixAutomaticPayload ?? "";
                }
                var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                    clientName, clientEmail, clientTaxId, clientPhone, clientAddress, "client_" + client.Id);
                var baseContractId = BuildPixAutomaticBaseContractId(contract);

                // Cancelar autorizações stale (troca de plano/valor ou órfãs sem id local) antes de criar nova.
                await TryCancelStalePixAutomaticAuthorizationsForContractAsync(asaasCustomerId, baseContractId);

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
                        await PersistPixAutomaticAuthorizationAsync(subscription, payment, authResponse);
                        return subscription.PixAutomaticPayload ?? "";
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Já existe uma autorização", StringComparison.OrdinalIgnoreCase))
                {
                    // Conflito de contractId: cancelar autorizações existentes e tentar novamente com o id base.
                    await TryCancelStalePixAutomaticAuthorizationsForContractAsync(asaasCustomerId, baseContractId);
                    var retryAuth = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        baseContractId,
                        payment.Amount,
                        $"Contrato {contract.ContractNumber} - PIX {periodLabel}",
                        asaasFrequency,
                        86400);
                    if (retryAuth != null && !string.IsNullOrEmpty(retryAuth.Id))
                    {
                        await PersistPixAutomaticAuthorizationAsync(subscription, payment, retryAuth);
                        return subscription.PixAutomaticPayload ?? "";
                    }

                    // Último recurso: contractId com sufixo único (legado).
                    var uniqueContractId = $"{baseContractId}-{Guid.NewGuid().ToString("N")[..8]}";
                    if (uniqueContractId.Length > 35) uniqueContractId = uniqueContractId[..35];
                    var fallbackAuth = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                        asaasCustomerId,
                        uniqueContractId,
                        payment.Amount,
                        $"Contrato {contract.ContractNumber} - PIX {periodLabel}",
                        asaasFrequency,
                        86400);
                    if (fallbackAuth != null && !string.IsNullOrEmpty(fallbackAuth.Id))
                    {
                        await PersistPixAutomaticAuthorizationAsync(subscription, payment, fallbackAuth);
                        return subscription.PixAutomaticPayload ?? "";
                    }
                    throw new InvalidOperationException(
                        "Já existe uma autorização PIX Automático para este contrato. O código anterior pode ter expirado. Entre em contato com o suporte para gerar um novo código se necessário.");
                }
                throw new InvalidOperationException(
                    "Não foi possível criar a autorização PIX Automático. Verifique no painel Asaas se a funcionalidade está habilitada e os logs da aplicação.");
            }

            // PIX único ou PIX via Assinatura Asaas (mensal): não PIX Automático.
            // Reconciliar value no Asaas ANTES de reutilizar QR — evita cobrir R$ 750 em assinatura mensal
            // quando Payment.Amount já foi corrigido para a parcela (R$ 85).
            var useAsaasSubscription = ShouldUseAsaasSubscriptionForPixOrBoleto(payment, effectivePixPlan);
            if (useAsaasSubscription && IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
            {
                var asaasSynced = await TrySyncAsaasSubscriptionAmountAsync(subscription, payment, AsaasBillingType.Pix);
                if (asaasSynced)
                    payment.PixCode = null;
            }

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

            if (useAsaasSubscription && IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
            {
                // UpdateSubscription já deve ter ajustado a cobrança pendente; sem QR válido, não cria avulsa paralela.
                _logger.LogWarning(
                    "Assinatura Asaas {SubscriptionId} sem QR PIX válido para paymentId={PaymentId}. ExternalPaymentId={ExternalPaymentId}",
                    subscription.ExternalSubscriptionId, payment.Id, payment.ExternalPaymentId);
            }

            if (useAsaasSubscription && !IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
            {
                var pixReferenceId = $"pix_sub_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var pixCycle = ResolveSubscriptionCycleForAsaas(payment);
                var subscriptionOrder = await _asaasService.CreateSubscriptionAsync(
                    referenceId: pixReferenceId,
                    amount: payment.Amount,
                    billingType: AsaasBillingType.Pix,
                    cycle: pixCycle,
                    clientName: clientName,
                    clientEmail: clientEmail,
                    clientTaxId: clientTaxId,
                    clientPhone: clientPhone,
                    clientAddress: clientAddress,
                    customerExternalReference: "client_" + client.Id);
                if (!string.IsNullOrEmpty(subscriptionOrder.SubscriptionId))
                    await PersistLocalAsaasSubscriptionAsync(
                        subscription,
                        subscriptionOrder.SubscriptionId,
                        payment.Amount,
                        isMonthly: pixCycle == AsaasSubscriptionCycle.Monthly);
                ApplyPixArtifactsFromOrder(payment, subscriptionOrder);
                payment.Method = PaymentMethod.PIX;
                if (contract.CardPlanType.IsPixAutomatic() || contract.CardPlanType == ContractCardPlanType.CardMonthly
                    || contract.CardPlanType == ContractCardPlanType.Card1x)
                {
                    contract.CardPlanType = ContractCardPlanType.None;
                    await _contractRepository.UpdateAsync(contract);
                }
                await _paymentRepository.UpdateAsync(payment);
                return payment.PixCode ?? "";
            }

            // Fallback legado: só se ainda não for assinatura (não deveria ocorrer no checkout atual).
            // Nunca reutilizar pay_* existente como externalReference (ex.: pay de cartão pago) — cria PIX órfão no Asaas.
            var stalePayId = payment.ExternalPaymentId?.Trim();
            if (!string.IsNullOrEmpty(stalePayId) && stalePayId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _asaasService.CancelChargeAsync(stalePayId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao cancelar pay_* stale {ExternalPaymentId} antes de PIX avulso (paymentId={PaymentId})",
                        stalePayId, payment.Id);
                }
                payment.ExternalPaymentId = string.Empty;
            }
            var pixReferenceIdAvulso = $"pix_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: pixReferenceIdAvulso,
                amount: payment.Amount,
                method: PaymentMethod.PIX,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                clientAddress: clientAddress,
                customerExternalReference: "client_" + client.Id
            );
            ApplyPixArtifactsFromOrder(payment, orderResponse);
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

            payment.Method = PaymentMethod.BankTransfer;
            var useAsaasSubscription = ShouldUseAsaasSubscriptionForPixOrBoleto(payment, pixPlanType: null);

            if (useAsaasSubscription && IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
            {
                // Após UpdateSubscription BOLETO: Asaas pode demorar ~15–20s a emitir a linha digitável.
                const int boletoPollAttempts = 18;
                const int boletoPollDelayMs = 1000;

                // Cupom/recalc: reconciliar value na sub_* ANTES de reusar pay_* (evita boleto antigo sem desconto).
                await TrySyncAsaasSubscriptionAmountAsync(subscription, payment, AsaasBillingType.Boleto);

                // Reconsulta rápida do pay_* local. Se ainda for PIX (não convertido) ou valor defasado, abandonar.
                if (!string.IsNullOrEmpty(payment.ExternalPaymentId)
                    && payment.ExternalPaymentId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var existingOrder = await _asaasService.GetOrderAsync(payment.ExternalPaymentId);
                        var hasBoletoLine = existingOrder.Charges?.Any(c =>
                            c.PaymentMethod?.Boleto != null
                            && (!string.IsNullOrEmpty(c.PaymentMethod.Boleto.FormattedBarcode)
                                || !string.IsNullOrEmpty(c.PaymentMethod.Boleto.Barcode))) == true;
                        var stillPix = existingOrder.QrCodes?.Any() == true && !hasBoletoLine;
                        var orderAmountMatches = OrderAmountMatchesPayment(existingOrder, payment.Amount);

                        if (hasBoletoLine && orderAmountMatches)
                        {
                            ApplyBoletoArtifactsFromOrder(payment, existingOrder);
                            await _paymentRepository.UpdateAsync(payment);
                            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
                        }

                        if (hasBoletoLine && !orderAmountMatches)
                        {
                            // Cupom/recalc: UpdateSubscription já rodou — aguardar value propagar no mesmo pay_*.
                            // Não cancelar de imediato (pode deixar a sub_* sem cobrança pendente).
                            _logger.LogInformation(
                                "GenerateBoleto: pay_* {ExternalPaymentId} com valor defasado; aguardando UpdateSubscription (paymentId={PaymentId}, amount={Amount}).",
                                payment.ExternalPaymentId, payment.Id, payment.Amount);
                            var amountAligned = false;
                            for (var attempt = 1; attempt <= 8; attempt++)
                            {
                                await Task.Delay(boletoPollDelayMs);
                                try
                                {
                                    existingOrder = await _asaasService.GetOrderAsync(payment.ExternalPaymentId);
                                    if (!OrderAmountMatchesPayment(existingOrder, payment.Amount))
                                        continue;

                                    ApplyBoletoArtifactsFromOrder(payment, existingOrder);
                                    if (!string.IsNullOrEmpty(payment.BoletoCode) || !string.IsNullOrEmpty(payment.BoletoBarcode))
                                    {
                                        await _paymentRepository.UpdateAsync(payment);
                                        return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
                                    }

                                    amountAligned = true;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex,
                                        "Tentativa {Attempt}/8: falha ao reconsultar pay_* {ExternalPaymentId} após sync de valor.",
                                        attempt, payment.ExternalPaymentId);
                                }
                            }

                            if (!amountAligned)
                            {
                                _logger.LogWarning(
                                    "GenerateBoleto: pay_* {ExternalPaymentId} ainda defasado após sync; limpando vínculo local (paymentId={PaymentId}).",
                                    payment.ExternalPaymentId, payment.Id);
                                payment.ExternalPaymentId = string.Empty;
                                payment.BoletoCode = null;
                                payment.BoletoBarcode = null;
                                payment.BoletoExpiresAt = null;
                                payment.BankSlipUrl = null;
                                await _paymentRepository.UpdateAsync(payment);
                            }
                        }
                        else if (stillPix)
                        {
                            _logger.LogWarning(
                                "GenerateBoleto: pay_* {ExternalPaymentId} ainda é PIX (paymentId={PaymentId}). Cancelando e buscando cobrança BOLETO da sub.",
                                payment.ExternalPaymentId, payment.Id);
                            try { await _asaasService.CancelChargeAsync(payment.ExternalPaymentId); }
                            catch (Exception cancelEx)
                            {
                                _logger.LogWarning(cancelEx, "Falha ao cancelar pay_* PIX {Id} em GenerateBoleto.", payment.ExternalPaymentId);
                            }
                            payment.ExternalPaymentId = string.Empty;
                            payment.PixCode = null;
                            payment.PixExpiresAt = null;
                            await _paymentRepository.UpdateAsync(payment);
                        }
                        else
                        {
                            // BOLETO sem linha ainda (ou valor ainda propagando): polling curto neste pay_*.
                            for (var attempt = 1; attempt <= 5; attempt++)
                            {
                                try
                                {
                                    existingOrder = await _asaasService.GetOrderAsync(payment.ExternalPaymentId);
                                    if (!OrderAmountMatchesPayment(existingOrder, payment.Amount))
                                    {
                                        if (attempt < 5)
                                        {
                                            await Task.Delay(boletoPollDelayMs);
                                            continue;
                                        }

                                        _logger.LogWarning(
                                            "GenerateBoleto: pay_* {ExternalPaymentId} sem valor alinhado após polling (paymentId={PaymentId}, amount={Amount}). Limpando vínculo local.",
                                            payment.ExternalPaymentId, payment.Id, payment.Amount);
                                        payment.ExternalPaymentId = string.Empty;
                                        payment.BoletoCode = null;
                                        payment.BoletoBarcode = null;
                                        payment.BoletoExpiresAt = null;
                                        payment.BankSlipUrl = null;
                                        await _paymentRepository.UpdateAsync(payment);
                                        break;
                                    }

                                    ApplyBoletoArtifactsFromOrder(payment, existingOrder);
                                    if (!string.IsNullOrEmpty(payment.BoletoCode) || !string.IsNullOrEmpty(payment.BoletoBarcode))
                                    {
                                        await _paymentRepository.UpdateAsync(payment);
                                        return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex,
                                        "Tentativa {Attempt}/5: falha ao reconsultar boleto da cobrança {ExternalPaymentId}.",
                                        attempt, payment.ExternalPaymentId);
                                }

                                if (attempt < 5)
                                    await Task.Delay(boletoPollDelayMs);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Falha ao reconsultar cobrança {ExternalPaymentId} em GenerateBoleto (paymentId={PaymentId}).",
                            payment.ExternalPaymentId, payment.Id);
                    }
                }

                // Sem pay_* local ou boleto ainda sem linha digitável: aguardar cobrança BOLETO da sub_*.
                if (string.IsNullOrEmpty(payment.BoletoCode) && string.IsNullOrEmpty(payment.BoletoBarcode))
                {
                    var boletoOrder = await _asaasService.WaitForSubscriptionPaymentOrderAsync(
                        subscription.ExternalSubscriptionId!,
                        maxAttempts: boletoPollAttempts);
                    if (boletoOrder != null && OrderAmountMatchesPayment(boletoOrder, payment.Amount))
                    {
                        ApplyBoletoArtifactsFromOrder(payment, boletoOrder);
                        if (!string.IsNullOrEmpty(payment.BoletoCode) || !string.IsNullOrEmpty(payment.BoletoBarcode))
                        {
                            await _paymentRepository.UpdateAsync(payment);
                            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
                        }
                    }
                    else if (boletoOrder != null)
                    {
                        _logger.LogWarning(
                            "GenerateBoleto: cobrança da sub {SubscriptionId} com valor defasado (esperado {Amount}, paymentId={PaymentId}). Seguirá recreate se necessário.",
                            subscription.ExternalSubscriptionId, payment.Amount, payment.Id);
                    }
                }

                // Fallback controlado: DELETE+recreate assinatura BOLETO com value/cycle locais.
                if (string.IsNullOrEmpty(payment.BoletoCode) && string.IsNullOrEmpty(payment.BoletoBarcode)
                    && payment.Amount > 0)
                {
                    _logger.LogWarning(
                        "Boleto sem linha digitável após polling; recriando assinatura Asaas (paymentId={PaymentId}, amount={Amount}).",
                        payment.Id, payment.Amount);
                    try
                    {
                        await TryCancelAsaasSubscriptionAsync(subscription, "recreate boleto mensal/anual");
                        payment.ExternalPaymentId = string.Empty;
                        payment.BoletoCode = null;
                        payment.BoletoBarcode = null;
                        payment.BoletoExpiresAt = null;
                        payment.BankSlipUrl = null;

                        var boletoCycle = ResolveSubscriptionCycleForAsaas(payment);
                        var recreated = await _asaasService.CreateSubscriptionAsync(
                            referenceId: $"boleto_sub_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                            amount: payment.Amount,
                            billingType: AsaasBillingType.Boleto,
                            cycle: boletoCycle,
                            clientName: clientName,
                            clientEmail: clientEmail,
                            clientTaxId: clientTaxId,
                            clientPhone: clientPhone,
                            clientAddress: clientAddress,
                            customerExternalReference: "client_" + client.Id);
                        if (!string.IsNullOrEmpty(recreated.SubscriptionId))
                        {
                            await PersistLocalAsaasSubscriptionAsync(
                                subscription,
                                recreated.SubscriptionId,
                                payment.Amount,
                                isMonthly: boletoCycle == AsaasSubscriptionCycle.Monthly);
                        }
                        ApplyBoletoArtifactsFromOrder(payment, recreated);
                        if (!string.IsNullOrEmpty(payment.BoletoCode) || !string.IsNullOrEmpty(payment.BoletoBarcode))
                        {
                            await _paymentRepository.UpdateAsync(payment);
                            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Fallback recreate assinatura BOLETO falhou (paymentId={PaymentId}).",
                            payment.Id);
                    }
                }

                await _paymentRepository.UpdateAsync(payment);
                throw new InvalidOperationException(
                    "Método de pagamento é boleto, mas a linha digitável ainda não está disponível no Asaas. Atualize a página em alguns segundos.");
            }

            OrderResponse orderResponse;
            if (useAsaasSubscription)
            {
                var boletoCycle = ResolveSubscriptionCycleForAsaas(payment);
                orderResponse = await _asaasService.CreateSubscriptionAsync(
                    referenceId: string.IsNullOrEmpty(payment.ExternalPaymentId)
                        ? $"boleto_sub_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}"
                        : payment.ExternalPaymentId,
                    amount: payment.Amount,
                    billingType: AsaasBillingType.Boleto,
                    cycle: boletoCycle,
                    clientName: clientName,
                    clientEmail: clientEmail,
                    clientTaxId: clientTaxId,
                    clientPhone: clientPhone,
                    clientAddress: clientAddress,
                    customerExternalReference: "client_" + client.Id);
                if (!string.IsNullOrEmpty(orderResponse.SubscriptionId))
                    await PersistLocalAsaasSubscriptionAsync(
                        subscription,
                        orderResponse.SubscriptionId,
                        payment.Amount,
                        isMonthly: boletoCycle == AsaasSubscriptionCycle.Monthly);
                if (contract.CardPlanType != ContractCardPlanType.None)
                {
                    contract.CardPlanType = ContractCardPlanType.None;
                    await _contractRepository.UpdateAsync(contract);
                }
            }
            else
            {
                orderResponse = await _asaasService.CreateOrderAsync(
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
            }

            ApplyBoletoArtifactsFromOrder(payment, orderResponse);
            await _paymentRepository.UpdateAsync(payment);
            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
        }

        public async Task SyncBoletoDueDateFromAsaasAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                return;
            if (payment.Method != PaymentMethod.BankTransfer)
                return;
            var payId = payment.ExternalPaymentId?.Trim();
            if (string.IsNullOrEmpty(payId) || !payId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var order = await _asaasService.GetOrderAsync(payId);
                var dueBefore = payment.DueDate;
                var expBefore = payment.BoletoExpiresAt;
                ApplyBoletoArtifactsFromOrder(payment, order);
                var dueChanged = payment.DueDate.Date != dueBefore.Date;
                var expChanged = expBefore?.Date != payment.BoletoExpiresAt?.Date;
                if (dueChanged || expChanged)
                {
                    await _paymentRepository.UpdateAsync(payment);
                    if (dueChanged)
                    {
                        _logger.LogInformation(
                            "DueDate boleto sincronizado do Asaas: paymentId={PaymentId}, DueDate {Before:yyyy-MM-dd} → {After:yyyy-MM-dd}, ExternalPaymentId={ExternalPaymentId}",
                            payment.Id, dueBefore, payment.DueDate, payId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao sincronizar DueDate do boleto Asaas (paymentId={PaymentId}, ExternalPaymentId={ExternalPaymentId}).",
                    paymentId, payId);
            }
        }

        public async Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null, string? remoteIp = null, string? postalCode = null, string? addressNumber = null, bool addressHasNoNumber = false)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed)
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
            if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed)
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
        /// Se já existe sub_*, atualiza billingType/cartão com updatePendingPayments (não cria segunda assinatura).
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
            var useAsaasSubscription = plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.Card1x
                || PaymentNavigationHelper.IsMonthlyBillingCycle(payment);
            var cycle = plan == ContractCardPlanType.Card1x || PaymentNavigationHelper.IsYearlyBillingCycle(payment)
                ? AsaasSubscriptionCycle.Yearly
                : AsaasSubscriptionCycle.Monthly;
            if (plan == ContractCardPlanType.CardMonthly)
                cycle = AsaasSubscriptionCycle.Monthly;
            else if (plan == ContractCardPlanType.Card1x)
                cycle = AsaasSubscriptionCycle.Yearly;

            if (useAsaasSubscription && IsAsaasManagedSubscriptionId(subscription.ExternalSubscriptionId))
            {
                var cpfCnpj = new string((clientTaxId ?? "").Where(char.IsDigit).ToArray());
                var holder = new AsaasCreditCardHolderInfo
                {
                    Name = card?.Holder?.Name ?? clientName,
                    Email = clientEmail,
                    CpfCnpj = cpfCnpj,
                    PostalCode = new string((clientAddress.PostalCode ?? "").Where(char.IsDigit).ToArray()),
                    AddressNumber = clientAddress.Number?.Trim() ?? "",
                    Phone = clientPhone,
                    MobilePhone = clientPhone
                };
                // Troca billingType/cartão na assinatura (e nas cobranças pendentes).
                // O Asaas NÃO captura o cartão aqui se dueDate for futuro — fica PENDING até o vencimento.
                await _asaasService.UpdateSubscriptionAsync(
                    subscription.ExternalSubscriptionId!,
                    billingType: AsaasBillingType.CreditCard,
                    value: amountToCharge,
                    cycle: cycle,
                    updatePendingPayments: true,
                    card: card,
                    creditCardHolderInfo: holder,
                    creditCardToken: creditCardToken,
                    remoteIp: remoteIp);

                subscription.BillingCycle = cycle == AsaasSubscriptionCycle.Monthly
                    ? BillingCycleConstants.Monthly
                    : BillingCycleConstants.Yearly;
                subscription.MonthlyAmount = payment.Amount;
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);

                // Checkout "Pagar agora": captura imediata da cobrança pendente (mesmo com dueDate futuro).
                var pendingPayId = await _asaasService.ResolveSubscriptionPendingPaymentIdAsync(
                    subscription.ExternalSubscriptionId!,
                    payment.ExternalPaymentId);
                if (string.IsNullOrEmpty(pendingPayId))
                {
                    _logger.LogWarning(
                        "Assinatura {SubId} atualizada para CREDIT_CARD, mas nenhuma cobrança pendente encontrada (paymentLocalId={PaymentId}).",
                        subscription.ExternalSubscriptionId, payment.Id);
                    return new OrderResponse
                    {
                        Id = payment.ExternalPaymentId,
                        ReferenceId = payment.ExternalPaymentId,
                        Status = "PENDING",
                        CreatedAt = DateTime.UtcNow,
                        SubscriptionId = subscription.ExternalSubscriptionId
                    };
                }

                _logger.LogInformation(
                    "Capturando cobrança pendente após UpdateSubscription CREDIT_CARD: subId={SubId}, payId={PayId}, paymentLocalId={PaymentId}, localStatus={LocalStatus}.",
                    subscription.ExternalSubscriptionId, pendingPayId, payment.Id, payment.Status);

                var charged = await _asaasService.PayExistingPaymentWithCreditCardAsync(
                    pendingPayId,
                    card: card,
                    creditCardHolderInfo: holder,
                    creditCardToken: creditCardToken,
                    remoteIp: remoteIp);
                charged.SubscriptionId = subscription.ExternalSubscriptionId;

                _logger.LogInformation(
                    "Resultado captura cartão (sub existente): paymentLocalId={PaymentId}, asaasPayId={AsaasPayId}, gatewayStatus={GatewayStatus}, localStatusAntes={LocalStatus}.",
                    payment.Id, charged.Id, charged.Status, payment.Status);

                return charged;
            }

            if (useAsaasSubscription)
            {
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
                var chargeStatus = (charge.Status ?? "").Trim().ToUpperInvariant();
                payment.Status = IsCreditCardChargePaid(chargeStatus) ? PaymentStatus.Paid
                    : IsCreditCardChargeDeclined(chargeStatus) ? PaymentStatus.Failed
                    : PaymentStatus.Processing;
                if (charge.PaidAt.HasValue)
                    payment.PaidAt = EnsurePaidAtWithTime(charge.PaidAt.Value);
                if (charge.PaymentMethod?.Card != null || IsCreditCardChargePaid(chargeStatus))
                    payment.TransactionId = charge.Id;
            }
            else
            {
                // Sem charges: ainda assim respeitar Status agregado da OrderResponse (ex.: PAID/DECLINED).
                var orderStatus = (orderResponse.Status ?? "").Trim().ToUpperInvariant();
                if (IsCreditCardChargePaid(orderStatus))
                    payment.Status = PaymentStatus.Paid;
                else if (IsCreditCardChargeDeclined(orderStatus))
                    payment.Status = PaymentStatus.Failed;
            }

            await _paymentRepository.UpdateAsync(payment);

            var gatewayStatus = (orderResponse.Charges?.FirstOrDefault()?.Status
                ?? orderResponse.Status
                ?? "").Trim().ToUpperInvariant();

            // Cartão é síncrono: se não confirmou nem foi recusado de forma explícita (exceto análise de risco
            // ou cobrança ainda PENDING após update de assinatura), marca Failed para retentar.
            if (payment.Status == PaymentStatus.Processing)
            {
                if (openStatusIsAwaitingOrPending(gatewayStatus) == false
                    && gatewayStatus != AsaasPaymentStatus.AwaitingRiskAnalysis)
                {
                    payment.Status = PaymentStatus.Failed;
                    await _paymentRepository.UpdateAsync(payment);
                    _logger.LogWarning(
                        "Cobrança com cartão sem confirmação síncrona. PaymentId={PaymentId}, GatewayStatus={GatewayStatus}, ExternalPaymentId={ExternalPaymentId}. Marcado como Failed.",
                        payment.Id, gatewayStatus, payment.ExternalPaymentId);
                }
                else if (openStatusIsAwaitingOrPending(gatewayStatus))
                {
                    payment.Status = PaymentStatus.Pending;
                    await _paymentRepository.UpdateAsync(payment);
                }
            }

            _logger.LogInformation(
                "ApplyCreditCardOrderResponse: PaymentId={PaymentId}, GatewayStatus={GatewayStatus}, LocalStatus={LocalStatus}, ExternalPaymentId={ExternalPaymentId}, SubscriptionId={SubscriptionId}.",
                payment.Id, gatewayStatus, payment.Status, payment.ExternalPaymentId, orderResponse.SubscriptionId);

            if (payment.Status == PaymentStatus.Paid)
                await ConfirmPaymentAsync(payment.Id, payment.TransactionId);

            static bool openStatusIsAwaitingOrPending(string openStatus) =>
                openStatus == AsaasPaymentStatus.Pending
                || openStatus == AsaasPaymentStatus.Overdue
                || string.Equals(openStatus, "PENDING", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCreditCardChargePaid(string statusUpper) =>
            statusUpper is "PAID" or "CONFIRMED" or "RECEIVED";

        private static bool IsCreditCardChargeDeclined(string statusUpper) =>
            statusUpper is "DECLINED" or "FAILED" or "REFUSED" or "REFUNDED" or "INACTIVE" or "DELETED";

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
                throw new InvalidOperationException(AsaasCreditCardErrorMapper.CardValidationFailed);
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
                        await CancelSiblingPendingPaymentsOnPaidAsync(payment);
                        if (healed)
                        {
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            if (_paymentRealtimeNotifier != null)
                                await _paymentRealtimeNotifier.NotifyPaymentConfirmedAsync(paymentId);
                        }
                        else
                        {
                            await _context.SaveChangesAsync();
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
                    await CancelSiblingPendingPaymentsOnPaidAsync(payment);
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

            await CancelSiblingPendingPaymentsOnPaidAsync(payment);

            return changed;
        }

        /// <summary>
        /// Após um pagamento ficar Paid: cancela no Asaas e exclui localmente as outras cobranças
        /// Pending/Failed/Processing da mesma assinatura (órfãos de troca de meio — espelha Asaas).
        /// Não altera o payment que acabou de ser pago.
        /// </summary>
        private async Task CancelSiblingPendingPaymentsOnPaidAsync(Payment paidPayment)
        {
            if (paidPayment.Status != PaymentStatus.Paid || !paidPayment.SubscriptionId.HasValue)
                return;

            var siblings = await _context.Payments
                .Where(p =>
                    p.SubscriptionId == paidPayment.SubscriptionId
                    && p.Id != paidPayment.Id
                    && p.Status != PaymentStatus.Paid
                    && (p.Status == PaymentStatus.Pending
                        || p.Status == PaymentStatus.Failed
                        || p.Status == PaymentStatus.Processing))
                .ToListAsync();

            foreach (var sibling in siblings)
            {
                await DeleteOrphanPaymentLocallyAsync(sibling, paidPayment.Id, cancelRemoteCharge: true);
            }
        }

        /// <summary>
        /// Exclui cobrança órfã local (nunca Paid). PaymentHistories em cascade; Commissions só existem em Paid.
        /// </summary>
        private async Task DeleteOrphanPaymentLocallyAsync(
            Payment orphan,
            int paidSiblingId,
            bool cancelRemoteCharge)
        {
            if (orphan.Status == PaymentStatus.Paid)
                return;

            var remotePayId = orphan.ExternalPaymentId?.Trim();
            if (cancelRemoteCharge
                && !string.IsNullOrEmpty(remotePayId)
                && remotePayId.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _asaasService.CancelChargeAsync(remotePayId);
                    _logger.LogInformation(
                        "Cobrança Asaas {ExternalPaymentId} cancelada (órfã após Paid {PaidPaymentId}; local {OrphanId})",
                        remotePayId, paidSiblingId, orphan.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao cancelar cobrança Asaas {ExternalPaymentId} órfã (local {OrphanId} após Paid {PaidPaymentId}); excluindo localmente mesmo assim.",
                        remotePayId, orphan.Id, paidSiblingId);
                }
            }

            _context.Payments.Remove(orphan);
            _logger.LogInformation(
                "Pagamento {OrphanId} excluído (substituído pelo Paid {PaidPaymentId} na assinatura {SubscriptionId})",
                orphan.Id, paidSiblingId, orphan.SubscriptionId);
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
            subscription.NextBillingDate = IsYearlyNextBilling(payment, subscription, contractForCycle)
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
            var payments = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ClientId == clientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return payments;
        }
        public async Task<Payment?> GetPaymentByIdAsync(int paymentId)
        {
            return await _paymentRepository.GetByIdAsync(paymentId);
        }

        private static string? ResolveBillingCycleSnapshot(Subscription subscription)
        {
            if (BillingCycleConstants.IsMonthly(subscription.BillingCycle))
                return BillingCycleConstants.Monthly;
            if (BillingCycleConstants.IsYearly(subscription.BillingCycle))
                return BillingCycleConstants.Yearly;

            var plan = subscription.Contract?.CardPlanType;
            if (plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly)
                return BillingCycleConstants.Monthly;
            if (plan?.IsAnnualBillingCycle() == true)
                return BillingCycleConstants.Yearly;

            return null;
        }

        /// <summary>
        /// Próximo ciclo após pagamento: SnapshotBillingCycle do pago → CardPlanType → BillingCycle.
        /// Evita +1 mês quando o cliente pagou cartão/PIX anual e a sub Asaas ainda aponta MONTHLY.
        /// </summary>
        private static bool IsYearlyNextBilling(Payment payment, Subscription subscription, Contract? contract)
        {
            if (BillingCycleConstants.IsYearly(payment.SnapshotBillingCycle))
                return true;
            if (BillingCycleConstants.IsMonthly(payment.SnapshotBillingCycle))
                return false;

            var plan = contract?.CardPlanType;
            if (plan.IsAnnualBillingCycle())
                return true;
            if (plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly)
                return false;

            if (BillingCycleConstants.IsYearly(subscription.BillingCycle))
                return true;
            if (BillingCycleConstants.IsMonthly(subscription.BillingCycle))
                return false;

            return false;
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