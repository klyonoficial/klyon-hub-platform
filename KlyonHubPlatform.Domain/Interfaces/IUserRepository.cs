using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailForAuthenticationAsync(string email, CancellationToken cancellationToken = default);
        Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
        Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<User>> GetByRoleAsync(string roleName, CancellationToken cancellationToken = default);
    }
}