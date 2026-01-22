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

            int coworkingId;
            string serviceName;
            int? serviceId = null;

            CoworkingAddress? address = null;

            // Identificar o serviço e coworking baseado no tipo de pagamento
            if (payment.SubscriptionId.HasValue && payment.Subscription?.Contract != null)
            {
                // Pagamento de Endereço Fiscal (Subscription -> Contract)
                var contract = payment.Subscription.Contract;
                address = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == contract.CoworkingAddressId);

                if (address == null)
                    throw new InvalidOperationException("Endereço não encontrado");

                coworkingId = address.CoworkingId;
                serviceName = "Contratar Endereço Fiscal";
            }
            else if (payment.ReservationId.HasValue && payment.Reservation != null)
            {
                // Pagamento de Reserva (Aluguel de Salas)
                var reservation = payment.Reservation;
                address = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == reservation.CoworkingAddressId);

                if (address == null)
                    throw new InvalidOperationException("Endereço não encontrado");

                coworkingId = address.CoworkingId;
                serviceName = "Reservar Sala";
            }
            else
            {
                throw new InvalidOperationException("Pagamento não possui Subscription ou Reservation associada");
            }

            // Buscar o ServiceId baseado no nome do serviço
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Name == serviceName && s.IsActive);

            if (service != null)
            {
                serviceId = service.Id;
            }

            // Buscar porcentagem do PartnerService baseado no ServiceId e CoworkingId
            decimal commissionPercentage = 0;
            
            if (serviceId.HasValue)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId.Value);

                if (partnerService != null && partnerService.Percentage > 0)
                {
                    commissionPercentage = partnerService.Percentage;
                }
            }

            // Se não encontrou porcentagem do PartnerService, usar fallback
            if (commissionPercentage == 0)
            {
                // Buscar comissão da localidade como fallback
                if (address?.LocationId != null)
                {
                    var locationCommission = await _context.LocationCommissions
                        .Where(lc => lc.LocationId == address.LocationId && lc.IsActive)
                        .OrderByDescending(lc => lc.EffectiveDate)
                        .FirstOrDefaultAsync();

                    commissionPercentage = locationCommission?.CommissionPercentage ?? 
                        _configuration.GetValue<decimal>("CommissionSettings:DefaultCommissionPercentage");
                }
                else
                {
                    commissionPercentage = _configuration.GetValue<decimal>("CommissionSettings:DefaultCommissionPercentage");
                }
            }

            var commissionAmount = payment.Amount * (commissionPercentage / 100);

            var commission = new Commission
            {
                PaymentId = paymentId,
                CoworkingId = coworkingId,
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
