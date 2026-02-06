using System.Threading;
using System.Threading.Tasks;
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

        public async Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
            return payment;
        }

        public async Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync(cancellationToken);
            return payment;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                .FirstOrDefaultAsync(p => p.ExternalPaymentId == externalPaymentId, cancellationToken);
        }

        public async Task<Payment?> FindPaymentByExternalIdOrReferenceAsync(string externalId, string? referenceId = null, CancellationToken cancellationToken = default)
        {
            // Primeiro, tenta buscar pelo ExternalPaymentId exato (ID do pedido PagBank)
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                .FirstOrDefaultAsync(p => p.ExternalPaymentId == externalId, cancellationToken);

            if (payment != null)
                return payment;

            // Se não encontrou e temos um reference_id, buscar por ele
            // O reference_id pode ser o ExternalPaymentId original gerado antes de criar o pedido no PagBank
            if (!string.IsNullOrEmpty(referenceId))
            {
                payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                    .FirstOrDefaultAsync(p => p.ExternalPaymentId == referenceId, cancellationToken);

                if (payment != null)
                    return payment;
            }

            // Busca alternativa: procurar pagamentos pendentes recentes com valor similar
            // Isso é útil quando há inconsistência nos IDs
            // Por segurança, não fazemos essa busca automática - apenas retornamos null
            return null;
        }
    }
}
