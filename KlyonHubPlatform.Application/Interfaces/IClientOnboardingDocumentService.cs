using KlyonHubPlatform.Application.DTOs.ClientOnboarding;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IClientOnboardingDocumentService
    {
        Task<ClientOnboardingPageResult> GetPageAsync(int contractId, int clientId, bool isAdmin, CancellationToken cancellationToken = default);
        Task<ClientOnboardingCommandResult> UploadSetAsync(ClientOnboardingSetUploadInput input, CancellationToken cancellationToken = default);
        Task<ClientOnboardingFileResult?> GetFileAsync(int documentId, int? clientId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default);
        Task<List<ClientOnboardingReviewItem>> ListPendingForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<ClientOnboardingReviewItem>> ListPendingForAdminAsync(CancellationToken cancellationToken = default);
        Task<ClientOnboardingPageResult> GetReviewPageAsync(int contractId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default);
        Task<ClientOnboardingCommandResult> ApproveAsync(int contractId, int reviewerUserId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default);
        Task<ClientOnboardingCommandResult> RejectAsync(int contractId, int reviewerUserId, string reason, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default);
        Task<int> CountPendingForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> CountPendingForAdminAsync(CancellationToken cancellationToken = default);
        bool IsAllowedContentType(string? contentType);
        long MaxFileSizeBytes { get; }
    }
}
