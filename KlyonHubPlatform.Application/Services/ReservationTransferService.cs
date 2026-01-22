using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ReservationTransferService : IReservationTransferService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISystemConfigurationService _config;

        public ReservationTransferService(ApplicationDbContext context, ISystemConfigurationService config)
        {
            _context = context;
            _config = config;
        }

        public async Task ExecuteTransferIfBothConfirmedAsync(Reservation reservation)
        {
            if (reservation.ClientConfirmedAt == null || reservation.CoworkingConfirmedAt == null)
                return;
            if (string.Equals(reservation.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return;

            var r = await _context.Reservations
                .Include(x => x.CoworkingAddress)
                .ThenInclude(a => a!.Coworking)
                .FirstOrDefaultAsync(x => x.Id == reservation.Id);
            if (r == null || r.CoworkingAddress?.Coworking == null) return;

            // Buscar porcentagem do PartnerService baseado no serviço "Reservar Sala"
            decimal pct = 20m; // Fallback padrão
            
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Name == "Reservar Sala" && s.IsActive);

            if (service != null && r.CoworkingAddress.CoworkingId > 0)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == r.CoworkingAddress.CoworkingId && ps.ServiceId == service.Id);
                
                if (partnerService != null && partnerService.Percentage > 0)
                {
                    pct = partnerService.Percentage;
                }
                else
                {
                    // Fallback: usar configuração do sistema
                    var pctVal = await _config.GetValueAsync("Platform.CommissionPercentage");
                    if (!string.IsNullOrEmpty(pctVal) && decimal.TryParse(pctVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        pct = parsed;
                }
            }
            else
            {
                // Fallback: usar configuração do sistema
                var pctVal = await _config.GetValueAsync("Platform.CommissionPercentage");
                if (!string.IsNullOrEmpty(pctVal) && decimal.TryParse(pctVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    pct = parsed;
            }

            var total = r.TotalValue;
            var commissionAmount = Math.Round(total * (pct / 100m), 2, MidpointRounding.AwayFromZero);
            var coworkingAmount = Math.Round(total - commissionAmount, 2, MidpointRounding.AwayFromZero);

            _context.ReservationCommissions.Add(new ReservationCommission
            {
                ReservationId = r.Id,
                TotalCredits = total, // Mantido para compatibilidade, mas agora representa valor em R$
                CommissionPercentage = pct,
                CommissionAmount = commissionAmount,
                CoworkingAmount = coworkingAmount
            });

            var transfer = new Transfer
            {
                CoworkingId = r.CoworkingAddress.CoworkingId,
                ReservationId = r.Id,
                Amount = coworkingAmount,
                Status = TransferStatus.Completed,
                Description = $"Reserva #{r.Id}",
                ScheduledDate = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };
            _context.Transfers.Add(transfer);

            // Atualizar saldo da plataforma (se a tabela existir)
            try
            {
                var plat = await _context.PlatformCreditBalances.FirstOrDefaultAsync();
                if (plat != null)
                {
                    plat.TotalCredits -= coworkingAmount;
                    plat.UpdatedAt = DateTime.UtcNow;
                }
            }
            catch (Exception)
            {
                // Tabela PlatformCreditBalances pode não existir ainda; ignorar e continuar
            }

            r.Status = "Completed";
            r.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
