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

        /// <summary>
        /// Cria uma transferência PIX para uma chave PIX
        /// </summary>
        /// <param name="referenceId">ID de referência interno</param>
        /// <param name="amount">Valor em reais</param>
        /// <param name="pixKey">Chave PIX do destinatário</param>
        /// <param name="pixKeyType">Tipo da chave: cpf, cnpj, email, phone, evp</param>
        /// <param name="receiverName">Nome do recebedor</param>
        /// <param name="description">Descrição da transferência</param>
        /// <returns>Resultado da transferência</returns>
        Task<PixTransferResult> CreatePixTransferAsync(string referenceId, decimal amount, string pixKey, 
            string pixKeyType, string receiverName, string? description = null);

        /// <summary>
        /// Consulta o status de uma transferência PIX
        /// </summary>
        Task<PixTransferResult> GetPixTransferAsync(string transferId);
    }

    /// <summary>
    /// Resultado de uma transferência PIX
    /// </summary>
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
