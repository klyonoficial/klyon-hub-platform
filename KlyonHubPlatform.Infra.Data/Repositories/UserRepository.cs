using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;
        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }
        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }
        public async Task<User?> GetByEmailForAuthenticationAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }
        public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return user;
        }
        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync(cancellationToken);
            return user;
        }
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(id, cancellationToken);
            if (user == null) return false;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<User>> GetByRoleAsync(string roleName, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName))
                .ToListAsync(cancellationToken);
        }
    }
}