using KlyonHubPlatform.Application.DTOs.Asaas;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de cobrança e pagamento via API Asaas (provedor fixo).
    /// Retorna DTOs no formato OrderResponse para compatibilidade com o fluxo de pagamento existente.
    /// </summary>
    public interface IAsaasService
    {
        /// <summary>
        /// Valida os dados do cliente/pagamento conforme documentação Asaas (customerData, value, dueDate, etc.).
        /// </summary>
        AsaasValidationResult ValidateCustomerDataForPayment(
            string? clientName,
            string? clientEmail,
            string? clientTaxId,
            string? clientPhone,
            decimal value,
            string? dueDate = null);

        /// <summary>
        /// Cria uma cobrança PIX de teste (valor mínimo) e retorna o status HTTP (200 = sucesso) e mensagem para exibição na tela.
        /// </summary>
        Task<(bool Success, int StatusCode, string Message)> TestCreatePaymentAsync();

        /// <summary>
        /// Cria uma cobrança com cartão de crédito de teste (cartão sandbox Asaas, valor fixo R$ 5,00) e retorna status/mensagem para exibição no Admin.
        /// </summary>
        Task<(bool Success, int StatusCode, string Message)> TestCreateCreditCardPaymentAsync();

        /// <summary>
        /// Cria uma cobrança no Asaas (PIX, Boleto ou Cartão) e retorna no formato de "pedido" (OrderResponse).
        /// </summary>
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

        /// <summary>
        /// Consulta uma cobrança pelo ID (retorna no formato OrderResponse).
        /// </summary>
        Task<OrderResponse> GetOrderAsync(string orderId);

        /// <summary>
        /// Cancela uma cobrança no Asaas.
        /// </summary>
        Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null);

        /// <summary>
        /// Valida a assinatura do webhook do Asaas.
        /// </summary>
        Task<bool> ValidateWebhookSignatureAsync(string payload, string signature);

        /// <summary>
        /// Cria transferência PIX (para repasses).
        /// </summary>
        Task<PixTransferResult> CreatePixTransferAsync(
            string referenceId,
            decimal amount,
            string pixKey,
            string pixKeyType,
            string receiverName,
            string? description = null);

        /// <summary>
        /// Consulta status de uma transferência PIX.
        /// </summary>
        Task<PixTransferResult> GetPixTransferAsync(string transferId);
    }
}
