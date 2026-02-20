using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;
namespace KlyonHubPlatform.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        public UserService(IUserRepository userRepository, ApplicationDbContext context)
        {
            _userRepository = userRepository;
            _context = context;
        }
        public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _userRepository.GetByIdAsync(id, cancellationToken);
        }
        public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _userRepository.GetAllAsync(cancellationToken);
        }
        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            return await _userRepository.UpdateAsync(user, cancellationToken);
        }
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _userRepository.DeleteAsync(id, cancellationToken);
        }
        public async Task<bool> AssignRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null) return false;
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role == null) return false;
            var existingRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == role.Id);
            if (existingRole != null) return true;
            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        public async Task<bool> RemoveRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null) return false;
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role == null) return false;
            var userRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == role.Id);
            if (userRole == null) return false;
            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<List<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);
        }

        public async Task SetUserRolesAsync(int userId, int[] roleIds, CancellationToken cancellationToken = default)
        {
            var existingUserRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
            _context.UserRoles.RemoveRange(existingUserRoles);
            if (roleIds != null)
            {
                foreach (var roleId in roleIds)
                {
                    var role = await _context.Roles.FindAsync(new object[] { roleId }, cancellationToken);
                    if (role != null)
                    {
                        _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
                    }
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}