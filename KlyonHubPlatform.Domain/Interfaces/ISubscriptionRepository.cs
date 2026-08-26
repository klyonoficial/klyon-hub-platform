using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Subscription?> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default);
        Task<Subscription> UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
        Task<List<Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);
        Task<List<Subscription>> GetDueSubscriptionsAsync(DateTime date, CancellationToken cancellationToken = default);
        /// <summary>Assinaturas ativas com autorização PIX Automático (para emitir cobranças recorrentes na janela Asaas).</summary>
        Task<List<Subscription>> GetActivePixAutomaticSubscriptionsAsync(CancellationToken cancellationToken = default);
        Task<Subscription?> GetByPixAutomaticAuthorizationIdAsync(string pixAutomaticAuthorizationId, CancellationToken cancellationToken = default);
        /// <summary>Busca assinatura local pelo ID da assinatura no Asaas (sub_...).</summary>
        Task<Subscription?> GetByExternalSubscriptionIdAsync(string externalSubscriptionId, CancellationToken cancellationToken = default);
    }
}