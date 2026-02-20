using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
namespace KlyonHubPlatform.Application.Services
{
    public class ClientDashboardService : IClientDashboardService
    {
        private readonly ApplicationDbContext _context;
        public ClientDashboardService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<ClientDashboardDetailsResult> GetDashboardDetailsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var client = await _context.Clients
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.CoworkingAddress)
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.Subscription)
                        .ThenInclude(s => s!.Payments)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            int reservationsCount = 0;
            if (client != null)
            {
                try
                {
                    reservationsCount = await _context.Reservations
                        .AsNoTracking()
                        .Where(r => r.ClientId == client.Id && r.Status == ReservationStatus.Confirmed)
                        .CountAsync(cancellationToken);
                }
                catch
                {
                }
            }
            bool hasFinancialPending = false;
            if (client?.Contracts != null)
            {
                var subscriptionIds = client.Contracts
                    .Where(c => c.Subscription != null)
                    .Select(c => c.Subscription!.Id)
                    .ToList();
                if (subscriptionIds.Any())
                {
                    hasFinancialPending = await _context.Payments
                        .AsNoTracking()
                        .Where(p => subscriptionIds.Contains(p.SubscriptionId ?? 0) &&
                                   (p.Status == PaymentStatus.Pending ||
                                    p.Status == PaymentStatus.Processing ||
                                    p.Status == PaymentStatus.AwaitingValidation))
                        .AnyAsync(cancellationToken);
                }
            }
            bool hasCertificateExpiring = false;
            if (client?.Contracts != null)
            {
                var today = DateTime.UtcNow.Date;
                var expiryThreshold = today.AddDays(30);
                hasCertificateExpiring = client.Contracts
                    .Any(c => c.Status == ContractStatus.Active &&
                             c.ExpiryDate.HasValue &&
                             c.ExpiryDate.Value.Date >= today &&
                             c.ExpiryDate.Value.Date <= expiryThreshold);
            }
            return new ClientDashboardDetailsResult(
                client,
                reservationsCount,
                hasFinancialPending,
                hasCertificateExpiring);
        }
    }
}