using KlyonHubPlatform.Application.DTOs.FiscalOnboarding;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IFiscalOnboardingService
    {
        Task<FiscalOnboardingStartResult> StartOnboardingAsync(FiscalOnboardingStartRequest request, CancellationToken cancellationToken = default);
        Task<FiscalOnboardingPaymentView?> GetPaymentViewByPublicTokenAsync(Guid publicToken, CancellationToken cancellationToken = default);
        /// <summary>Chamado quando o pagamento de adesão (sem assinatura) é confirmado — cria usuário, cliente, token e envia e-mail.</summary>
        Task HandleAdhesionPaymentConfirmedAsync(int paymentId, CancellationToken cancellationToken = default);
        /// <summary>Valida token de acesso mágico, marca como usado e retorna o usuário para sign-in.</summary>
        Task<User?> TryConsumeMagicLoginTokenAsync(string token, CancellationToken cancellationToken = default);
        /// <summary>Endereço escolhido no onboarding (para retomar contratação após auto-login).</summary>
        Task<int?> GetCoworkingAddressIdForOnboardedUserAsync(int userId, CancellationToken cancellationToken = default);
    }
}
