using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.DTOs.Payments;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPaymentService
    {
        /// <summary>Cria um pagamento. Para PIX sem recorrência, subscriptionId pode ser null.</summary>
        Task<Payment> CreatePaymentAsync(
            int? subscriptionId,
            decimal amount,
            PaymentMethod method,
            decimal snapshotListPrice = 0,
            string? promoCodeSnapshot = null,
            decimal snapshotDiscountAmount = 0,
            int? promoCodeId = null,
            string? snapshotBillingCycle = null);
        Task<Payment> ProcessPaymentAsync(int paymentId);
        Task<Payment> ConfirmPaymentAsync(int paymentId, string? transactionId = null);
        Task<Payment> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, string? transactionId = null);
        /// <summary>
        /// Altera o método de pagamento. Para PIX, use pixPlanType: PixAnnual, PixMonthly ou None (pagamento único).
        /// Informe <paramref name="previousCardPlanType"/> quando o Sync de checkout já tiver atualizado o contrato,
        /// para detectar troca PixAnnual ↔ PixMonthly ↔ None e cancelar charge/auth no Asaas antes de regenerar.
        /// </summary>
        Task<Payment> UpdatePaymentMethodAsync(
            int paymentId,
            PaymentMethod method,
            ContractCardPlanType? pixPlanType = null,
            ContractCardPlanType? previousCardPlanType = null);
        /// <param name="previousCardPlanType">Plano antes do Sync (quando o contrato já foi atualizado). Se distinto de pixPlanType, cancela artifacts Asaas.</param>
        Task<string> GeneratePixCodeAsync(int paymentId, ContractCardPlanType? pixPlanType = null, ContractCardPlanType? previousCardPlanType = null);
        /// <summary>Reabre um pagamento PIX cancelado/vencido para permitir gerar novo código (ex.: após PAYMENT_OVERDUE).</summary>
        Task ReopenCancelledPixPaymentAsync(int paymentId);
        /// <summary>Trata vencimento (PAYMENT_OVERDUE) de pagamento PIX: limpa códigos e gera novo via Asaas (PIX único e PIX Automático). A data de vencimento na base passa a ser a data real do novo código retornada pelo Asaas. Garante que, ao expirar ou receber mensagem de vencido/cancelado, seja gerado outro código para o cliente pagar.</summary>
        Task HandlePixPaymentOverdueAsync(int paymentId);
        /// <summary>Verifica se o código PIX (único ou recorrente anual) está vencido; se sim, faz nova requisição ao Asaas para obter novo(s) código(s) e persiste na base. Deve ser chamado ao exibir/uso do pagamento para garantir código válido.</summary>
        Task EnsurePixCodeValidAsync(int paymentId);
        Task<string> GenerateBoletoCodeAsync(int paymentId);
        Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null, string? remoteIp = null, string? postalCode = null, string? addressNumber = null, bool addressHasNoNumber = false);
        /// <summary>Cobra o pagamento usando um cartão já tokenizado (AsaasToken).</summary>
        Task<Payment> ProcessPaymentWithSavedCardAsync(int paymentId, int creditCardId, int? installments = null, string? remoteIp = null, string? postalCode = null, string? addressNumber = null, bool addressHasNoNumber = false);
        /// <summary>Tokeniza o cartão no Asaas com CEP e número do form (creditCardHolderInfo). Não usa placeholder 00000-000. Retorna token e billing normalizado para persistir no cartão.</summary>
        Task<CreditCardTokenizeResult> TokenizeCreditCardForClientAsync(int clientId, string cardNumber, string expiryMonth, string expiryYear, string cvv, string holderName, string? postalCode, string? addressNumber, bool addressHasNoNumber, string remoteIp, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetPaymentsByClientIdAsync(int clientId);
        Task<Payment?> GetPaymentByIdAsync(int paymentId);
    }
}