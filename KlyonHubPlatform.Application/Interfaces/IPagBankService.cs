using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPagBankService
    {
        Task<OrderResponse> CreateOrderAsync(string referenceId, decimal amount, PaymentMethod method, 
            string clientName, string clientEmail, string clientTaxId, string? clientPhone = null,
            PagBankCard? card = null, int? installments = null);
        
        Task<OrderResponse> GetOrderAsync(string orderId);
        
        Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null);
        
        Task<bool> ValidateWebhookSignatureAsync(string payload, string signature);
    }
}
