using KlyonHubPlatform.Application.DTOs.FiscalOnboarding;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IKlyonOnboardingPublicDocumentService
    {
        /// <summary>Lista documentos da Klyon ainda válidos e com arquivo presente. Null se o usuário não for cliente em onboarding fiscal com adesão paga.</summary>
        Task<IReadOnlyList<KlyonOnboardingPublicDocumentItem>?> GetVisibleDocumentsForClientAsync(int userId, string webRootPath, CancellationToken cancellationToken = default);

        /// <summary>Resolve arquivo para download após checagem de elegibilidade e validade.</summary>
        Task<(string FullPath, string DownloadFileName)?> TryGetDocumentFileForClientAsync(int userId, string slug, string webRootPath, CancellationToken cancellationToken = default);
    }
}
