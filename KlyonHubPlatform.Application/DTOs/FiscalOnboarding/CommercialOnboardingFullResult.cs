using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    /// <summary>Resultado do registro completo de onboarding comercial (conta + contrato + pagamento criados).</summary>
    public class CommercialOnboardingFullResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public FiscalOnboardingStartBlockedKind BlockedKind { get; init; }
        /// <summary>Usuário criado/encontrado — disponível somente quando <see cref="Success"/> é true.</summary>
        public User? User { get; init; }
        /// <summary>ID do primeiro pagamento pendente gerado pelo contrato.</summary>
        public int PaymentId { get; init; }

        public static CommercialOnboardingFullResult Ok(User user, int paymentId) => new()
        {
            Success = true,
            User = user,
            PaymentId = paymentId,
            BlockedKind = FiscalOnboardingStartBlockedKind.None
        };

        public static CommercialOnboardingFullResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message,
            BlockedKind = FiscalOnboardingStartBlockedKind.None
        };

        public static CommercialOnboardingFullResult Blocked(FiscalOnboardingStartBlockedKind kind, string? message = null) => new()
        {
            Success = false,
            ErrorMessage = message,
            BlockedKind = kind
        };
    }
}
