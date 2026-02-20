using KlyonHubPlatform.Application.DTOs.Asaas;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAsaasService
    {
        AsaasValidationResult ValidateCustomerDataForPayment(
            string? clientName,
            string? clientEmail,
            string? clientTaxId,
            string? clientPhone,
            decimal value,
            string? dueDate = null);
        Task<(bool Success, int StatusCode, string Message)> TestCreatePaymentAsync();
        Task<(bool Success, int StatusCode, string Message)> TestCreateCreditCardPaymentAsync();
        Task<OrderResponse> CreateOrderAsync(
            string referenceId,
            decimal amount,
            PaymentMethod method,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankCard? card = null,
            int? installments = null,
            PagBankAddress? clientAddress = null);
        Task<OrderResponse> GetOrderAsync(string orderId);
        Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null);
        Task<bool> ValidateWebhookSignatureAsync(string payload, string signature);
        Task<PixTransferResult> CreatePixTransferAsync(
            string referenceId,
            decimal amount,
            string pixKey,
            string pixKeyType,
            string receiverName,
            string? description = null);
        Task<PixTransferResult> GetPixTransferAsync(string transferId);
    }
}