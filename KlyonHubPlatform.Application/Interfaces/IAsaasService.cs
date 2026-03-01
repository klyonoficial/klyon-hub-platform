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
        /// <summary>Obtém ou cria o cliente no Asaas e retorna o id. Use externalReference estável (ex: "client_123") para reutilizar o mesmo cliente.</summary>
        Task<string> GetOrCreateAsaasCustomerIdAsync(string clientName, string clientEmail, string clientTaxId, string? clientPhone, PagBankAddress? clientAddress, string externalReference);
        /// <summary>Tokeniza um cartão no Asaas para cobranças com cartão salvo. Retorna o token ou null em caso de erro.</summary>
        Task<string?> TokenizeCreditCardAsync(string asaasCustomerId, string cardNumber, string expiryMonth, string expiryYear, string cvv, string holderName, string email, string cpfCnpj, string postalCode, string addressNumber, string? phone, string remoteIp);
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
            PagBankAddress? clientAddress = null,
            string? creditCardToken = null,
            string? asaasCustomerIdForToken = null);
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