using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> AssignRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default);
        Task<bool> RemoveRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default);
    }
}
