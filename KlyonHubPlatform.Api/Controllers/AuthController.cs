using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Api.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;

        public AuthController(IAuthService authService, IEmailService emailService, ApplicationDbContext context)
        {
            _authService = authService;
            _emailService = emailService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewData["Error"] = "Email e senha são obrigatórios";
                return View();
            }

            var user = await _authService.AuthenticateAsync(email, password);
            if (user == null)
            {
                ViewData["Error"] = "Email ou senha inválidos";
                return View();
            }

            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var token = await _authService.GenerateTokenAsync(user, roles);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim("Token", token)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync("Cookies", claimsPrincipal);

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.FullName);

            // Verificar se precisa alterar senha
            if (user.MustChangePassword)
            {
                // Criar token temporário para redirecionar após alteração de senha
                var tempToken = Guid.NewGuid().ToString();
                user.PasswordResetToken = tempToken;
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
                await _authService.UpdateUserAsync(user);
                
                HttpContext.Session.SetString("MustChangePassword", "true");
                HttpContext.Session.SetString("ChangePasswordReturnUrl", returnUrl ?? "/Client/DashboardDetails");
                
                return RedirectToAction("ChangePasswordRequired");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string fullName, string role = "Client")
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fullName))
            {
                ViewData["Error"] = "Todos os campos são obrigatórios";
                return View();
            }

            // IMPORTANTE: Este método apenas cria o usuário e atribui a role.
            // NÃO cria Client nem Contract automaticamente.
            // O cliente deve ser criado separadamente através de /Client/RegisterClient
            // e os contratos devem ser criados apenas quando o cliente escolher um endereço fiscal.
            var success = await _authService.RegisterAsync(email, password, fullName, role);
            if (!success)
            {
                ViewData["Error"] = "Erro ao criar conta. Email já existe?";
                return View();
            }

            ViewData["Success"] = "Conta criada com sucesso! Verifique seu email para ativar a conta.";
            return View("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewData["Error"] = "Email é obrigatório.";
                return View();
            }

            var success = await _authService.RequestPasswordResetAsync(email);
            if (success)
            {
                ViewData["Success"] = "Se o email existir em nosso sistema, você receberá instruções para redefinir sua senha em alguns minutos.";
            }
            else
            {
                // Por segurança, não revelar se o email existe
                ViewData["Success"] = "Se o email existir em nosso sistema, você receberá instruções para redefinir sua senha.";
            }
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? token = null)
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            if (string.IsNullOrEmpty(token))
            {
                ViewData["Error"] = "Token de redefinição não fornecido.";
                return View();
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewData["Error"] = "Token de redefinição não fornecido.";
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ViewData["Error"] = "A senha deve ter no mínimo 6 caracteres.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "As senhas não coincidem.";
                return View();
            }

            var success = await _authService.ResetPasswordAsync(token, newPassword);
            if (success)
            {
                ViewData["Success"] = "Senha redefinida com sucesso! Você já pode fazer login com sua nova senha.";
            }
            else
            {
                ViewData["Error"] = "Token inválido ou expirado. Solicite um novo link de redefinição.";
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string? token = null)
        {
            if (!string.IsNullOrEmpty(token))
            {
                // Se o token vier na query string, processa automaticamente
                var success = await _authService.VerifyEmailAsync(token);
                if (success)
                {
                    ViewData["Success"] = "Email verificado com sucesso! Você já pode fazer login.";
                }
                else
                {
                    ViewData["Error"] = "Token inválido ou expirado.";
                }
            }
            else
            {
                ViewData["Message"] = "Token de verificação não fornecido.";
            }
            return View();
        }

        [HttpPost]
        [ActionName("VerifyEmail")]
        public async Task<IActionResult> VerifyEmailPost(string token)
        {
            var success = await _authService.VerifyEmailAsync(token);
            if (success)
            {
                ViewData["Success"] = "Email verificado com sucesso! Você já pode fazer login.";
            }
            else
            {
                ViewData["Error"] = "Token inválido ou expirado.";
            }
            return View("VerifyEmail");
        }

        [HttpGet]
        public IActionResult ResendVerification()
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Client")
                {
                    return RedirectToAction("Services", "Client");
                }
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    _ => "Home"
                });
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResendVerification(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewData["Error"] = "Email é obrigatório.";
                return View();
            }

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                // Por segurança, não revelar se o email existe ou não
                ViewData["Success"] = "Se o email existir em nosso sistema, você receberá um novo link de verificação.";
                return View();
            }

            if (user.EmailVerified)
            {
                ViewData["Message"] = "Este email já está verificado. Você pode fazer login normalmente.";
                return View();
            }

            // Gerar novo token
            user.EmailVerificationToken = Guid.NewGuid().ToString();
            await _authService.UpdateUserAsync(user);

            // Enviar email
            try
            {
                var sent = await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, user.EmailVerificationToken);
                
                if (sent)
                {
                    ViewData["Success"] = "Email de verificação reenviado com sucesso! Verifique sua caixa de entrada.";
                }
                else
                {
                    ViewData["Error"] = "Erro ao enviar email. Verifique as configurações de email no appsettings.json ou entre em contato com o administrador.";
                }
            }
            catch (Exception ex)
            {
                ViewData["Error"] = $"Erro ao enviar email: {ex.Message}";
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AutoLogin(string token, string? redirect = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Token inválido ou expirado.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => 
                    u.PasswordResetToken == token && 
                    u.PasswordResetExpires.HasValue && 
                    u.PasswordResetExpires.Value > DateTime.UtcNow);

            if (user == null || !user.IsActive)
            {
                TempData["Error"] = "Token inválido ou expirado.";
                return RedirectToAction("Login");
            }

            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var jwtToken = await _authService.GenerateTokenAsync(user, roles);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim("Token", jwtToken)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync("Cookies", claimsPrincipal);

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.FullName);

            // Verificar se precisa alterar senha
            if (user.MustChangePassword)
            {
                HttpContext.Session.SetString("MustChangePassword", "true");
                if (!string.IsNullOrEmpty(redirect) && Url.IsLocalUrl(redirect))
                {
                    HttpContext.Session.SetString("ChangePasswordReturnUrl", redirect);
                }
                return RedirectToAction("ChangePasswordRequired");
            }

            // Limpar token após uso
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(redirect) && Url.IsLocalUrl(redirect))
            {
                return Redirect(redirect);
            }

            return RedirectToAction("Services", "Client");
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePasswordRequired()
        {
            if (HttpContext.Session.GetString("MustChangePassword") != "true")
            {
                return RedirectToAction("Services", "Client");
            }
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordRequired(string currentPassword, string newPassword, string confirmPassword)
        {
            if (HttpContext.Session.GetString("MustChangePassword") != "true")
            {
                return RedirectToAction("Services", "Client");
            }

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewData["Error"] = "Todos os campos são obrigatórios.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "As senhas não coincidem.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewData["Error"] = "A senha deve ter no mínimo 6 caracteres.";
                return View();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null)
            {
                ViewData["Error"] = "Usuário não encontrado.";
                return View();
            }

            // Verificar senha atual
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ViewData["Error"] = "Senha atual incorreta.";
                return View();
            }

            // Atualizar senha
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("MustChangePassword");
            var returnUrl = HttpContext.Session.GetString("ChangePasswordReturnUrl") ?? "/Client/Services";
            HttpContext.Session.Remove("ChangePasswordReturnUrl");

            TempData["Success"] = "Senha alterada com sucesso!";
            return Redirect(returnUrl);
        }
    }
}
