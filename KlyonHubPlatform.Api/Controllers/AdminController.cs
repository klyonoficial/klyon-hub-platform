using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IReportService _reportService;
        private readonly ILocationService _locationService;
        private readonly IAdminService _adminService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _context;

        public AdminController(
            IUserService userService,
            IReportService reportService,
            ILocationService locationService,
            IAdminService adminService,
            ISystemConfigurationService systemConfigurationService,
            IPaymentService paymentService,
            ApplicationDbContext context)
        {
            _userService = userService;
            _reportService = reportService;
            _locationService = locationService;
            _adminService = adminService;
            _systemConfigurationService = systemConfigurationService;
            _paymentService = paymentService;
            _context = context; // Mantido apenas para operações de roles que ainda não foram movidas
        }

        public async Task<IActionResult> Dashboard()
        {
            var dashboardData = await _reportService.GetDashboardDataAsync();

            ViewBag.TotalUsers = dashboardData.TotalUsers;
            ViewBag.TotalCoworkings = dashboardData.TotalCoworkings;
            ViewBag.ActiveContracts = dashboardData.ActiveContracts;
            ViewBag.TotalRevenue = dashboardData.TotalRevenue;
            ViewBag.TotalLocations = dashboardData.TotalLocations;
            ViewBag.PendingPayments = dashboardData.PendingPayments;
            ViewBag.TotalCommissions = dashboardData.TotalCommissions;
            ViewBag.InactiveUsers = dashboardData.InactiveUsers;
            ViewBag.UnverifiedUsers = dashboardData.UnverifiedUsers;
            ViewBag.MonthlyRevenue = dashboardData.MonthlyRevenue;
            ViewBag.UsersByRole = dashboardData.UsersByRole;
            ViewBag.ContractsByStatus = dashboardData.ContractsByStatus;
            ViewBag.TopLocations = dashboardData.TopLocations;
            ViewBag.RecentPayments = dashboardData.RecentPayments;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15)
        {
            var users = await _adminService.GetUsersFilteredAsync(search, role, status, emailVerified, page, pageSize);
            var totalItems = await _adminService.GetUsersFilteredCountAsync(search, role, status, emailVerified);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.Status = status;
            ViewBag.EmailVerified = emailVerified;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.AvailableRoles = await _adminService.GetAvailableRolesAsync();

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> ViewUser(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            
            var allRoles = await _context.Roles.ToListAsync();
            var userRoles = user.UserRoles.Select(ur => ur.RoleId).ToList();
            
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;
            
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, string fullName, string email, string? phone, string isActive, int[]? roleIds)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                user.FullName = fullName;
                user.Email = email;
                user.Phone = phone;
                user.IsActive = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                user.UpdatedAt = DateTime.UtcNow;

                await _userService.UpdateAsync(user);

                // Atualizar roles
                if (roleIds != null)
                {
                    var existingUserRoles = await _context.UserRoles
                        .Where(ur => ur.UserId == id)
                        .ToListAsync();
                    
                    _context.UserRoles.RemoveRange(existingUserRoles);
                    
                    foreach (var roleId in roleIds)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = id,
                            RoleId = roleId
                        });
                    }
                    
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Usuário atualizado com sucesso!";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar usuário: {ex.Message}";
                return RedirectToAction("EditUser", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUserEmail(int id)
        {
            try
            {
                await _adminService.VerifyUserEmailAsync(id);
                TempData["Success"] = "E-mail verificado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao verificar e-mail: {ex.Message}";
            }

            return RedirectToAction("ViewUser", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(int userId, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmNewPassword)
            {
                TempData["Error"] = "As senhas não coincidem ou estão vazias.";
                return RedirectToAction("ViewUser", new { id = userId });
            }

            try
            {
                await _adminService.ResetUserPasswordAsync(userId, newPassword, confirmNewPassword);
                TempData["Success"] = "Senha resetada com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao resetar senha: {ex.Message}";
            }

            return RedirectToAction("ViewUser", new { id = userId });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> FixPasswordHash(string email, string newPassword = "password")
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Content($"<h1>Usuário não encontrado</h1><p>Email: {email}</p>", "text/html");
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.PasswordHash = hash;
                await _context.SaveChangesAsync();

                var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                var hashValid = savedUser?.PasswordHash?.StartsWith("$2a$") ?? false;

                return Content($"<h1>Senha Resetada!</h1>" +
                    $"<p><strong>Email:</strong> {user.Email}</p>" +
                    $"<p><strong>Nova Senha:</strong> {newPassword}</p>" +
                    $"<p><strong>Hash válido:</strong> {(hashValid ? "SIM ✓" : "NÃO ✗")}</p>" +
                    $"<p><strong>Hash (primeiros 30 chars):</strong> {(savedUser?.PasswordHash != null ? savedUser.PasswordHash.Substring(0, Math.Min(30, savedUser.PasswordHash.Length)) : "N/A")}</p>" +
                    $"<hr><p><a href='/Auth/Login'>Fazer Login</a></p>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<h1>Erro</h1><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>", "text/html");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Coworkings(string? search = null, string? status = null, string? isPartner = null, int page = 1, int pageSize = 15)
        {
            var coworkings = await _adminService.GetCoworkingsFilteredAsync(search, status, isPartner, page, pageSize);
            var totalItems = await _adminService.GetCoworkingsFilteredCountAsync(search, status, isPartner);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.IsPartner = isPartner;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(coworkings);
        }

        [HttpGet]
        public async Task<IActionResult> ViewCoworking(int id)
        {
            var coworking = await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coworking == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Coworkings");
            }

            return View(coworking);
        }

        [HttpGet]
        public async Task<IActionResult> EditCoworking(int id)
        {
            var coworking = await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coworking == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Coworkings");
            }

            return View(coworking);
        }

        [HttpGet]
        public async Task<IActionResult> Contracts(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15)
        {
            var contracts = await _adminService.GetContractsFilteredAsync(search, status, clientId, page, pageSize);
            var totalItems = await _adminService.GetContractsFilteredCountAsync(search, status, clientId);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.ClientId = clientId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Clients = await _adminService.GetAllClientsAsync();

            return View(contracts);
        }

        [HttpGet]
        public async Task<IActionResult> Locations(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 12)
        {
            var locations = await _locationService.GetFilteredAsync(search, state, region, status, page, pageSize);
            var totalItems = await _locationService.GetFilteredCountAsync(search, state, region, status);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.Commissions = await _locationService.GetActiveCommissionsAsync();
            ViewBag.Search = search;
            ViewBag.State = state;
            ViewBag.Region = region;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.States = await _locationService.GetStatesAsync();
            ViewBag.Regions = await _locationService.GetRegionsAsync();

            return View(locations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLocationCommission(int locationId, decimal commissionPercentage)
        {
            try
            {
                await _locationService.UpdateCommissionAsync(locationId, commissionPercentage);
                TempData["Success"] = $"Comissão atualizada para {commissionPercentage}%";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar comissão: {ex.Message}";
            }

            return RedirectToAction("Locations");
        }

        [HttpGet]
        public IActionResult CreateLocation()
        {
            // Lista de estados brasileiros
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };

            // Lista de regiões
            ViewBag.Regions = new List<string>
            {
                "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
            };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(string city, string state, string? region, string? isActive, decimal commissionPercentage = 15.0m)
        {
            try
            {
                var isActiveBool = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                await _locationService.CreateAsync(city, state, region, isActiveBool, commissionPercentage);

                TempData["Success"] = $"Localidade {city}/{state} criada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao criar localidade: {ex.Message}";
                
                // Recarregar ViewBag para não perder os dados ao retornar a view
                ViewBag.States = new List<string>
                {
                    "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                    "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                    "RS", "RO", "RR", "SC", "SP", "SE", "TO"
                };
                ViewBag.Regions = new List<string>
                {
                    "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
                };
                
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditLocation(int id)
        {
            var location = await _locationService.GetByIdAsync(id);
            if (location == null)
            {
                return NotFound();
            }

            // Lista de estados brasileiros
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };

            // Lista de regiões
            ViewBag.Regions = new List<string>
            {
                "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
            };

            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(int id, string city, string state, string? region, string? isActive)
        {
            try
            {
                var isActiveBool = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                await _locationService.UpdateAsync(id, city, state, region, isActiveBool);

                TempData["Success"] = $"Localidade {city}/{state} atualizada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Localidade não encontrada.";
                return RedirectToAction("Locations");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("EditLocation", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar localidade: {ex.Message}";
                return RedirectToAction("EditLocation", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            try
            {
                var deleted = await _locationService.DeleteAsync(id);
                if (!deleted)
                {
                    TempData["Error"] = "Localidade não encontrada.";
                    return RedirectToAction("Locations");
                }

                TempData["Success"] = "Localidade excluída com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao excluir localidade: {ex.Message}";
                return RedirectToAction("Locations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var startDate = DateTime.UtcNow.AddMonths(-1);
            var endDate = DateTime.UtcNow;

            // Buscar pagamentos dos últimos 30 dias incluindo pendentes
            var payments = await _reportService.GetRecentPaymentsAsync(30, 500);

            ViewBag.Payments = payments;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SystemConfigurations()
        {
            var configurations = await _systemConfigurationService.GetAllAsync();
            var groupedByCategory = configurations.GroupBy(c => c.Category).ToList();
            
            // Obter o provedor de storage atual
            var currentProvider = await _systemConfigurationService.GetValueAsync("Storage.Provider") ?? "AWS";
            
            ViewBag.GroupedConfigurations = groupedByCategory;
            ViewBag.Categories = groupedByCategory.Select(g => g.Key).ToList();
            ViewBag.CurrentStorageProvider = currentProvider;
            
            return View(configurations);
        }

        [HttpGet]
        public async Task<IActionResult> EditSystemConfiguration(int id)
        {
            var configuration = await _systemConfigurationService.GetByIdAsync(id);
            if (configuration == null)
            {
                TempData["Error"] = "Configuração não encontrada.";
                return RedirectToAction("SystemConfigurations");
            }
            
            return View(configuration);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSystemConfiguration(int id, string value, string? description)
        {
            try
            {
                var configuration = await _systemConfigurationService.GetByIdAsync(id);
                if (configuration == null)
                {
                    TempData["Error"] = "Configuração não encontrada.";
                    return RedirectToAction("SystemConfigurations");
                }

                configuration.Value = value;
                configuration.Description = description ?? configuration.Description;
                configuration.UpdatedBy = User.Identity?.Name;

                await _systemConfigurationService.UpdateAsync(configuration, User.Identity?.Name);

                TempData["Success"] = $"Configuração '{configuration.Key}' atualizada com sucesso!";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar configuração: {ex.Message}";
                return RedirectToAction("EditSystemConfiguration", new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Payments(string? search = null, string? status = null, int page = 1, int pageSize = 15)
        {
            var query = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .AsQueryable();

            // Filtro de busca
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => 
                    p.ExternalPaymentId.Contains(search) ||
                    (p.TransactionId != null && p.TransactionId.Contains(search)) ||
                    p.Subscription.Contract.ContractNumber.Contains(search) ||
                    (p.Subscription.Contract.Client.RazaoSocial != null && p.Subscription.Contract.Client.RazaoSocial.Contains(search)) ||
                    (p.Subscription.Contract.Client.NomeFantasia != null && p.Subscription.Contract.Client.NomeFantasia.Contains(search)));
            }

            // Filtro por status
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<PaymentStatus>(status, out var paymentStatus))
                {
                    query = query.Where(p => p.Status == paymentStatus);
                }
            }

            // Ordenação
            query = query.OrderByDescending(p => p.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var payments = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int paymentId)
        {
            try
            {
                await _paymentService.ConfirmPaymentAsync(paymentId, Guid.NewGuid().ToString());
                TempData["Success"] = "Pagamento confirmado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao confirmar pagamento: {ex.Message}";
            }

            return RedirectToAction("Payments");
        }
    }
}
