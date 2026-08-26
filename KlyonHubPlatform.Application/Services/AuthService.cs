using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Helpers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
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
        public Task<string> GenerateTokenAsync(User user, List<string> roles)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is required");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "1440");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, ClientDisplayName.Resolve(user.FullName, user.Email))
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
            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailForAuthenticationAsync(email);
            if (user == null || !user.IsActive)
                return null;
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;
            return user;
        }
        public async Task<bool> RegisterAsync(string email, string password, string fullName, string roleName, string? returnUrl = null, bool? clientHasCnpj = null, string? cnpj = null, string? cpf = null)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email);
            if (existingUser != null)
                return false;
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var emailVerificationToken = Guid.NewGuid().ToString();
            var safeFullName = ClientDisplayName.NullIfPlaceholder(fullName, email) ?? string.Empty;
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = safeFullName,
                EmailVerificationToken = emailVerificationToken,
                IsActive = true,
                EmailVerified = false,
                ClientHasCnpj = roleName == RoleNames.Client ? clientHasCnpj : null
            };
            await _userRepository.CreateAsync(user);
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role != null)
            {
                var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();
            }
            var clientCheck = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (clientCheck != null)
            {
                var contractsCheck = await _context.Contracts.Where(c => c.ClientId == clientCheck.Id).CountAsync();
                if (contractsCheck > 0)
                    throw new InvalidOperationException($"ERRO: Contratos foram criados automaticamente durante o registro do usuário {email}. Isso não deveria acontecer. Verifique triggers no banco de dados ou código que cria contratos automaticamente.");
            }

            // Cria registro mínimo de Client para que o fluxo de contratação rápida encontre o perfil.
            if (roleName == RoleNames.Client)
            {
                string? cnpjFormatted = null;
                string? cpfFormatted = null;

                if (clientHasCnpj == true && !string.IsNullOrWhiteSpace(cnpj))
                {
                    var cnpjDigits = System.Text.RegularExpressions.Regex.Replace(cnpj, @"[^\d]", "");
                    if (cnpjDigits.Length == 14)
                        cnpjFormatted = $"{cnpjDigits[..2]}.{cnpjDigits[2..5]}.{cnpjDigits[5..8]}/{cnpjDigits[8..12]}-{cnpjDigits[12..]}";
                }

                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    var cpfDigits = System.Text.RegularExpressions.Regex.Replace(cpf, @"[^\d]", "");
                    if (cpfDigits.Length == 11)
                        cpfFormatted = $"{cpfDigits[..3]}.{cpfDigits[3..6]}.{cpfDigits[6..9]}-{cpfDigits[9..]}";
                }

                // Só cria se houver ao menos um documento identificador
                if (cnpjFormatted != null || cpfFormatted != null)
                {
                    var cnpjAlreadyExists = cnpjFormatted != null && await _context.Clients.AnyAsync(c => c.CNPJ == cnpjFormatted);
                    var cpfAlreadyExists  = cpfFormatted  != null && await _context.Clients.AnyAsync(c => c.CPF  == cpfFormatted);

                    if (!cnpjAlreadyExists && !cpfAlreadyExists)
                    {
                        var client = new Client
                        {
                            UserId = user.Id,
                            CNPJ = cnpjFormatted,
                            CPF = cpfFormatted,
                            ContactEmail = email,
                            ContactName = ClientDisplayName.NullIfPlaceholder(safeFullName, email),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Clients.Add(client);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            try
            {
                await _emailService.SendVerificationEmailAsync(email, ClientDisplayName.Resolve(safeFullName, email), emailVerificationToken, returnUrl);
            }
            catch (Exception)
            {
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
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, user.PasswordResetToken);
            }
            catch (Exception)
            {
            }
            return true;
        }
        public async Task<bool> RequestCommercialPasswordResetAsync(string email, string taxId)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(taxId))
                return false;

            var emailLower = email.Trim().ToLowerInvariant();
            var taxDigits = System.Text.RegularExpressions.Regex.Replace(taxId, @"[^\d]", "");

            var user = await _userRepository.GetByEmailAsync(emailLower);
            if (user == null)
                return false;

            // Verificar se o CPF ou CNPJ pertence ao mesmo usuário
            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (client == null)
                return false;

            // Normalizar CPF/CNPJ do banco para comparar apenas dígitos
            var clientCpfDigits = client.CPF != null
                ? System.Text.RegularExpressions.Regex.Replace(client.CPF, @"[^\d]", "") : null;
            var clientCnpjDigits = client.CNPJ != null
                ? System.Text.RegularExpressions.Regex.Replace(client.CNPJ, @"[^\d]", "") : null;

            var taxMatch = (clientCpfDigits != null && clientCpfDigits == taxDigits)
                        || (clientCnpjDigits != null && clientCnpjDigits == taxDigits);
            if (!taxMatch)
                return false;

            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(72);
            await _userRepository.UpdateAsync(user);
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, user.PasswordResetToken);
            }
            catch (Exception)
            {
                // não bloquear o fluxo por falha no envio do e-mail
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
            user.MustChangePassword = false;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> DoesResetTokenRequireTaxIdAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetExpires > DateTime.UtcNow);
            if (user == null) return false;
            var client = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == user.Id);
            return client != null && (!string.IsNullOrEmpty(client.CPF) || !string.IsNullOrEmpty(client.CNPJ));
        }

        public async Task<(bool Success, string ErrorMessage)> ResetPasswordWithTaxValidationAsync(
            string token, string? taxId, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.PasswordResetToken == token &&
                u.PasswordResetExpires > DateTime.UtcNow);
            if (user == null)
                return (false, "Link inválido ou expirado. Solicite um novo link de redefinição.");

            // Verificar se o usuário tem CPF/CNPJ e exigir validação
            var client = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (client != null && (!string.IsNullOrEmpty(client.CPF) || !string.IsNullOrEmpty(client.CNPJ)))
            {
                if (string.IsNullOrWhiteSpace(taxId))
                    return (false, "Informe o CPF ou CNPJ para continuar.");

                var providedDigits = System.Text.RegularExpressions.Regex.Replace(taxId, @"[^\d]", "");
                var clientCpfDigits = string.IsNullOrEmpty(client.CPF) ? null
                    : System.Text.RegularExpressions.Regex.Replace(client.CPF, @"[^\d]", "");
                var clientCnpjDigits = string.IsNullOrEmpty(client.CNPJ) ? null
                    : System.Text.RegularExpressions.Regex.Replace(client.CNPJ, @"[^\d]", "");

                var match = (clientCpfDigits != null && clientCpfDigits == providedDigits)
                         || (clientCnpjDigits != null && clientCnpjDigits == providedDigits);
                if (!match)
                    return (false, "O CPF ou CNPJ informado não corresponde ao cadastro vinculado a este e-mail.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            user.MustChangePassword = false;
            await _userRepository.UpdateAsync(user);
            return (true, string.Empty);
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
        public async Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _userRepository.GetByIdAsync(userId, cancellationToken);
        }
        public async Task<User?> GetUserByPasswordResetTokenForAutoLoginAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u =>
                    u.PasswordResetToken == token &&
                    u.PasswordResetExpires.HasValue &&
                    u.PasswordResetExpires.Value > DateTime.UtcNow,
                    cancellationToken);
        }
        public async Task<User> UpdateUserAsync(User user)
        {
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(
            int userId,
            string? currentPassword,
            string newPassword,
            string confirmPassword,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return (false, "Nova senha e confirmação são obrigatórias.");

            if (newPassword != confirmPassword)
                return (false, "As senhas não coincidem.");

            // Política alinhada ao fluxo Identity/Auth existente (mín. 6 caracteres)
            if (newPassword.Length < 6)
                return (false, "A senha deve ter no mínimo 6 caracteres.");

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return (false, "Usuário não encontrado.");

            if (!user.MustChangePassword)
            {
                if (string.IsNullOrWhiteSpace(currentPassword))
                    return (false, "Informe a senha atual.");

                if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    return (false, "Senha atual incorreta.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _userRepository.UpdateAsync(user);
            return (true, string.Empty);
        }
    }
}