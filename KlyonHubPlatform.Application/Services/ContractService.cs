using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Security.Claims;
namespace KlyonHubPlatform.Application.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        private readonly ICoworkingRepository _coworkingRepository;
        private readonly ApplicationDbContext _context;
        private readonly IDocumentService _documentService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IConfiguration _configuration;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        public ContractService(
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            ApplicationDbContext context,
            IDocumentService documentService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IAsaasService asaasService,
            IConfiguration configuration,
            IAuditService? auditService = null,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _context = context;
            _documentService = documentService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _asaasService = asaasService;
            _configuration = configuration;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
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
        private int? GetCurrentUserId()
        {
            if (_httpContextAccessor?.HttpContext?.User != null)
            {
                var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }
            }
            return null;
        }
        public async Task<Contract?> GetByIdAsync(int id)
        {
            return await _contractRepository.GetByIdAsync(id);
        }
        public async Task<List<Contract>> GetByClientIdAsync(int clientId)
        {
            return await _contractRepository.GetByClientIdAsync(clientId);
        }
        public async Task<Contract> CreateContractAsync(int clientId, int coworkingAddressId, decimal monthlyValue, int serviceId, int? assistedSaleAdminId = null, PaymentMethod? initialPaymentMethod = null, ContractCardPlanType? cardPlanType = null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var address = await _context.CoworkingAddresses
                        .Include(a => a.Location)
                        .Include(a => a.Coworking)
                        .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);
                    if (address == null)
                        throw new InvalidOperationException("Endereço não encontrado.");
                    if (!address.Coworking.IsActive)
                        throw new InvalidOperationException("O parceiro está suspenso. Não é possível criar novos contratos.");
                    if (address.AddressStatus != AddressStatus.Active)
                        throw new InvalidOperationException("O endereço não está ativo para novas vendas (suspenso ou removido). Contratos em andamento continuam válidos.");
                    if (!address.IsAvailable || address.CurrentContracts >= address.MaxContracts)
                        throw new InvalidOperationException("Endereço não disponível para contrato");
                    decimal commissionPercentage = await GetCommissionPercentageAsync(address);
                    decimal platformCommissionPercentage = commissionPercentage;
                    decimal coworkingCommissionPercentage = 100 - commissionPercentage;
                    var snapshotAddress = $"{address.Address}, {address.Number}" +
                        (!string.IsNullOrEmpty(address.Complement) ? $" - {address.Complement}" : "") +
                        $", {address.Neighborhood}, {address.City}/{address.State}, CEP: {address.ZipCode}";
                    var contract = new Contract
                    {
                        ClientId = clientId,
                        CoworkingAddressId = coworkingAddressId,
                        ServiceId = serviceId,
                        ContractNumber = await GenerateContractNumberAsync(),
                        StartDate = DateTime.UtcNow,
                        MonthlyValue = monthlyValue,
                        Status = ContractStatus.PendingPayment,
                        IsAssistedSale = assistedSaleAdminId.HasValue,
                        AssistedSaleAdminId = assistedSaleAdminId,
                        CardPlanType = cardPlanType,
                        SnapshotMonthlyValue = monthlyValue,
                        SnapshotPlatformCommissionPercentage = platformCommissionPercentage,
                        SnapshotCoworkingCommissionPercentage = coworkingCommissionPercentage,
                        SnapshotAddress = snapshotAddress,
                        SnapshotCommissionPercentage = commissionPercentage,
                        SnapshotCapacityConsumed = address.CurrentContracts
                    };
                    var createdContract = await _contractRepository.CreateAsync(contract);
                    var subscription = await _subscriptionService.CreateSubscriptionAsync(
                        createdContract.Id,
                        monthlyValue,
                        cardPlanType
                    );

                    var payment = await _paymentService.CreatePaymentAsync(
                        subscription.Id,
                        monthlyValue,
                        cardPlanType == ContractCardPlanType.PixAnnual ? PaymentMethod.PIX : (initialPaymentMethod ?? PaymentMethod.PIX)
                    );

                    //if (cardPlanType == ContractCardPlanType.PixAnnual)
                    //{
                    var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == createdContract.ClientId);
                    if (client != null)
                    {
                        var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? "";
                        var clientEmail = client.ContactEmail ?? client.User?.Email ?? "";
                        var clientTaxId = client.CPF ?? client.CNPJ ?? "";
                        var clientPhone = client.ContactPhone;
                        if (string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(clientTaxId))
                            throw new InvalidOperationException("Cliente sem nome, e-mail ou CPF/CNPJ. Complete o cadastro em Meu Perfil.");

                        if (cardPlanType == ContractCardPlanType.PixAnnual)
                        {


                            var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(clientName, clientEmail, clientTaxId, clientPhone, null, $"client_{client.Id}");
                            var annualValue = monthlyValue;
                            // contractId único (ContractNumber + Id) para evitar "Já existe autorização" ao reutilizar número
                            var asaasContractId = $"{createdContract.ContractNumber ?? ""}-{createdContract.Id}";
                            var authResponse = await _asaasService.CreatePixAutomaticAuthorizationAsync(
                                asaasCustomerId,
                                asaasContractId,
                                annualValue,
                                $"Contrato {createdContract.ContractNumber} - PIX anual",
                                86400
                            );
                            if (authResponse != null && !string.IsNullOrEmpty(authResponse.Id))
                            {
                                var payload = !string.IsNullOrEmpty(authResponse.ImmediateQrCode?.Payload) ? authResponse.ImmediateQrCode.Payload : authResponse.Payload;
                                if (string.IsNullOrEmpty(payload))
                                    throw new InvalidOperationException("Asaas PIX Automático retornou autorização sem payload do QR Code.");
                                var expiresAt = AsaasDateParser.TryParse(authResponse.ImmediateQrCode?.ExpirationDate) ?? DateTime.UtcNow.AddHours(24);

                                subscription.PixAutomaticAuthorizationId = authResponse.Id;
                                subscription.PixAutomaticPayload = payload;
                                subscription.PixAutomaticPayloadExpiresAt = expiresAt;
                                subscription.PixAutomaticConciliationIdentifier = authResponse.ImmediateQrCode?.ConciliationIdentifier;

                                _context.Subscriptions.Update(subscription);
                                await _context.SaveChangesAsync();
                            }
                            else
                                throw new InvalidOperationException(
                                    "Não foi possível criar a autorização PIX Automático. Verifique no painel Asaas se a funcionalidade PIX Automático está habilitada e se a API retornou erro (veja os logs da aplicação).");
                        }
                        else if (cardPlanType == ContractCardPlanType.None)
                        {
                            // PIX (pagamento único): criar cobrança via POST /payments e obter QR Code
                            var clientTaxIdNormalized = (client.CPF ?? client.CNPJ ?? "").Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                            if (string.IsNullOrWhiteSpace(clientTaxIdNormalized) || clientTaxIdNormalized.Length < 11)
                                throw new InvalidOperationException("CPF ou CNPJ do cliente inválido. Complete o cadastro em Meu Perfil.");
                            string GetVal(string? value, string fallback = "S/N") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
                            var regionCode = "SP";
                            if (!string.IsNullOrWhiteSpace(client.State))
                            {
                                var stateTrim = client.State.Trim().ToUpperInvariant();
                                if (stateTrim.Length >= 2 && BrazilianUfs.All.Contains(stateTrim.Substring(0, 2)))
                                    regionCode = stateTrim.Substring(0, 2);
                            }
                            var postalCode = "00000000";
                            if (!string.IsNullOrWhiteSpace(client.ZipCode))
                            {
                                var zip = client.ZipCode.Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                                if (zip.Length >= 8 && zip.All(char.IsDigit))
                                    postalCode = zip.Length > 8 ? zip.Substring(0, 8) : zip;
                                else if (zip.Length > 0 && zip.All(char.IsDigit))
                                    postalCode = zip.PadLeft(8, '0');
                            }
                            var clientAddress = new PagBankAddress
                            {
                                Street = GetVal(client.Address, DefaultDisplayStrings.NaoInformado),
                                Number = GetVal(client.AddressNumber, "S/N"),
                                Complement = null,
                                Locality = GetVal(client.Neighborhood, DefaultDisplayStrings.NaoInformado),
                                City = GetVal(client.City, DefaultDisplayStrings.NaoInformado),
                                RegionCode = regionCode,
                                Country = "BRA",
                                PostalCode = postalCode
                            };
                            var pixReferenceId = $"pix_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                            var orderResponse = await _asaasService.CreateOrderAsync(
                                referenceId: pixReferenceId,
                                amount: payment.Amount,
                                method: PaymentMethod.PIX,
                                clientName: clientName,
                                clientEmail: clientEmail,
                                clientTaxId: clientTaxIdNormalized,
                                clientPhone: clientPhone,
                                clientAddress: clientAddress,
                                customerExternalReference: $"client_{client.Id}"
                            );
                            payment.ExternalPaymentId = orderResponse.Id ?? "";
                            if (orderResponse.QrCodes != null && orderResponse.QrCodes.Any())
                            {
                                var qrCode = orderResponse.QrCodes.First();
                                payment.PixCode = qrCode.Text;
                                payment.PixExpiresAt = qrCode.ExpirationDate;
                                if (qrCode.ExpirationDate.HasValue)
                                    payment.DueDate = qrCode.ExpirationDate.Value;
                            }
                            _context.Payments.Update(payment);
                            await _context.SaveChangesAsync();
                        }
                    }

                    var service = await _context.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == serviceId);
                    if (service == null || !ServiceCodes.IsFiscalAddress(service.Code))
                    {
                        try
                        {
                            await _documentService.GenerateContractDocumentAsync(createdContract.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Erro ao gerar documento do contrato: {ex.Message}"
                            );
                        }
                    }
                    await transaction.CommitAsync();
                    if (!assistedSaleAdminId.HasValue)
                    {
                        await LogAuditAsync(
                            GetCurrentUserId(),
                            "Contrato criado",
                            "Contract",
                            createdContract.Id,
                            null,
                            new
                            {
                                ContractNumber = createdContract.ContractNumber,
                                ClientId = clientId,
                                CoworkingAddressId = coworkingAddressId,
                                MonthlyValue = monthlyValue,
                                Status = createdContract.Status.ToString(),
                                SnapshotMonthlyValue = createdContract.SnapshotMonthlyValue,
                                SnapshotPlatformCommissionPercentage = createdContract.SnapshotPlatformCommissionPercentage
                            }
                        );
                    }
                    return createdContract;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        public async Task<Contract> CancelContractAsync(int contractId, string reason)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var oldStatus = contract.Status;
            var oldCancelledAt = contract.CancelledAt;
            contract.Status = ContractStatus.Cancelled;
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancellationReason = reason;
            var address = await _coworkingRepository.GetAddressByIdAsync(contract.CoworkingAddressId);
            if (address != null)
            {
                address.CurrentContracts = Math.Max(0, address.CurrentContracts - 1);
                if (address.AddressStatus == AddressStatus.Active && address.CurrentContracts < address.MaxContracts)
                    address.IsAvailable = true;
                await _coworkingRepository.UpdateAddressAsync(address);
            }
            if (contract.Subscription != null)
            {
                contract.Subscription.Status = SubscriptionStatus.Cancelled;
                contract.Subscription.CancelledAt = DateTime.UtcNow;
                contract.Subscription.CancellationReason = reason;
            }
            var updatedContract = await _contractRepository.UpdateAsync(contract);
            await LogAuditAsync(
                GetCurrentUserId(),
                "Contrato cancelado",
                "Contract",
                contractId,
                new { Status = oldStatus.ToString(), CancelledAt = oldCancelledAt },
                new { Status = updatedContract.Status.ToString(), CancelledAt = updatedContract.CancelledAt, CancellationReason = reason, ContractNumber = updatedContract.ContractNumber }
            );
            return updatedContract;
        }
        public async Task<Contract> SuspendContractAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var oldStatus = contract.Status;
            contract.Status = ContractStatus.Suspended;
            if (contract.Subscription != null)
            {
                contract.Subscription.Status = SubscriptionStatus.Suspended;
                contract.Subscription.SuspendedAt = DateTime.UtcNow;
            }
            var updatedContract = await _contractRepository.UpdateAsync(contract);
            await LogAuditAsync(
                GetCurrentUserId(),
                "Contrato suspenso",
                "Contract",
                contractId,
                new { Status = oldStatus.ToString() },
                new { Status = updatedContract.Status.ToString(), ContractNumber = updatedContract.ContractNumber, SuspendedAt = contract.Subscription?.SuspendedAt }
            );
            return updatedContract;
        }
        public async Task<Contract> ActivateContractAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            contract.Status = ContractStatus.Active;
            if (contract.Subscription != null)
            {
                contract.Subscription.Status = SubscriptionStatus.Active;
                contract.Subscription.SuspendedAt = null;
            }
            return await _contractRepository.UpdateAsync(contract);
        }
        public async Task<string> GenerateContractNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var count = await _context.Contracts
                .Where(c => c.CreatedAt.Year == year)
                .CountAsync();
            return $"CT-{year}-{count + 1:D6}";
        }
        private async Task<decimal> GetCommissionPercentageAsync(CoworkingAddress address)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive);
            if (service != null)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == address.CoworkingId && ps.ServiceId == service.Id);
                if (partnerService != null && partnerService.Percentage > 0)
                {
                    return partnerService.Percentage;
                }
            }
            var locationCommission = await _context.LocationCommissions
                .Where(lc => lc.LocationId == address.LocationId && lc.IsActive)
                .OrderByDescending(lc => lc.EffectiveDate)
                .FirstOrDefaultAsync();
            if (locationCommission != null)
            {
                return locationCommission.CommissionPercentage;
            }
            return _configuration.GetValue<decimal>("CommissionSettings:DefaultCommissionPercentage");
        }
    }
}