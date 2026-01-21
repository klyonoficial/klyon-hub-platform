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

            var pctVal = await _config.GetValueAsync("Platform.CommissionPercentage");
            decimal pct = 20m;
            if (!string.IsNullOrEmpty(pctVal) && decimal.TryParse(pctVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                pct = parsed;

            var total = (decimal)r.CreditsUsed;
            var commissionAmount = Math.Round(total * (pct / 100m), 2, MidpointRounding.AwayFromZero);
            var coworkingAmount = Math.Round(total - commissionAmount, 2, MidpointRounding.AwayFromZero);

            _context.ReservationCommissions.Add(new ReservationCommission
            {
                ReservationId = r.Id,
                TotalCredits = total,
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

            var plat = await _context.PlatformCreditBalances.FirstOrDefaultAsync();
            if (plat != null)
            {
                plat.TotalCredits -= coworkingAmount;
                plat.UpdatedAt = DateTime.UtcNow;
            }

            r.Status = "Completed";
            r.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
