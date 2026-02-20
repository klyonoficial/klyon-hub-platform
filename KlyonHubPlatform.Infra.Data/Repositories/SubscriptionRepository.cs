using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly ApplicationDbContext _context;
        public SubscriptionRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Subscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                    .ThenInclude(c => c.Client)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }
        public async Task<Subscription?> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.ContractId == contractId, cancellationToken);
        }
        public async Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default)
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync(cancellationToken);
            return subscription;
        }
        public async Task<Subscription> UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
        {
            subscription.UpdatedAt = DateTime.UtcNow;
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
            return subscription;
        }
        public async Task<List<Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<Subscription>> GetDueSubscriptionsAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Where(s => s.Status == SubscriptionStatus.Active && s.NextBillingDate <= date)
                .ToListAsync(cancellationToken);
        }
    }
}