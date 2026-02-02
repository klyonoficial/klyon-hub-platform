using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> GenerateTokenAsync(User user, List<string> roles);
        Task<User?> AuthenticateAsync(string email, string password);
        Task<bool> RegisterAsync(string email, string password, string fullName, string roleName, string? returnUrl = null);
        Task<bool> VerifyEmailAsync(string token);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<List<string>> GetUserRolesAsync(int userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> UpdateUserAsync(User user);
    }
}
