using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
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
        private readonly ILogger<CommissionService> _logger;
        public CommissionService(
            ICommissionRepository commissionRepository,
            IPaymentRepository paymentRepository,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<CommissionService> logger)
        {
            _commissionRepository = commissionRepository;
            _paymentRepository = paymentRepository;
            _context = context;
            _configuration = configuration;
            _logger = logger;
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
            Contract? contract = null;
            if (payment.SubscriptionId.HasValue && payment.Subscription?.Contract != null)
            {
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
            decimal commissionPercentage = 0;
            if (contract != null && contract.SnapshotCommissionPercentage > 0)
            {
                commissionPercentage = contract.SnapshotCommissionPercentage;
            }
            else
            {
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Code == serviceCode && s.IsActive);
                if (service != null)
                {
                    serviceId = service.Id;
                }
                if (serviceId.HasValue)
                {
                    var partnerService = await _context.PartnerServices
                        .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId.Value);
                    if (partnerService != null && partnerService.Percentage > 0)
                    {
                        commissionPercentage = partnerService.Percentage;
                    }
                }
                if (commissionPercentage == 0)
                {
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
            var createdCommission = await _commissionRepository.CreateAsync(commission);
            try
            {
                await AddCommissionToMonthlySettlementAsync(createdCommission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar comissão {CommissionId} ao fechamento mensal", createdCommission.Id);
            }
            return createdCommission;
        }
        private async Task AddCommissionToMonthlySettlementAsync(Commission commission)
        {
            var paymentDate = commission.CreatedAt;
            var month = paymentDate.Month;
            var year = paymentDate.Year;
            var settlement = await _context.MonthlySettlements
                .FirstOrDefaultAsync(s => s.CoworkingId == commission.CoworkingId 
                    && s.ReferenceMonth == month 
                    && s.ReferenceYear == year);
            if (settlement == null)
            {
                settlement = new MonthlySettlement
                {
                    CoworkingId = commission.CoworkingId,
                    ReferenceMonth = month,
                    ReferenceYear = year,
                    Status = SettlementStatus.Open,
                    TotalAmount = 0,
                    TotalGrossAmount = 0,
                    TotalPlatformCommission = 0,
                    CommissionCount = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MonthlySettlements.Add(settlement);
                await _context.SaveChangesAsync();
            }
            var existingLink = await _context.SettlementCommissions
                .AnyAsync(sc => sc.CommissionId == commission.Id);
            if (existingLink || settlement.Status != SettlementStatus.Open)
            {
                return;
            }
            var partnerAmount = commission.TotalPaymentAmount - commission.Amount;
            var settlementCommission = new SettlementCommission
            {
                MonthlySettlementId = settlement.Id,
                CommissionId = commission.Id,
                Amount = partnerAmount,
                CreatedAt = DateTime.UtcNow
            };
            _context.SettlementCommissions.Add(settlementCommission);
            settlement.TotalAmount += partnerAmount;
            settlement.TotalGrossAmount += commission.TotalPaymentAmount;
            settlement.TotalPlatformCommission += commission.Amount;
            settlement.CommissionCount++;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Comissão {CommissionId} adicionada ao fechamento {SettlementId}. Valor parceiro: {Amount}", 
                commission.Id, settlement.Id, partnerAmount);
        }
        public async Task<List<Commission>> GetPendingCommissionsAsync(int coworkingId)
        {
            var allPending = await _commissionRepository.GetPendingTransfersAsync(coworkingId);
            var pendingTransfers = await _context.Transfers
                .Where(t => t.CoworkingId == coworkingId && 
                           (t.Status == TransferStatus.Pending || t.Status == TransferStatus.Processing || 
                            t.Status == TransferStatus.AwaitingInvoice))
                .ToListAsync();
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
                ExternalTransferId = Guid.NewGuid().ToString()
            };
            var includedIds = commissions.Select(c => c.Id).ToList();
            var commissionIdsStr = string.Join(",", includedIds);
            var count = commissions.Count;
            var comissaoLabel = count == 1 ? "1 comissão" : $"{count} comissões";
            transfer.Description = $"Repasse referente a {comissaoLabel}|CommissionIds:{commissionIdsStr}";
            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync();
            return transfer;
        }
        public async Task<Transfer> ApproveTransferAsync(int transferId)
        {
            var transfer = await _context.Transfers
                .Include(t => t.Coworking)
                .FirstOrDefaultAsync(t => t.Id == transferId);
            if (transfer == null)
                throw new InvalidOperationException("Transferência não encontrada");
            if (transfer.Status != TransferStatus.Pending)
                throw new InvalidOperationException("Apenas repasses com status Pendente podem ser aprovados.");
            var now = DateTime.UtcNow;
            var nextMonth = now.Month == 12 ? 1 : now.Month + 1;
            var nextYear = now.Month == 12 ? now.Year + 1 : now.Year;
            var lastDay = DateTime.DaysInMonth(nextYear, nextMonth);
            var scheduledDate = new DateTime(nextYear, nextMonth, lastDay, 23, 59, 59, DateTimeKind.Utc);
            transfer.Status = TransferStatus.AwaitingInvoice;
            transfer.ScheduledDate = scheduledDate;
            transfer.UpdatedAt = now;
            await _context.SaveChangesAsync();
            return transfer;
        }
        public async Task<Transfer> ProcessTransferAsync(int transferId)
        {
            var transfer = await _context.Transfers
                .Include(t => t.Coworking)
                .FirstOrDefaultAsync(t => t.Id == transferId);
            if (transfer == null)
                throw new InvalidOperationException("Transferência não encontrada");
            transfer.Status = TransferStatus.Processing;
            await _context.SaveChangesAsync();
            await Task.Delay(1000);
            transfer.Status = TransferStatus.Completed;
            transfer.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
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
            if (commissionIds.Count == 0)
            {
                var candidateCommissions = await _context.Commissions
                    .Where(c => c.CoworkingId == transfer.CoworkingId && 
                               !c.IsTransferred &&
                               c.CreatedAt <= transfer.CreatedAt.AddHours(1))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
                var sameDayCommissions = candidateCommissions
                    .Where(c => c.CreatedAt.Date == transfer.CreatedAt.Date)
                    .ToList();
                var totalSameDay = sameDayCommissions.Sum(c => c.Amount);
                if (Math.Abs(totalSameDay - transfer.Amount) < 0.01m && sameDayCommissions.Count > 0)
                {
                    commissionIds = sameDayCommissions.Select(c => c.Id).ToList();
                }
                else
                {
                    var totalAll = candidateCommissions.Sum(c => c.Amount);
                    if (Math.Abs(totalAll - transfer.Amount) < 0.01m && candidateCommissions.Count > 0)
                    {
                        commissionIds = candidateCommissions.Select(c => c.Id).ToList();
                    }
                }
            }
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