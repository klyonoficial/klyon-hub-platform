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
                    .ThenInclude(s => s!.Contract)
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
                .Include(p => p.Subscription!)
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
                .Include(p => p.Subscription!)
                .FirstOrDefaultAsync(p => p.ExternalPaymentId == externalPaymentId, cancellationToken);
        }
        public async Task<Payment?> FindPaymentByExternalIdOrReferenceAsync(string externalId, string? referenceId = null, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .FirstOrDefaultAsync(p => p.ExternalPaymentId == externalId, cancellationToken);
            if (payment != null)
                return payment;
            if (!string.IsNullOrEmpty(referenceId))
            {
                payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                    .FirstOrDefaultAsync(p => p.ExternalPaymentId == referenceId, cancellationToken);
                if (payment != null)
                    return payment;

                // externalReference do Asaas costuma ser o Id interno do Payment (ex.: "12345").
                if (int.TryParse(referenceId, out var localPaymentId))
                {
                    payment = await _context.Payments
                        .Include(p => p.Subscription)
                            .ThenInclude(s => s!.Contract)
                        .FirstOrDefaultAsync(p => p.Id == localPaymentId, cancellationToken);
                    if (payment != null)
                        return payment;
                }
            }
            return null;
        }
        public async Task<Payment?> GetByPixConciliationIdentifierAsync(string pixConciliationIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pixConciliationIdentifier)) return null;
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .FirstOrDefaultAsync(p => p.PixConciliationIdentifier == pixConciliationIdentifier, cancellationToken);
            if (payment != null)
                return payment;

            // PIX Automático: Asaas envia pixQrCodeId no webhook com externalReference null;
            // o identificador fica em Subscription.PixAutomaticConciliationIdentifier.
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .Where(p => p.Subscription != null
                    && p.Subscription.PixAutomaticConciliationIdentifier == pixConciliationIdentifier
                    && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing))
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}