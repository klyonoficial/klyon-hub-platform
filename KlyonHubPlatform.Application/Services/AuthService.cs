using KlyonHubPlatform.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AuthService(
            IUserRepository userRepository, 
            IConfiguration configuration, 
            ApplicationDbContext context,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
        }

        public async Task<string> GenerateTokenAsync(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "1440");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var roles = await GetUserRolesAsync(user.Id);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null || !user.IsActive)
                return null;

            // Em desenvolvimento, permitir login sem verificação de email
            // TODO: Remover esta verificação quando o envio de email estiver implementado
            // if (!user.EmailVerified)
            //     return null;

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return user;
        }

        public async Task<bool> RegisterAsync(string email, string password, string fullName, string roleName)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email);
            if (existingUser != null)
                return false;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var emailVerificationToken = Guid.NewGuid().ToString();

            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = fullName,
                EmailVerificationToken = emailVerificationToken,
                IsActive = true,
                EmailVerified = false
            };

            await _userRepository.CreateAsync(user);

            // Assign role
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role != null)
            {
                var userRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                };
                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            // Send verification email
            try
            {
                await _emailService.SendVerificationEmailAsync(email, fullName, emailVerificationToken);
            }
            catch (Exception)
            {
                // Log error but don't fail registration
            }
            
            return true;
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
            if (user == null)
                return false;

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return false;

            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
            await _userRepository.UpdateAsync(user);

            // Send reset email
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, user.PasswordResetToken);
            }
            catch (Exception)
            {
                // Log error but don't fail password reset request
            }
            
            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.PasswordResetToken == token && 
                u.PasswordResetExpires > DateTime.UtcNow);

            if (user == null)
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<List<string>> GetUserRolesAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return new List<string>();

            return user.UserRoles.Select(ur => ur.Role.Name).ToList();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            return await _userRepository.UpdateAsync(user);
        }
    }
}
