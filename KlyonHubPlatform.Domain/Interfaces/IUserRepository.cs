using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<bool> DeleteAsync(int id);
        Task<List<User>> GetAllAsync();
        Task<List<User>> GetByRoleAsync(string roleName);
    }
}
