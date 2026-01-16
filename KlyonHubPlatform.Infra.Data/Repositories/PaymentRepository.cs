using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Payment> CreateAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> UpdateAsync(Payment payment)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync()
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                .ToListAsync();
        }

        public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Status == status)
                .ToListAsync();
        }
    }
}
