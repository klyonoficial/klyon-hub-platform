using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id);
        Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId);
        Task<Payment> CreateAsync(Payment payment);
        Task<Payment> UpdateAsync(Payment payment);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status);
        Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId);
        
        /// <summary>
        /// Busca pagamento por ExternalPaymentId ou por padrão parcial (para reference_id)
        /// Útil quando o webhook envia reference_id ao invés do ID do pedido
        /// </summary>
        Task<Payment?> FindPaymentByExternalIdOrReferenceAsync(string externalId, string? referenceId = null);
    }
}
