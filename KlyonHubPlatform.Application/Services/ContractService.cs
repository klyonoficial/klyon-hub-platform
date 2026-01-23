using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

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

        public ContractService(
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            ApplicationDbContext context,
            IDocumentService documentService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService)
        {
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _context = context;
            _documentService = documentService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
        }

        public async Task<Contract?> GetByIdAsync(int id)
        {
            return await _contractRepository.GetByIdAsync(id);
        }

        public async Task<List<Contract>> GetByClientIdAsync(int clientId)
        {
            return await _contractRepository.GetByClientIdAsync(clientId);
        }

        public async Task<Contract> CreateContractAsync(int clientId, int coworkingAddressId, decimal monthlyValue)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var address = await _coworkingRepository.GetAddressByIdAsync(coworkingAddressId);
                    if (address == null || !address.IsAvailable || address.CurrentContracts >= address.MaxContracts)
                    {
                        throw new InvalidOperationException("Endereço não disponível para contrato");
                    }

                    var contract = new Contract
                    {
                        ClientId = clientId,
                        CoworkingAddressId = coworkingAddressId,
                        ContractNumber = await GenerateContractNumberAsync(),
                        StartDate = DateTime.UtcNow,
                        MonthlyValue = monthlyValue,
                        Status = ContractStatus.PendingPayment
                    };

                    var createdContract = await _contractRepository.CreateAsync(contract);

                    var subscription = await _subscriptionService.CreateSubscriptionAsync(
                        createdContract.Id,
                        monthlyValue
                    );

                    await _paymentService.CreatePaymentAsync(
                        subscription.Id,
                        monthlyValue,
                        PaymentMethod.PIX
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

            contract.Status = ContractStatus.Cancelled;
            contract.CancelledAt = DateTime.UtcNow;
            contract.CancellationReason = reason;

            // Liberar endereço
            var address = await _coworkingRepository.GetAddressByIdAsync(contract.CoworkingAddressId);
            if (address != null)
            {
                address.CurrentContracts = Math.Max(0, address.CurrentContracts - 1);
                address.IsAvailable = true;
                await _coworkingRepository.UpdateAddressAsync(address);
            }

            // Cancelar assinatura se existir
            if (contract.Subscription != null)
            {
                contract.Subscription.Status = SubscriptionStatus.Cancelled;
                contract.Subscription.CancelledAt = DateTime.UtcNow;
                contract.Subscription.CancellationReason = reason;
            }

            return await _contractRepository.UpdateAsync(contract);
        }

        public async Task<Contract> SuspendContractAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            contract.Status = ContractStatus.Suspended;
            if (contract.Subscription != null)
            {
                contract.Subscription.Status = SubscriptionStatus.Suspended;
                contract.Subscription.SuspendedAt = DateTime.UtcNow;
            }

            return await _contractRepository.UpdateAsync(contract);
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
    }
}
