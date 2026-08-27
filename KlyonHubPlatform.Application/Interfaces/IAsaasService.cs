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
        /// <summary>
        /// Atualiza o customer já existente no Asaas (PUT /customers/{id}), localizado por <paramref name="asaasCustomerId"/> ou <paramref name="externalReference"/> (ex: "client_123").
        /// Não cria cliente novo. Não lança: Skipped se não existir, Failed se a API falhar ou a chave não estiver configurada.
        /// </summary>
        Task<AsaasCustomerSyncOutcome> TryUpdateExistingCustomerAsync(
            string externalReference,
            string clientName,
            string? clientEmail,
            string? clientTaxId,
            string? clientPhone,
            PagBankAddress? clientAddress,
            string? asaasCustomerId = null,
            CancellationToken cancellationToken = default);
        /// <summary>Tokeniza um cartão no Asaas para cobranças com cartão salvo. Retorna o token ou null em caso de erro.</summary>
        Task<string?> TokenizeCreditCardAsync(string asaasCustomerId, string cardNumber, string expiryMonth, string expiryYear, string cvv, string holderName, string email, string cpfCnpj, string postalCode, string addressNumber, string? phone, string remoteIp);
        /// <param name="customerExternalReference">Referência estável do cliente no Asaas (ex: "client_8"). Quando informada, reutiliza o mesmo cadastro para todas as cobranças desse cliente. Se omitida, usa referenceId (pode criar duplicatas).</param>
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
            string? asaasCustomerIdForToken = null,
            string? remoteIp = null,
            string? customerExternalReference = null);
        /// <summary>
        /// Cria assinatura recorrente no Asaas (POST /subscriptions) com cartão.
        /// <paramref name="cycle"/>: MONTHLY (CardMonthly) ou YEARLY (Card1x anual).
        /// Retorna OrderResponse com Id da primeira cobrança e SubscriptionId (sub_...).
        /// </summary>
        Task<OrderResponse> CreateCreditCardSubscriptionAsync(
            string referenceId,
            decimal amount,
            string cycle,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankCard? card = null,
            PagBankAddress? clientAddress = null,
            string? creditCardToken = null,
            string? asaasCustomerIdForToken = null,
            string? remoteIp = null,
            string? customerExternalReference = null);
        /// <summary>
        /// Cria assinatura Asaas (POST /subscriptions) para <paramref name="billingType"/> PIX ou BOLETO.
        /// <paramref name="cycle"/>: MONTHLY ou YEARLY. Retorna 1ª cobrança + SubscriptionId (sub_...).
        /// </summary>
        Task<OrderResponse> CreateSubscriptionAsync(
            string referenceId,
            decimal amount,
            string billingType,
            string cycle,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankAddress? clientAddress = null,
            string? customerExternalReference = null);
        /// <summary>
        /// Atualiza assinatura Asaas (PUT /subscriptions/{id}).
        /// Com <paramref name="updatePendingPayments"/> true, aplica billingType/valor também nas cobranças pendentes (como o painel Asaas).
        /// </summary>
        Task<AsaasSubscriptionResponse> UpdateSubscriptionAsync(
            string asaasSubscriptionId,
            string? billingType = null,
            decimal? value = null,
            string? cycle = null,
            bool updatePendingPayments = true,
            PagBankCard? card = null,
            AsaasCreditCardHolderInfo? creditCardHolderInfo = null,
            string? creditCardToken = null,
            string? remoteIp = null);
        /// <summary>Cancela/remove assinatura Asaas (DELETE /subscriptions/{id}).</summary>
        Task CancelSubscriptionAsync(string asaasSubscriptionId);
        /// <summary>
        /// Aguarda a 1ª cobrança da assinatura e retorna como OrderResponse (útil após UpdateSubscription
        /// para BOLETO/PIX, quando o Asaas ainda está gerando a cobrança pendente).
        /// Default ~20s de polling (20 x 1s) para linha digitável após conversão BOLETO.
        /// </summary>
        Task<OrderResponse?> WaitForSubscriptionPaymentOrderAsync(string asaasSubscriptionId, int maxAttempts = 20);
        /// <summary>
        /// Paga cobrança pendente existente com cartão (POST /payments/{id}/payWithCreditCard).
        /// Captura imediata mesmo com dueDate futuro — necessário após UpdateSubscription CREDIT_CARD
        /// que só troca billingType sem cobrar (Asaas agenda até o vencimento).
        /// </summary>
        Task<OrderResponse> PayExistingPaymentWithCreditCardAsync(
            string asaasPaymentId,
            PagBankCard? card = null,
            AsaasCreditCardHolderInfo? creditCardHolderInfo = null,
            string? creditCardToken = null,
            string? remoteIp = null);
        /// <summary>
        /// Resolve o pay_* pendente da assinatura (preferindo <paramref name="preferredPaymentId"/> se ainda existir).
        /// </summary>
        Task<string?> ResolveSubscriptionPendingPaymentIdAsync(
            string asaasSubscriptionId,
            string? preferredPaymentId = null);
        Task<OrderResponse> GetOrderAsync(string orderId);
        /// <summary>Obtém o QR Code PIX de uma cobrança já existente no Asaas (para reutilizar em vez de criar nova).</summary>
        Task<AsaasPixQrCodeResponse?> GetPixQrCodeAsync(string asaasPaymentId);
        Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null);
        /// <summary>
        /// Cancela autorização PIX Automático no Asaas (DELETE /pix/automatic/authorizations/{id}).
        /// Instruções agendadas vinculadas podem ser canceladas automaticamente pelo Asaas.
        /// </summary>
        Task CancelPixAutomaticAuthorizationAsync(string authorizationId);
        /// <summary>
        /// Cria autorização PIX Automático. Retorna id, payload do QR Code e expiração.
        /// <paramref name="frequency"/>: MONTHLY ou ANNUALLY.
        /// </summary>
        Task<AsaasPixAutomaticAuthorizationResponse?> CreatePixAutomaticAuthorizationAsync(
            string asaasCustomerId,
            string contractId,
            decimal value,
            string description,
            string frequency = "ANNUALLY",
            int immediateExpirationSeconds = 86400);
        /// <summary>Cria cobrança PIX vinculada à autorização (débito automático na data).</summary>
        Task<OrderResponse?> CreatePaymentWithPixAutomaticAuthorizationAsync(string asaasCustomerId, string pixAutomaticAuthorizationId, decimal value, DateTime dueDate, string externalReference, string description);
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