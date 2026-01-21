using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Api.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;

        public AuthController(IAuthService authService, IEmailService emailService)
        {
            _authService = authService;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Se já estiver autenticado, redireciona para o dashboard apropriado
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
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
    }
}
