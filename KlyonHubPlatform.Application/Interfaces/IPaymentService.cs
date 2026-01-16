using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Application.DTOs.PagBank;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPaymentService
    {
        Task<Payment> CreatePaymentAsync(int subscriptionId, decimal amount, PaymentMethod method);
        Task<Payment> ProcessPaymentAsync(int paymentId);
        Task<Payment> ConfirmPaymentAsync(int paymentId, string? transactionId = null);
        Task<Payment> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, string? transactionId = null);
        Task<Payment> UpdatePaymentMethodAsync(int paymentId, PaymentMethod method);
        Task<string> GeneratePixCodeAsync(int paymentId);
        Task<string> GenerateBoletoCodeAsync(int paymentId);
        Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetPaymentsByClientIdAsync(int clientId);
        Task<Payment?> GetPaymentByIdAsync(int paymentId);
    }
}
