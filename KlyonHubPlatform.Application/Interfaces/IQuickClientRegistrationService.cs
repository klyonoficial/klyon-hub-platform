using KlyonHubPlatform.Application.DTOs;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IQuickClientRegistrationService
    {
        Task<QuickClientRegistrationResult> RegisterAsync(
            QuickClientRegistrationInput input,
            int? adminUserId,
            string? remoteIp,
            string? userAgent,
            CancellationToken cancellationToken = default);
    }
}
