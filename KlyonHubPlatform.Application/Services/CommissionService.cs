using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class CommissionService : ICommissionService
    {
        private readonly ICommissionRepository _commissionRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public CommissionService(
            ICommissionRepository commissionRepository,
            IPaymentRepository paymentRepository,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _commissionRepository = commissionRepository;
            _paymentRepository = paymentRepository;
            _context = context;
            _configuration = configuration;
        }

        public async Task<Commission> CreateCommissionAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            var contract = payment.Subscription?.Contract;
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == contract.CoworkingAddressId);

            if (address == null)
                throw new InvalidOperationException("Endereço não encontrado");

            // Buscar comissão da localidade
            var locationCommission = await _context.LocationCommissions
                .Where(lc => lc.LocationId == address.LocationId && lc.IsActive)
                .OrderByDescending(lc => lc.EffectiveDate)
                .FirstOrDefaultAsync();

            var commissionPercentage = locationCommission?.CommissionPercentage ?? 
                _configuration.GetValue<decimal>("CommissionSettings:DefaultCommissionPercentage");

            var commissionAmount = payment.Amount * (commissionPercentage / 100);

            var commission = new Commission
            {
                PaymentId = paymentId,
                CoworkingId = address.CoworkingId,
                Amount = commissionAmount,
                Percentage = commissionPercentage,
                TotalPaymentAmount = payment.Amount
            };

            return await _commissionRepository.CreateAsync(commission);
        }

        public async Task<List<Commission>> GetPendingCommissionsAsync(int coworkingId)
        {
            return await _commissionRepository.GetPendingTransfersAsync(coworkingId);
        }

        public async Task<Transfer> CreateTransferAsync(int coworkingId, List<int> commissionIds)
        {
            var commissions = new List<Commission>();
            decimal totalAmount = 0;

            foreach (var commissionId in commissionIds)
            {
                var commission = await _commissionRepository.GetByIdAsync(commissionId);
                if (commission == null || commission.CoworkingId != coworkingId || commission.IsTransferred)
                    continue;

                commissions.Add(commission);
                totalAmount += commission.Amount;
            }

            if (commissions.Count == 0)
                throw new InvalidOperationException("Nenhuma comissão válida para transferência");

            var minimumAmount = _configuration.GetValue<decimal>("CommissionSettings:MinimumTransferAmount");
            if (totalAmount < minimumAmount)
                throw new InvalidOperationException($"Valor mínimo para transferência é R$ {minimumAmount:F2}");

            var transfer = new Transfer
            {
                CoworkingId = coworkingId,
                Amount = totalAmount,
                Status = TransferStatus.Pending,
                ScheduledDate = DateTime.UtcNow,
                Description = $"Repasse de {commissions.Count} comissões",
                ExternalTransferId = Guid.NewGuid().ToString()
            };

            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync();

            // Marcar comissões como transferidas
            foreach (var commission in commissions)
            {
                commission.IsTransferred = true;
                commission.TransferredAt = DateTime.UtcNow;
                await _commissionRepository.UpdateAsync(commission);
            }

            return transfer;
        }

        public async Task<Transfer> ProcessTransferAsync(int transferId)
        {
            var transfer = await _context.Transfers
                .Include(t => t.Coworking)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
                throw new InvalidOperationException("Transferência não encontrada");

            // TODO: Integrar com gateway de pagamento para realizar transferência real
            // Por enquanto, simula processamento
            transfer.Status = TransferStatus.Processing;
            await _context.SaveChangesAsync();

            // Simular processamento
            await Task.Delay(1000);

            transfer.Status = TransferStatus.Completed;
            transfer.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return transfer;
        }
    }
}
