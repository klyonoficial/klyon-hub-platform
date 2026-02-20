using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPagBankService
    {
        Task<OrderResponse> CreateOrderAsync(string referenceId, decimal amount, PaymentMethod method, 
            string clientName, string clientEmail, string clientTaxId, string? clientPhone = null,
            PagBankCard? card = null, int? installments = null, PagBankAddress? clientAddress = null);
        Task<OrderResponse> GetOrderAsync(string orderId);
        Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null);
        Task<bool> ValidateWebhookSignatureAsync(string payload, string signature);
        Task<PixTransferResult> CreatePixTransferAsync(string referenceId, decimal amount, string pixKey, 
            string pixKeyType, string receiverName, string? description = null);
        Task<PixTransferResult> GetPixTransferAsync(string transferId);
    }
    public class PixTransferResult
    {
        public bool Success { get; set; }
        public string? TransferId { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal Amount { get; set; }
        public string? EndToEndId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}