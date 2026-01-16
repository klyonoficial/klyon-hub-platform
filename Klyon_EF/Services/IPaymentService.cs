using KlyonEF.Models;

namespace KlyonEF.Services
{
    public interface IPaymentService
    {
        Task<Payment> CreatePaymentAsync(int subscriptionId, decimal amount, PaymentMethod method);
        Task<Payment> ProcessPaymentAsync(int paymentId);
        Task<Payment> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, string? transactionId = null);
        Task<string> GeneratePixCodeAsync(int paymentId);
        Task<List<Payment>> GetPendingPaymentsAsync();
    }
}
