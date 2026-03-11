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
        public async Task<Contract> CreateContractAsync(int clientId, int coworkingAddressId, decimal monthlyValue, int? assistedSaleAdminId = null, PaymentMethod? initialPaymentMethod = null, ContractCardPlanType? cardPlanType = null)
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
                    await _paymentService.CreatePaymentAsync(
                        subscription.Id,
                        monthlyValue,
                        initialPaymentMethod ?? PaymentMethod.PIX
                    );
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
                    await transaction.CommitAsync();
                    if (!assistedSaleAdminId.HasValue)
                    {
                        await LogAuditAsync(
                            GetCurrentUserId(),
                            "Contrato criado",
                            "Contract",
                            createdContract.Id,
                            null,
                            new { 
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