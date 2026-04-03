using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Api.Constants;
namespace KlyonHubPlatform.Api.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IFiscalOnboardingService _fiscalOnboardingService;

        public AuthController(
            IAuthService authService,
            IEmailService emailService,
            IConfiguration configuration,
            IFiscalOnboardingService fiscalOnboardingService)
        {
            _authService = authService;
            _emailService = emailService;
            _configuration = configuration;
            _fiscalOnboardingService = fiscalOnboardingService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> AutoLogin(string? token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData[ErrorMessages.TempDataKey] = "Link inválido.";
                return RedirectToAction(ActionNames.Login);
            }
            var user = await _fiscalOnboardingService.TryConsumeMagicLoginTokenAsync(token.Trim(), cancellationToken);
            if (user == null || !user.IsActive)
            {
                TempData[ErrorMessages.TempDataKey] = "Link expirado ou já utilizado.";
                return RedirectToAction(ActionNames.Login);
            }
            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var jwtToken = await _authService.GenerateTokenAsync(user, roles);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(AuthConstants.TokenClaimType, jwtToken)
            };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
            if (roles.Contains(RoleNames.Client) && user.ClientHasCnpj == true)
                claims.Add(new Claim(AuthConstants.ClientHasCnpjClaimType, "true"));
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email ?? "");
            HttpContext.Session.SetString(SessionKeys.UserName, user.FullName ?? "");

            var cwId = await _fiscalOnboardingService.GetCoworkingAddressIdForOnboardedUserAsync(user.Id, cancellationToken);
            if (cwId.HasValue)
            {
                var resume = Url.Action(ActionNames.CreateContractAndGoToPayment, ControllerNames.Client, new { coworkingAddressId = cwId.Value });
                if (!string.IsNullOrEmpty(resume))
                    return Redirect(resume);
            }
            return RedirectToAction(ActionNames.DashboardDetails, ControllerNames.Client);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(role, returnUrl);
            }
            ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
            if (TempData[SuccessMessage.TempDataKey] != null)
            {
                ViewData[ViewDataKeys.Success] = TempData[SuccessMessage.TempDataKey];
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null, bool rememberMe = false, CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(role, returnUrl);
            }
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewData[ViewDataKeys.Error] = "Email e senha são obrigatórios";
                return View();
            }
            var user = await _authService.AuthenticateAsync(email, password);
            if (user == null)
            {
                ViewData[ViewDataKeys.Error] = "Email ou senha inválidos";
                return View();
            }
            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var token = await _authService.GenerateTokenAsync(user, roles);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(AuthConstants.TokenClaimType, token)
            };
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            if (roles.Contains(RoleNames.Client) && user.ClientHasCnpj == true)
            {
                claims.Add(new Claim(AuthConstants.ClientHasCnpjClaimType, "true"));
            }
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var rememberMeDays = _configuration.GetValue("Auth:RememberMeExpirationDays", 14);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(rememberMeDays) : null
            };
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal, authProperties);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email);
            HttpContext.Session.SetString(SessionKeys.UserName, user.FullName);
            if (user.MustChangePassword)
            {
                var tempToken = Guid.NewGuid().ToString();
                user.PasswordResetToken = tempToken;
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
                await _authService.UpdateUserAsync(user);
                HttpContext.Session.SetString(SessionKeys.MustChangePassword, StringBooleanValue.True);
                HttpContext.Session.SetString(SessionKeys.ChangePasswordReturnUrl, returnUrl ?? SessionKeys.DefaultChangePasswordReturnUrlAfterLogin);
                return RedirectToAction(ActionNames.ChangePasswordRequired);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            var primaryRole = roles.FirstOrDefault();
            return RedirectToDashboardByRole(primaryRole, null);
        }
        [HttpGet]
        public IActionResult Register([FromQuery] string? role = null, [FromQuery] string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(userRole, returnUrl);
            }
            if (role == RoleNames.CoworkingPartner)
                ViewBag.PreSelectedRole = RoleNames.CoworkingPartner;
            ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string fullName, string role = RoleNames.Client, string? returnUrl = null, string? acceptLgpdTerms = null, string? hasCnpj = null, CancellationToken cancellationToken = default)
        {
            var acceptLgpdRaw = acceptLgpdTerms ?? Request.Form["acceptLgpdTerms"].ToString();
            bool aceiteLgpd = StringBooleanValue.IsTrue(acceptLgpdRaw)
                || string.Equals(acceptLgpdRaw?.Trim(), "on", StringComparison.OrdinalIgnoreCase);
            bool? clientHasCnpj = null;
            if (role == RoleNames.Client && !string.IsNullOrWhiteSpace(hasCnpj))
                clientHasCnpj = StringBooleanValue.IsTrue(hasCnpj);
            void PreserveRegisterForm(string? err = null)
            {
                if (err != null) ViewData[ViewDataKeys.Error] = err;
                ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
                ViewBag.RegisterFullName = fullName;
                ViewBag.RegisterEmail = email;
                ViewBag.RegisterRole = role;
                ViewBag.RegisterAcceptLgpdTerms = aceiteLgpd;
                ViewBag.RegisterHasCnpj = hasCnpj;
                if (role == RoleNames.CoworkingPartner) ViewBag.PreSelectedRole = RoleNames.CoworkingPartner;
            }
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fullName))
            {
                PreserveRegisterForm("Todos os campos são obrigatórios");
                return View();
            }
            if (!aceiteLgpd)
            {
                PreserveRegisterForm("É obrigatório aceitar os Termos de Uso e a Política de Privacidade (LGPD) para criar sua conta.");
                return View();
            }
            var success = await _authService.RegisterAsync(email, password, fullName, role, returnUrl, clientHasCnpj);
            if (!success)
            {
                PreserveRegisterForm("Erro ao criar conta. Email já existe?");
                return View();
            }
            ViewData[ViewDataKeys.Success] = "Conta criada com sucesso! Verifique seu email para ativar a conta.";
            ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
            return View(ViewNames.Login);
        }
        [HttpPost]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
        {
            await HttpContext.SignOutAsync(AuthConstants.CookieScheme);
            HttpContext.Session.Clear();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.LoggedOut;
            return RedirectToAction(ActionNames.Index, ControllerNames.Home);
        }
        private IActionResult RedirectToDashboardByRole(string? role, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return role switch
            {
                RoleNames.Administrator => RedirectToAction(ActionNames.Dashboard, ControllerNames.Admin),
                RoleNames.CoworkingPartner => RedirectToAction(ActionNames.Dashboard, ControllerNames.Coworking),
                RoleNames.Client => RedirectToAction(ActionNames.DashboardDetails, ControllerNames.Client),
                _ => RedirectToAction(ActionNames.Index, ControllerNames.Home)
            };
        }
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(role, null);
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewData[ViewDataKeys.Error] = "Email é obrigatório.";
                return View();
            }
            var success = await _authService.RequestPasswordResetAsync(email);
            if (success)
            {
                ViewData[ViewDataKeys.Success] = "Se o email existir em nosso sistema, você receberá instruções para redefinir sua senha em alguns minutos.";
            }
            else
            {
                ViewData[ViewDataKeys.Success] = "Se o email existir em nosso sistema, você receberá instruções para redefinir sua senha.";
            }
            return View();
        }
        [HttpGet]
        public IActionResult ResetPassword(string? token = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(role, null);
            }
            if (string.IsNullOrEmpty(token))
            {
                ViewData[ViewDataKeys.Error] = "Token de redefinição não fornecido.";
                return View();
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewData[ViewDataKeys.Error] = "Token de redefinição não fornecido.";
                return View();
            }
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ViewData[ViewDataKeys.Error] = "A senha deve ter no mínimo 6 caracteres.";
                return View();
            }
            if (newPassword != confirmPassword)
            {
                ViewData[ViewDataKeys.Error] = "As senhas não coincidem.";
                return View();
            }
            var success = await _authService.ResetPasswordAsync(token, newPassword);
            if (success)
            {
                ViewData[ViewDataKeys.Success] = "Senha redefinida com sucesso! Você já pode fazer login com sua nova senha.";
            }
            else
            {
                ViewData[ViewDataKeys.Error] = "Token inválido ou expirado. Solicite um novo link de redefinição.";
            }
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string? token = null, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var success = await _authService.VerifyEmailAsync(token);
                if (success)
                {
                    ViewData[ViewDataKeys.Success] = "Email verificado com sucesso! Você já pode fazer login.";
                    ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.EmailVerifiedLogin;
                    return RedirectToAction(ActionNames.Login, new { returnUrl });
                    }
                }
                else
                {
                    ViewData[ViewDataKeys.Error] = "Token inválido ou expirado.";
                }
            }
            else
            {
                ViewData[ViewDataKeys.Message] = "Token de verificação não fornecido.";
            }
            ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
            return View();
        }
        [HttpPost]
        [ActionName("VerifyEmail")]
        public async Task<IActionResult> VerifyEmailPost(string token, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var success = await _authService.VerifyEmailAsync(token);
            if (success)
            {
                ViewData[ViewDataKeys.Success] = "Email verificado com sucesso! Você já pode fazer login.";
                ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.EmailVerifiedLogin;
                    return RedirectToAction(ActionNames.Login, new { returnUrl });
                }
            }
            else
            {
                ViewData[ViewDataKeys.Error] = "Token inválido ou expirado.";
            }
            ViewData[ViewDataKeys.ReturnUrl] = returnUrl;
            return View(ViewNames.VerifyEmail);
        }
        [HttpGet]
        public IActionResult ResendVerification()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboardByRole(role, null);
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ResendVerification(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewData[ViewDataKeys.Error] = "Email é obrigatório.";
                return View();
            }
            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                ViewData[ViewDataKeys.Success] = "Se o email existir em nosso sistema, você receberá um novo link de verificação.";
                return View();
            }
            if (user.EmailVerified)
            {
                ViewData[ViewDataKeys.Message] = "Este email já está verificado. Você pode fazer login normalmente.";
                return View();
            }
            user.EmailVerificationToken = Guid.NewGuid().ToString();
            await _authService.UpdateUserAsync(user);
            try
            {
                var sent = await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, user.EmailVerificationToken);
                if (sent)
                {
                    ViewData[ViewDataKeys.Success] = "Email de verificação reenviado com sucesso! Verifique sua caixa de entrada.";
                }
                else
                {
                    ViewData[ViewDataKeys.Error] = "Erro ao enviar email. Verifique as configurações de email no appsettings.json ou entre em contato com o administrador.";
                }
            }
            catch (Exception ex)
            {
                ViewData[ViewDataKeys.Error] = $"Erro ao enviar email: {ex.Message}";
            }
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> AutoLogin(string token, string? redirect = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidOrExpiredToken;
                return RedirectToAction(ActionNames.Login);
            }
            var user = await _authService.GetUserByPasswordResetTokenForAutoLoginAsync(token, cancellationToken);
            if (user == null || !user.IsActive)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidOrExpiredToken;
                return RedirectToAction(ActionNames.Login);
            }
            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var jwtToken = await _authService.GenerateTokenAsync(user, roles);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(AuthConstants.TokenClaimType, jwtToken)
            };
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email);
            HttpContext.Session.SetString(SessionKeys.UserName, user.FullName);
            if (user.MustChangePassword)
            {
                HttpContext.Session.SetString(SessionKeys.MustChangePassword, StringBooleanValue.True);
                if (!string.IsNullOrEmpty(redirect) && Url.IsLocalUrl(redirect))
                {
                    HttpContext.Session.SetString(SessionKeys.ChangePasswordReturnUrl, redirect);
                }
                return RedirectToAction(ActionNames.ChangePasswordRequired);
            }
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _authService.UpdateUserAsync(user);
            if (!string.IsNullOrEmpty(redirect) && Url.IsLocalUrl(redirect))
            {
                return Redirect(redirect);
            }
            return RedirectToAction(ActionNames.Index, ControllerNames.Home);
        }
        [HttpGet]
        [Authorize]
        public IActionResult ChangePasswordRequired()
        {
            if (HttpContext.Session.GetString(SessionKeys.MustChangePassword) != StringBooleanValue.True)
            {
                return RedirectToAction(ActionNames.Index, ControllerNames.Home);
            }
            return View();
        }
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordRequired(string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
        {
            if (HttpContext.Session.GetString(SessionKeys.MustChangePassword) != StringBooleanValue.True)
            {
                return RedirectToAction(ActionNames.Index, ControllerNames.Home);
            }
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewData[ViewDataKeys.Error] = "Todos os campos são obrigatórios.";
                return View();
            }
            if (newPassword != confirmPassword)
            {
                ViewData[ViewDataKeys.Error] = "As senhas não coincidem.";
                return View();
            }
            if (newPassword.Length < 6)
            {
                ViewData[ViewDataKeys.Error] = "A senha deve ter no mínimo 6 caracteres.";
                return View();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = await _authService.GetUserByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                ViewData[ViewDataKeys.Error] = "Usuário não encontrado.";
                return View();
            }
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ViewData[ViewDataKeys.Error] = "Senha atual incorreta.";
                return View();
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _authService.UpdateUserAsync(user);
            HttpContext.Session.Remove(SessionKeys.MustChangePassword);
            var returnUrl = HttpContext.Session.GetString(SessionKeys.ChangePasswordReturnUrl) ?? SessionKeys.DefaultChangePasswordReturnUrl;
            HttpContext.Session.Remove(SessionKeys.ChangePasswordReturnUrl);
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.PasswordChanged;
            return Redirect(returnUrl);
        }
    }
}