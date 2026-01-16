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

        public async Task<Subscription?> GetByIdAsync(int id)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                    .ThenInclude(c => c.Client)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Subscription?> GetByContractIdAsync(int contractId)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.ContractId == contractId);
        }

        public async Task<Subscription> CreateAsync(Subscription subscription)
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task<Subscription> UpdateAsync(Subscription subscription)
        {
            subscription.UpdatedAt = DateTime.UtcNow;
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task<List<Subscription>> GetActiveSubscriptionsAsync()
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync();
        }

        public async Task<List<Subscription>> GetDueSubscriptionsAsync(DateTime date)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .Where(s => s.Status == SubscriptionStatus.Active && s.NextBillingDate <= date)
                .ToListAsync();
        }
    }
}
