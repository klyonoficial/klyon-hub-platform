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
            // Usar transação para garantir atomicidade - se houver erro, nada é criado
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verificar se o endereço está disponível
                var address = await _coworkingRepository.GetAddressByIdAsync(coworkingAddressId);
                if (address == null || !address.IsAvailable || address.CurrentContracts >= address.MaxContracts)
                {
                    throw new InvalidOperationException("Endereço não disponível para contrato");
                }

                // Verificar se já existe contrato ativo neste endereço
                var activeContract = await _contractRepository.GetActiveContractByAddressIdAsync(coworkingAddressId);
                if (activeContract != null)
                {
                    throw new InvalidOperationException("Já existe um contrato ativo neste endereço");
                }

                // Criar contrato inicialmente como Suspended (aguardando pagamento)
                var contract = new Contract
                {
                    ClientId = clientId,
                    CoworkingAddressId = coworkingAddressId,
                    ContractNumber = await GenerateContractNumberAsync(),
                    StartDate = DateTime.UtcNow,
                    MonthlyValue = monthlyValue,
                    Status = ContractStatus.Suspended // Inicia suspenso até pagamento
                };

                var createdContract = await _contractRepository.CreateAsync(contract);

                // Criar assinatura
                var subscription = await _subscriptionService.CreateSubscriptionAsync(
                    createdContract.Id, 
                    monthlyValue
                );

                // Criar primeiro pagamento (cobrança inicial)
                var initialPayment = await _paymentService.CreatePaymentAsync(
                    subscription.Id,
                    monthlyValue,
                    PaymentMethod.PIX // Método padrão, pode ser alterado depois
                );

                // NÃO gerar código PIX automaticamente - será gerado quando o usuário acessar a página de pagamento
                // Isso evita erros se o PagBank não estiver configurado ainda
                // O código PIX será gerado em PaymentDetails quando o usuário visualizar o pagamento

                // NÃO atualizar contador de contratos ainda - só após pagamento confirmado
                // address.CurrentContracts++;
                // if (address.CurrentContracts >= address.MaxContracts)
                // {
                //     address.IsAvailable = false;
                // }
                // await _coworkingRepository.UpdateAddressAsync(address);

                // Gerar documento do contrato (opcional - não deve impedir a criação do contrato)
                try
                {
                    await _documentService.GenerateContractDocumentAsync(createdContract.Id);
                }
                catch (Exception ex)
                {
                    // Log do erro mas não impede a criação do contrato
                    // O documento pode ser gerado posteriormente
                    // Em produção, usar um logger adequado
                    System.Diagnostics.Debug.WriteLine($"Erro ao gerar documento do contrato: {ex.Message}");
                }

                // Se tudo correu bem, confirmar a transação
                await transaction.CommitAsync();
                return createdContract;
            }
            catch
            {
                // Em caso de qualquer erro, reverter tudo
                await transaction.RollbackAsync();
                throw; // Re-lançar a exceção para que o controller possa tratá-la
            }
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
