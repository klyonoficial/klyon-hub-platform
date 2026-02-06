using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);
        Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
        Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Busca pagamento por ExternalPaymentId ou por padrão parcial (para reference_id)
        /// Útil quando o webhook envia reference_id ao invés do ID do pedido
        /// </summary>
        Task<Payment?> FindPaymentByExternalIdOrReferenceAsync(string externalId, string? referenceId = null, CancellationToken cancellationToken = default);
    }
}
