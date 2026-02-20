namespace KlyonHubPlatform.Application.Interfaces
{
    public record ClientRegistrationStatus(bool IsComplete, string? TempDataMessageKey, string? RedirectActionName);
    public interface IClientMenuService
    {
        Task<bool> ClientHasActiveContractAsync(int userId);
        Task<ClientRegistrationStatus> GetRegistrationStatusAsync(int userId);
    }
}