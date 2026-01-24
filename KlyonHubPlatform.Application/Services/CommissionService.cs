using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
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
            string serviceCode;
            int? serviceId = null;

            CoworkingAddress? address = null;

            // Identificar o serviço e coworking baseado no tipo de pagamento
            Contract? contract = null;
            if (payment.SubscriptionId.HasValue && payment.Subscription?.Contract != null)
            {
                // Pagamento de Endereço Fiscal (Subscription -> Contract)
                contract = payment.Subscription.Contract;
                address = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == contract.CoworkingAddressId);

                if (address == null)
                    throw new InvalidOperationException("Endereço não encontrado");

                coworkingId = address.CoworkingId;
                serviceCode = FiscalAddressOffer;
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
                serviceCode = RoomReservation;
            }
            else
            {
                throw new InvalidOperationException("Pagamento não possui Subscription ou Reservation associada");
            }

            // Para contratos, usar valores congelados (snapshot) se disponíveis
            decimal commissionPercentage = 0;
            
            if (contract != null && contract.SnapshotCommissionPercentage > 0)
            {
                // Usar percentual congelado do contrato (garante imutabilidade)
                commissionPercentage = contract.SnapshotCommissionPercentage;
            }
            else
            {
                // Para reservas ou contratos antigos sem snapshot, buscar valores atuais
                // Buscar o ServiceId baseado no nome do serviço
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Code == serviceCode && s.IsActive);

                if (service != null)
                {
                    serviceId = service.Id;
                }

                // Buscar porcentagem do PartnerService baseado no ServiceId e CoworkingId
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
            // Buscar comissões que não foram transferidas
            var allPending = await _commissionRepository.GetPendingTransfersAsync(coworkingId);
            
            // Buscar transfers pendentes ou em processamento do mesmo coworking
            var pendingTransfers = await _context.Transfers
                .Where(t => t.CoworkingId == coworkingId && 
                           (t.Status == TransferStatus.Pending || t.Status == TransferStatus.Processing))
                .ToListAsync();
            
            // Extrair IDs de comissões que estão em transfers pendentes
            var commissionIdsInPendingTransfers = new HashSet<int>();
            foreach (var transfer in pendingTransfers)
            {
                if (!string.IsNullOrEmpty(transfer.Description) && transfer.Description.Contains("CommissionIds:"))
                {
                    var idsPart = transfer.Description.Split("CommissionIds:")[1];
                    if (!string.IsNullOrEmpty(idsPart))
                    {
                        var ids = idsPart.Split(',');
                        foreach (var idStr in ids)
                        {
                            if (int.TryParse(idStr.Trim(), out var commissionId))
                            {
                                commissionIdsInPendingTransfers.Add(commissionId);
                            }
                        }
                    }
                }
            }
            
            // Retornar apenas comissões que não estão em transfers pendentes
            return allPending.Where(c => !commissionIdsInPendingTransfers.Contains(c.Id)).ToList();
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

            // Armazenar IDs das comissões no Description para poder recuperá-las depois
            var commissionIdsStr = string.Join(",", commissionIds);
            transfer.Description = $"Repasse de {commissions.Count}";

            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync();

            // NÃO marcar comissões como transferidas aqui - apenas quando o repasse for aprovado e processado
            // As comissões serão marcadas como transferidas no ProcessTransferAsync quando o status for Completed

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

            // Agora que o repasse foi processado e está Completed, marcar as comissões como transferidas
            // Buscar IDs das comissões do Description
            var commissionIds = new List<int>();
            if (!string.IsNullOrEmpty(transfer.Description) && transfer.Description.Contains("CommissionIds:"))
            {
                var idsPart = transfer.Description.Split("CommissionIds:")[1];
                if (!string.IsNullOrEmpty(idsPart))
                {
                    var ids = idsPart.Split(',');
                    foreach (var idStr in ids)
                    {
                        if (int.TryParse(idStr.Trim(), out var commissionId))
                        {
                            commissionIds.Add(commissionId);
                        }
                    }
                }
            }

            // Se não encontrou IDs no Description, tentar encontrar comissões pela heurística (data e valor)
            // Isso é para casos antigos onde os IDs não foram armazenados
            if (commissionIds.Count == 0)
            {
                // Buscar comissões do mesmo coworking que não foram transferidas e foram criadas antes do Transfer
                // e que somam aproximadamente o valor do Transfer
                var candidateCommissions = await _context.Commissions
                    .Where(c => c.CoworkingId == transfer.CoworkingId && 
                               !c.IsTransferred &&
                               c.CreatedAt <= transfer.CreatedAt.AddHours(1)) // Criadas até 1 hora após o Transfer
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // Tentar encontrar combinação de comissões que soma o valor do Transfer
                // Como pode ser complexo, vamos usar uma abordagem simples: buscar comissões criadas no mesmo dia
                var sameDayCommissions = candidateCommissions
                    .Where(c => c.CreatedAt.Date == transfer.CreatedAt.Date)
                    .ToList();

                var totalSameDay = sameDayCommissions.Sum(c => c.Amount);
                // Se a soma das comissões do mesmo dia é próxima ao valor do Transfer, usar essas
                if (Math.Abs(totalSameDay - transfer.Amount) < 0.01m && sameDayCommissions.Count > 0)
                {
                    commissionIds = sameDayCommissions.Select(c => c.Id).ToList();
                }
                else
                {
                    // Se não encontrou, usar todas as comissões não transferidas que somam aproximadamente o valor
                    var totalAll = candidateCommissions.Sum(c => c.Amount);
                    if (Math.Abs(totalAll - transfer.Amount) < 0.01m && candidateCommissions.Count > 0)
                    {
                        commissionIds = candidateCommissions.Select(c => c.Id).ToList();
                    }
                }
            }

            // Marcar comissões como transferidas apenas quando o repasse for Completed
            foreach (var commissionId in commissionIds)
            {
                var commission = await _commissionRepository.GetByIdAsync(commissionId);
                if (commission != null && !commission.IsTransferred)
                {
                    commission.IsTransferred = true;
                    commission.TransferredAt = transfer.ProcessedAt ?? DateTime.UtcNow;
                    await _commissionRepository.UpdateAsync(commission);
                }
            }

            return transfer;
        }
    }
}
