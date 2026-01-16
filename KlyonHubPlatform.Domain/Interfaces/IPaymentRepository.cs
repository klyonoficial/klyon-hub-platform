using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id);
        Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId);
        Task<Payment> CreateAsync(Payment payment);
        Task<Payment> UpdateAsync(Payment payment);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status);
    }
}
