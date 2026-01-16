using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Repositories;
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

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _userRepository.GetAllAsync();
        }

        public async Task<User> UpdateAsync(User user)
        {
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _userRepository.DeleteAsync(id);
        }

        public async Task<bool> AssignRoleAsync(int userId, string roleName)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null) return false;

            var existingRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == role.Id);
            if (existingRole != null) return true;

            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };

            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveRoleAsync(int userId, string roleName)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null) return false;

            var userRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == role.Id);
            if (userRole == null) return false;

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
