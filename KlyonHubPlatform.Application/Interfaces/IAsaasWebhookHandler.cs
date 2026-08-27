using System.Text.Json;
using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Lógica compartilhada para webhooks Asaas de cobranças (PaymentWebhook/asaas e AsaasWebhook).
    /// </summary>
    public interface IAsaasWebhookHandler
    {
        /// <summary>
        /// Despacha evento Asaas (PIX autorização/instrução, assinatura, cobrança) para handlers especializados.
        /// Eventos sem objeto <c>payment</c> são tratados antes de exigir cobrança no payload.
        /// </summary>
        Task<AsaasWebhookHandleResult> HandleEventAsync(
            JsonElement root,
            string? eventType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Localiza pagamento existente por pay_*, externalReference ou pixQrCodeId.
        /// </summary>
        Task<Payment?> ResolveLocalPaymentAsync(
            string asaasPaymentId,
            string? externalReference,
            string? pixQrCodeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Garante Payment local para cobrança recorrente (cartão sub_* ou PIX Automático).
        /// Idempotente: não duplica se ExternalPaymentId já existir.
        /// </summary>
        Task<Payment?> EnsureLocalPaymentFromAsaasChargeAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sincroniza ExternalPaymentId e DueDate quando o Asaas emite pay_* para pagamento já existente.
        /// </summary>
        Task<Payment?> SyncAsaasChargeMetadataAsync(
            Payment payment,
            string asaasPaymentId,
            JsonElement paymentEl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Localiza ou cria Payment local para PAYMENT_OVERDUE, sincroniza DueDate e mantém status Pending
        /// (cartão/assinatura). Não cancela — cancelamento é exclusivo de PAYMENT_DELETED.
        /// </summary>
        Task<Payment?> HandlePaymentOverdueAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sincroniza Payment local com PAYMENT_UPDATED (DueDate, Amount, Status).
        /// Cria via assinatura sub_* se não existir. RECEIVED/CONFIRMED confirmam via PaymentService.
        /// REFUSED/DUNNING marcam Failed (espelha rejeição de cartão).
        /// </summary>
        Task<Payment?> HandlePaymentUpdatedAsync(
            JsonElement paymentEl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Trata PAYMENT_REJECTED / PAYMENT_CREDIT_CARD_CAPTURE_REFUSED (e status REFUSED/DUNNING):
        /// localiza ou cria Payment, marca Failed com FailureReason, espelha PastDue na assinatura.
        /// Idempotente quando já Failed com o mesmo ExternalPaymentId.
        /// </summary>
        Task<Payment?> HandlePaymentRejectedAsync(
            JsonElement paymentEl,
            string? eventType = null,
            CancellationToken cancellationToken = default);
    }
}
