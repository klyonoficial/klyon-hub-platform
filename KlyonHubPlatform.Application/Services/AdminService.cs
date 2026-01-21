using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public AdminService(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task ResetUserPasswordAsync(int userId, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                throw new ArgumentException("A senha deve ter no mínimo 6 caracteres.");
            }

            if (newPassword != confirmNewPassword)
            {
                throw new ArgumentException("As senhas não coincidem.");
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Usuário não encontrado.");
            }

            // Gerar novo hash da senha usando BCrypt
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
            
            // Atualizar diretamente no contexto para garantir que o hash seja salvo corretamente
            _context.Entry(user).Property(u => u.PasswordHash).IsModified = true;
            await _context.SaveChangesAsync();
        }

        public async Task VerifyUserEmailAsync(int userId)
        {
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Usuário não encontrado.");
            }

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            await _userService.UpdateAsync(user);
        }

        public async Task<List<User>> GetUsersFilteredAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15)
        {
            var query = BuildUsersFilterQuery(search, role, status, emailVerified);
            query = query.OrderByDescending(u => u.CreatedAt);

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetUsersFilteredCountAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null)
        {
            var query = BuildUsersFilterQuery(search, role, status, emailVerified);
            return await query.CountAsync();
        }

        private IQueryable<User> BuildUsersFilterQuery(string? search, string? role, string? status, string? emailVerified)
        {
            var query = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            }

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
            }

            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(u => u.IsActive == isActive);
            }

            if (!string.IsNullOrEmpty(emailVerified))
            {
                bool verified = emailVerified == "verified";
                query = query.Where(u => u.EmailVerified == verified);
            }

            return query;
        }

        public async Task<List<string>> GetAvailableRolesAsync()
        {
            return await _context.Roles.Select(r => r.Name).ToListAsync();
        }

        public async Task<List<Coworking>> GetCoworkingsFilteredAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, int page = 1, int pageSize = 15)
        {
            var query = BuildCoworkingsFilterQuery(search, status, isPartner, approvalStatus);
            query = query.OrderByDescending(c => c.SubmittedAt ?? c.CreatedAt);

            return await query
                .Include(c => c.Addresses)
                .Include(c => c.User)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCoworkingsFilteredCountAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null)
        {
            var query = BuildCoworkingsFilterQuery(search, status, isPartner, approvalStatus);
            return await query.CountAsync();
        }

        private IQueryable<Coworking> BuildCoworkingsFilterQuery(string? search, string? status, string? isPartner, string? approvalStatus)
        {
            var query = _context.Coworkings.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    (c.Name != null && c.Name.Contains(search)) || 
                    (c.CNPJ != null && c.CNPJ.Contains(search)) || 
                    (c.ContactEmail != null && c.ContactEmail.Contains(search)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(c => c.IsActive == isActive);
            }

            if (!string.IsNullOrEmpty(isPartner))
            {
                bool partner = isPartner == "true";
                query = query.Where(c => c.IsPartner == partner);
            }

            if (!string.IsNullOrEmpty(approvalStatus) && Enum.TryParse<CoworkingApprovalStatus>(approvalStatus, out var appStatus))
            {
                query = query.Where(c => c.ApprovalStatus == appStatus);
            }

            return query;
        }

        public async Task<List<Contract>> GetContractsFilteredAsync(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15)
        {
            var query = BuildContractsFilterQuery(search, status, clientId);
            query = query.OrderByDescending(c => c.CreatedAt);

            return await query
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetContractsFilteredCountAsync(string? search = null, string? status = null, int? clientId = null)
        {
            var query = BuildContractsFilterQuery(search, status, clientId);
            return await query.CountAsync();
        }

        private IQueryable<Contract> BuildContractsFilterQuery(string? search, string? status, int? clientId)
        {
            var query = _context.Contracts.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    c.ContractNumber.Contains(search) || 
                    (c.Client != null && (c.Client.RazaoSocial.Contains(search) || c.Client.NomeFantasia.Contains(search))) ||
                    (c.CoworkingAddress != null && (c.CoworkingAddress.Address.Contains(search) || c.CoworkingAddress.City.Contains(search))));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ContractStatus>(status, out var contractStatus))
                {
                    query = query.Where(c => c.Status == contractStatus);
                }
            }

            if (clientId.HasValue && clientId.Value > 0)
            {
                query = query.Where(c => c.ClientId == clientId.Value);
            }

            return query;
        }

        public async Task<List<Client>> GetAllClientsAsync()
        {
            return await _context.Clients
                .OrderBy(c => c.RazaoSocial ?? c.NomeFantasia)
                .ToListAsync();
        }
    }
}
