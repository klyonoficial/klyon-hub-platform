using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> GenerateTokenAsync(User user, List<string> roles);
        Task<User?> AuthenticateAsync(string email, string password);
        Task<bool> RegisterAsync(string email, string password, string fullName, string roleName, string? returnUrl = null, bool? clientHasCnpj = null, string? cnpj = null, string? cpf = null);
        Task<bool> VerifyEmailAsync(string token);
        Task<bool> RequestPasswordResetAsync(string email);
        /// <summary>Recuperação de senha — valida e-mail + CPF ou CNPJ antes de enviar o link.</summary>
        Task<bool> RequestCommercialPasswordResetAsync(string email, string taxId);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        /// <summary>Verifica se o token pertence a um usuário com CPF/CNPJ no cadastro (exige validação extra).</summary>
        Task<bool> DoesResetTokenRequireTaxIdAsync(string token);
        /// <summary>Redefine a senha validando CPF/CNPJ quando o usuário tem cadastro de cliente.</summary>
        Task<(bool Success, string ErrorMessage)> ResetPasswordWithTaxValidationAsync(string token, string? taxId, string newPassword);
        Task<List<string>> GetUserRolesAsync(int userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<User?> GetUserByPasswordResetTokenForAutoLoginAsync(string token, CancellationToken cancellationToken = default);
        Task<User> UpdateUserAsync(User user);
    }
}