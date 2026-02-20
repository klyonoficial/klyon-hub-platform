using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);
        Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
        Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId, CancellationToken cancellationToken = default);
        Task<Payment?> FindPaymentByExternalIdOrReferenceAsync(string externalId, string? referenceId = null, CancellationToken cancellationToken = default);
    }
}