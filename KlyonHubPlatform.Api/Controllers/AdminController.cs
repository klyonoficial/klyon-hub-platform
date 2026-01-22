using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;

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
        private readonly IPagBankService _pagBankService;
        private readonly IAuditService _auditService;
        private readonly ISefazService _sefazService;

        public AdminController(
            IUserService userService,
            IReportService reportService,
            ILocationService locationService,
            IAdminService adminService,
            ISystemConfigurationService systemConfigurationService,
            IPaymentService paymentService,
            ApplicationDbContext context,
            IPagBankService pagBankService,
            IAuditService auditService,
            ISefazService sefazService)
        {
            _userService = userService;
            _reportService = reportService;
            _locationService = locationService;
            _adminService = adminService;
            _systemConfigurationService = systemConfigurationService;
            _paymentService = paymentService;
            _context = context;
            _pagBankService = pagBankService;
            _auditService = auditService;
            _sefazService = sefazService;
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

            // Verificar migrations pendentes
            try
            {
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                ViewBag.PendingMigrationsCount = pendingMigrations.Count();
                ViewBag.PendingMigrations = pendingMigrations.ToList();
            }
            catch (Exception)
            {
                ViewBag.PendingMigrationsCount = 0;
                ViewBag.PendingMigrations = new List<string>();
            }

            // Estatísticas de produtos
            try
            {
                var productsStats = await _context.CoworkingProducts
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.ProductType)
                    .Select(g => new { productType = g.Key, count = g.Count() })
                    .ToListAsync();
                ViewBag.ProductsByType = productsStats;
                
                var totalReservations = await _context.Reservations.CountAsync();
                ViewBag.TotalReservations = totalReservations;
                
                var reservationsByStatus = await _context.Reservations
                    .GroupBy(r => r.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync();
                ViewBag.ReservationsByStatus = reservationsByStatus;
            }
            catch
            {
                ViewBag.ProductsByType = new List<dynamic>();
                ViewBag.TotalReservations = 0;
                ViewBag.ReservationsByStatus = new List<dynamic>();
            }

            // Estatísticas de serviços e percentagens
            try
            {
                // Contar parceiros com serviços sem percentagem definida (ou com percentagem = 0)
                var partnersWithoutPercentage = await _context.PartnerServices
                    .Where(ps => ps.Percentage == 0)
                    .Select(ps => ps.CoworkingId)
                    .Distinct()
                    .CountAsync();
                
                // Contar total de serviços aguardando definição de percentagem
                var servicesAwaitingPercentage = await _context.PartnerServices
                    .Where(ps => ps.Percentage == 0)
                    .CountAsync();
                
                ViewBag.PartnersWithoutPercentage = partnersWithoutPercentage;
                ViewBag.ServicesAwaitingPercentage = servicesAwaitingPercentage;
            }
            catch
            {
                ViewBag.PartnersWithoutPercentage = 0;
                ViewBag.ServicesAwaitingPercentage = 0;
            }

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
        public async Task<IActionResult> Coworkings(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, int page = 1, int pageSize = 15)
        {
            var coworkings = await _adminService.GetCoworkingsFilteredAsync(search, status, isPartner, approvalStatus, page, pageSize);
            var totalItems = await _adminService.GetCoworkingsFilteredCountAsync(search, status, isPartner, approvalStatus);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            // Carregar contagem de serviços para cada coworking
            var coworkingIds = coworkings.Select(c => c.Id).ToList();
            var servicesCount = await _context.PartnerServices
                .Where(ps => coworkingIds.Contains(ps.CoworkingId))
                .GroupBy(ps => ps.CoworkingId)
                .Select(g => new { CoworkingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CoworkingId, x => x.Count);
            
            ViewBag.ServicesCount = servicesCount;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.IsPartner = isPartner;
            ViewBag.ApprovalStatus = approvalStatus;
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

            // Carregar serviços disponíveis
            var services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();
            ViewBag.Services = services;

            // Carregar serviços do parceiro (apenas os que têm serviço ativo)
            var partnerServices = await _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == id && ps.Service != null && ps.Service.IsActive)
                .ToListAsync();
            ViewBag.PartnerServices = partnerServices;

            return View(coworking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCoworking(int id, string? search, string? status, string? isPartner, string? approvalStatus, int? page)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _context.Coworkings.FindAsync(id);
            if (coworking == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Coworkings"); }
            var oldStatus = coworking.ApprovalStatus;
            coworking.ApprovalStatus = CoworkingApprovalStatus.Approved;
            coworking.ApprovedAt = DateTime.UtcNow;
            coworking.ApprovedBy = userId;
            coworking.RejectionReason = null;
            coworking.RejectedAt = null;
            coworking.RejectedBy = null;
            coworking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _auditService.LogActionAsync(userId, "ApprovalStatusChange", "Coworking", id, "{\"ApprovalStatus\":\"" + oldStatus + "\"}", "{\"ApprovalStatus\":\"Approved\"}");
            TempData["Success"] = $"Cadastro de '{coworking.Name}' aprovado. O espaço já está disponível no marketplace.";
            if (page.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(isPartner) || !string.IsNullOrEmpty(approvalStatus))
                return RedirectToAction("Coworkings", new { search, status, isPartner, approvalStatus, page });
            return RedirectToAction("ViewCoworking", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCoworking(int id, string rejectionReason, string? search, string? status, string? isPartner, string? approvalStatus, int? page)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason)) { TempData["Error"] = "Informe a justificativa ou os itens que precisam de correção."; return RedirectToAction("ViewCoworking", new { id }); }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _context.Coworkings.FindAsync(id);
            if (coworking == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Coworkings"); }
            var oldStatus = coworking.ApprovalStatus;
            coworking.ApprovalStatus = CoworkingApprovalStatus.AwaitingAdjustment;
            coworking.RejectionReason = rejectionReason.Trim();
            coworking.RejectedAt = DateTime.UtcNow;
            coworking.RejectedBy = userId;
            coworking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _auditService.LogActionAsync(userId, "ApprovalStatusChange", "Coworking", id, "{\"ApprovalStatus\":\"" + oldStatus + "\"}", "{\"ApprovalStatus\":\"AwaitingAdjustment\",\"RejectionReason\":" + System.Text.Json.JsonSerializer.Serialize(coworking.RejectionReason) + "}");
            TempData["Success"] = "Cadastro reprovado. O coworking foi notificado e poderá reenviar após os ajustes.";
            if (page.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(isPartner) || !string.IsNullOrEmpty(approvalStatus))
                return RedirectToAction("Coworkings", new { search, status, isPartner, approvalStatus, page });
            return RedirectToAction("ViewCoworking", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CoworkingHistory(int id)
        {
            var coworking = await _context.Coworkings.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (coworking == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Coworkings"); }
            var logs = await _auditService.GetLogsByEntityAsync("Coworking", id);
            ViewBag.Coworking = coworking;
            return View(logs);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCoworkingStatus(int id)
        {
            try
            {
                var coworking = await _context.Coworkings.FindAsync(id);

                if (coworking == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Coworkings");
                }

                coworking.IsActive = !coworking.IsActive;
                coworking.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var statusText = coworking.IsActive ? "ativado" : "inativado";
                TempData["Success"] = $"Coworking '{coworking.Name}' foi {statusText} com sucesso.";

                return RedirectToAction("Coworkings", new { 
                    search = Request.Query["search"],
                    status = Request.Query["status"],
                    isPartner = Request.Query["isPartner"],
                    approvalStatus = Request.Query["approvalStatus"],
                    page = Request.Query["page"]
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao alterar status do coworking: {ex.Message}";
                return RedirectToAction("Coworkings");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Contracts(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 10)
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
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string? product, int page = 1, int pageSize = 15)
        {
            var start = startDate?.Date ?? DateTime.UtcNow.AddMonths(-1).Date;
            var end = endDate?.Date ?? DateTime.UtcNow.Date;
            if (end < start) end = start;

            var report = await _reportService.GetFinancialReportAsync(start, end, product);

            // Paginação
            var totalItems = report.Rows.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            if (page < 1) page = 1;

            // Aplicar paginação nas linhas
            var paginatedRows = report.Rows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Criar novo resultado com linhas paginadas
            var paginatedReport = new Application.Interfaces.ReportFinancialResult
            {
                Rows = paginatedRows,
                TotalPayments = report.TotalPayments,
                TotalReceived = report.TotalReceived,
                TotalCommissions = report.TotalCommissions,
                Pending = report.Pending
            };

            ViewBag.Report = paginatedReport;
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            ViewBag.Product = product ?? ReportProductFilter.Todos;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ReportCredits()
        {
            var list = await _reportService.GetClientCreditsReportAsync();
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> ReportClientCreditStatement(int clientId, DateTime? dateFrom, DateTime? dateTo)
        {
            var dto = await _reportService.GetClientCreditStatementAsync(clientId, dateFrom, dateTo);
            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> GetStatementMovementDetails(int? paymentId, int? reservationId)
        {
            if (paymentId.HasValue)
            {
                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.CoworkingAddress)
                                .ThenInclude(a => a.Location)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.Client)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r.CoworkingAddress)
                            .ThenInclude(a => a.Coworking)
                    .FirstOrDefaultAsync(p => p.Id == paymentId.Value);

                if (payment == null)
                    return Json(new { success = false, message = "Pagamento não encontrado" });

                var paymentData = new
                {
                    type = "Pagamento",
                    id = payment.Id,
                    amount = payment.Amount,
                    status = payment.Status.ToString(),
                    method = payment.Method.ToString(),
                    dueDate = payment.DueDate.ToString("dd/MM/yyyy"),
                    paidAt = payment.PaidAt?.ToString("dd/MM/yyyy HH:mm"),
                    createdAt = payment.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    externalPaymentId = payment.ExternalPaymentId,
                    pixCode = payment.PixCode,
                    boletoCode = payment.BoletoCode,
                    transactionId = payment.TransactionId,
                    subscription = payment.Subscription != null ? new
                    {
                        contractNumber = payment.Subscription.Contract?.ContractNumber,
                        monthlyValue = payment.Subscription.Contract?.MonthlyValue,
                        location = payment.Subscription.Contract?.CoworkingAddress?.Location != null
                            ? $"{payment.Subscription.Contract.CoworkingAddress.Location.City}/{payment.Subscription.Contract.CoworkingAddress.Location.State}"
                            : null,
                        address = payment.Subscription.Contract?.CoworkingAddress != null
                            ? $"{payment.Subscription.Contract.CoworkingAddress.Address}, {payment.Subscription.Contract.CoworkingAddress.Number}"
                            : null
                    } : null,
                    reservation = payment.Reservation != null ? new
                    {
                        id = payment.Reservation.Id,
                        coworkingName = payment.Reservation.CoworkingAddress?.Coworking?.Name,
                        reservationDate = payment.Reservation.ReservationDate.ToString("dd/MM/yyyy"),
                        roomType = payment.Reservation.RoomType,
                        totalValue = payment.Reservation.TotalValue
                    } : null
                };

                return Json(new { success = true, data = paymentData });
            }
            else if (reservationId.HasValue)
            {
                var reservation = await _context.Reservations
                    .Include(r => r.Client)
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                    .Include(r => r.CoworkingRoom)
                    .FirstOrDefaultAsync(r => r.Id == reservationId.Value);

                if (reservation == null)
                    return Json(new { success = false, message = "Reserva não encontrada" });

                var reservationData = new
                {
                    type = "Reserva",
                    id = reservation.Id,
                    totalValue = reservation.TotalValue,
                    status = reservation.Status,
                    reservationDate = reservation.ReservationDate.ToString("dd/MM/yyyy"),
                    startTime = reservation.StartTime?.ToString(@"hh\:mm"),
                    endTime = reservation.EndTime?.ToString(@"hh\:mm"),
                    roomType = reservation.RoomType,
                    roomTypeDisplay = reservation.RoomType == "Shared" ? "Sala compartilhada" 
                        : reservation.RoomType == "Private" ? "Sala privativa" 
                        : reservation.RoomType == "Auditorium" ? "Auditório" 
                        : reservation.RoomType,
                    clientConfirmedAt = reservation.ClientConfirmedAt?.ToString("dd/MM/yyyy HH:mm"),
                    coworkingConfirmedAt = reservation.CoworkingConfirmedAt?.ToString("dd/MM/yyyy HH:mm"),
                    coworkingName = reservation.CoworkingAddress?.Coworking?.Name,
                    coworkingAddress = reservation.CoworkingAddress != null
                        ? $"{reservation.CoworkingAddress.Address}, {reservation.CoworkingAddress.Number}"
                        : null,
                    roomName = reservation.CoworkingRoom?.Name,
                    createdAt = reservation.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                };

                return Json(new { success = true, data = reservationData });
            }

            return Json(new { success = false, message = "ID de pagamento ou reserva não fornecido" });
        }

        [HttpGet]
        public async Task<IActionResult> ReportCoworkingRevenue()
        {
            var list = await _reportService.GetCoworkingRevenueReportAsync();
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> ReportCoworkingRevenueDetail(int id, DateTime? dateFrom, DateTime? dateTo)
        {
            var dto = await _reportService.GetCoworkingRevenueDetailAsync(id, dateFrom, dateTo);
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> ReservationFinancialDetail(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                TempData["Error"] = "Reserva não encontrada.";
                return RedirectToAction("ReportCoworkingRevenue");
            }

            // Buscar comissão da reserva
            ReservationCommission? commission = null;
            try
            {
                commission = await _context.ReservationCommissions
                    .FirstOrDefaultAsync(rc => rc.ReservationId == id);
            }
            catch (Exception)
            {
                // Se a tabela não existir ainda (migrations não aplicadas), continua sem comissão
                commission = null;
            }

            // Buscar transfer relacionado
            Transfer? transfer = null;
            try
            {
                transfer = await _context.Transfers
                    .FirstOrDefaultAsync(t => t.ReservationId == id);
            }
            catch (Exception)
            {
                // Se a tabela não existir ainda (migrations não aplicadas), continua sem transfer
                transfer = null;
            }

            // Buscar logs de auditoria relacionados
            List<AuditLog> auditLogs = new List<AuditLog>();
            try
            {
                auditLogs = await _context.AuditLogs
                    .Where(a => a.EntityType == "Reservation" && a.EntityId == id)
                    .OrderByDescending(a => a.ActionDate)
                    .ToListAsync();
            }
            catch (Exception)
            {
                // Se a tabela não existir ainda (migrations não aplicadas), continua sem logs
                auditLogs = new List<AuditLog>();
            }

            ViewBag.Reservation = reservation;
            ViewBag.Commission = commission;
            ViewBag.Transfer = transfer;
            ViewBag.AuditLogs = auditLogs;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SystemConfigurations()
        {
            // Inicializar configuração de menus de coworking se não existir
            var coworkingMenusConfig = await _systemConfigurationService.GetByKeyAsync("Features.EnableCoworkingMenus");
            if (coworkingMenusConfig == null)
            {
                await _systemConfigurationService.CreateOrUpdateAsync(
                    key: "Features.EnableCoworkingMenus",
                    value: "false",
                    description: "Habilita ou desabilita os menus de coworking (Reservar Sala, Reservas, etc.) para clientes e parceiros. Valores: true ou false",
                    category: "Features",
                    isEncrypted: false,
                    isSensitive: false,
                    updatedBy: User.Identity?.Name
                );
            }
            
            var configurations = await _systemConfigurationService.GetAllAsync();
            var groupedByCategory = configurations.GroupBy(c => c.Category).ToList();
            
            // Obter o provedor de storage atual
            var currentProvider = await _systemConfigurationService.GetValueAsync("Storage.Provider") ?? "AWS";
            
            // D4Sign: considerar configurado quando TokenAPI e SafeUuid preenchidos
            var d4Token = await _systemConfigurationService.GetValueAsync("D4Sign.TokenAPI");
            var d4Safe = await _systemConfigurationService.GetValueAsync("D4Sign.SafeUuid");
            ViewBag.D4SignConfigured = !string.IsNullOrWhiteSpace(d4Token) && !string.IsNullOrWhiteSpace(d4Safe);
            
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStorageToFTP()
        {
            try
            {
                // Atualizar Storage.Provider para FTP
                var storageProvider = await _context.SystemConfigurations
                    .FirstOrDefaultAsync(c => c.Key == "Storage.Provider");
                if (storageProvider != null)
                {
                    storageProvider.Value = "FTP";
                    storageProvider.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                    {
                        Key = "Storage.Provider",
                        Value = "FTP",
                        Description = "Provedor de armazenamento: AWS ou FTP",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Atualizar configurações FTP
                var ftpConfigs = new Dictionary<string, string>
                {
                    { "FTP.Host", "187.110.161.110" },
                    { "FTP.Port", "21" },
                    { "FTP.Username", "gestorhub" },
                    { "FTP.Password", "F@z3rd@rC3rt0" },
                    { "FTP.UsePassiveMode", "true" },
                    { "FTP.UseSSL", "false" }
                };

                foreach (var config in ftpConfigs)
                {
                    var existing = await _context.SystemConfigurations
                        .FirstOrDefaultAsync(c => c.Key == config.Key);
                    
                    if (existing != null)
                    {
                        existing.Value = config.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                        {
                            Key = config.Key,
                            Value = config.Value,
                            Description = config.Key switch
                            {
                                "FTP.Host" => "Endereço do servidor FTP",
                                "FTP.Port" => "Porta do servidor FTP (padrão: 21)",
                                "FTP.Username" => "Usuário para acesso ao servidor FTP",
                                "FTP.Password" => "Senha para acesso ao servidor FTP",
                                "FTP.UsePassiveMode" => "Usar modo passivo no FTP (recomendado para firewalls)",
                                "FTP.UseSSL" => "Usar SSL/TLS para conexão FTP (FTPS)",
                                _ => ""
                            },
                            Category = "Storage",
                            IsSensitive = config.Key == "FTP.Password",
                            IsEncrypted = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Configurações FTP atualizadas com sucesso! O sistema agora usa FTP como provedor de armazenamento.";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar configurações: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAddressDeclarationsTable()
        {
            try
            {
                // Verificar se a tabela já existe usando information_schema
                var tableExists = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    var wasOpen = connection.State == System.Data.ConnectionState.Open;
                    if (!wasOpen)
                    {
                        await connection.OpenAsync();
                    }
                    try
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = @"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = DATABASE() 
                            AND table_name = 'AddressDeclarations'";
                        var result = await command.ExecuteScalarAsync();
                        tableExists = Convert.ToInt32(result) > 0;
                    }
                    finally
                    {
                        if (!wasOpen)
                        {
                            await connection.CloseAsync();
                        }
                    }
                }
                catch
                {
                    // Se houver erro na verificação, tentar criar mesmo assim
                    tableExists = false;
                }

                if (!tableExists)
                {
                    // Criar a tabela AddressDeclarations
                    // Dividir em comandos menores para evitar problemas com múltiplos statements
                    var createTableSql = @"
                        CREATE TABLE IF NOT EXISTS `AddressDeclarations` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `ContractId` int NOT NULL,
                            `DeclarationNumber` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                            `Version` int NOT NULL,
                            `StartDate` datetime(6) NOT NULL,
                            `EndDate` datetime(6) NOT NULL,
                            `FilePath` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                            `DigitalSignature` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                            `IsValid` tinyint(1) NOT NULL,
                            `InvalidatedAt` datetime(6) NULL,
                            `InvalidationReason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                            `CreatedAt` datetime(6) NOT NULL,
                            `UpdatedAt` datetime(6) NULL,
                            PRIMARY KEY (`Id`),
                            CONSTRAINT `FK_AddressDeclarations_Contracts_ContractId` FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE,
                            INDEX `IX_AddressDeclarations_ContractId_Version` (`ContractId`, `Version`),
                            INDEX `IX_AddressDeclarations_DeclarationNumber` (`DeclarationNumber`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

                    await _context.Database.ExecuteSqlRawAsync(createTableSql);

                    TempData["Success"] = "Tabela AddressDeclarations criada com sucesso!";
                }
                else
                {
                    TempData["Info"] = "A tabela AddressDeclarations já existe no banco de dados.";
                }

                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurePagBankSandbox()
        {
            try
            {
                // Atualizar configurações do PagBank para Sandbox
                var configs = new Dictionary<string, string>
                {
                    { "PagBank.AccessToken", "db3453dd-8c96-46c8-9c91-b5549372ed51c6e68eb54829b960fbe67c1d261e73a27c75-0f2a-47d9-9fbe-da8211abbff4" },
                    { "PagBank.UseSandbox", "true" }
                };

                foreach (var config in configs)
                {
                    var existing = await _context.SystemConfigurations
                        .FirstOrDefaultAsync(c => c.Key == config.Key);
                    
                    if (existing != null)
                    {
                        existing.Value = config.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.UpdatedBy = User.Identity?.Name;
                    }
                    else
                    {
                        _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                        {
                            Key = config.Key,
                            Value = config.Value,
                            Description = config.Key switch
                            {
                                "PagBank.AccessToken" => "Token de acesso da API PagBank (Bearer Token)",
                                "PagBank.UseSandbox" => "Usar ambiente sandbox do PagBank (true/false)",
                                _ => ""
                            },
                            Category = "Payment",
                            IsSensitive = config.Key == "PagBank.AccessToken",
                            IsEncrypted = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Configurações do PagBank (Sandbox) atualizadas com sucesso!";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao configurar PagBank: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestPagBankConnection()
        {
            try
            {
                // Tentar fazer uma requisição simples para testar a conexão
                // Vamos tentar criar um pedido de teste muito pequeno (R$ 0,01)
                var testOrder = await _pagBankService.CreateOrderAsync(
                    referenceId: $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    amount: 0.01m,
                    method: PaymentMethod.PIX,
                    clientName: "Teste Conexão",
                    clientEmail: "teste@teste.com",
                    clientTaxId: "12345678909"
                );

                TempData["Success"] = $"Conexão com PagBank testada com sucesso! Pedido de teste criado: {testOrder.Id}";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao testar conexão PagBank: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
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

        [HttpGet]
        public async Task<IActionResult> DatabaseManager()
        {
            try
            {
                var pending = await _context.Database.GetPendingMigrationsAsync();
                ViewBag.PendingMigrations = pending.ToList();
                ViewBag.AppliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
            }
            catch
            {
                ViewBag.PendingMigrations = new List<string>();
                ViewBag.AppliedMigrations = new List<string>();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunMigrations()
        {
            try
            {
                // Corrige migrações órfãs (aplicadas parcialmente ou que dependem de tabelas opcionais)
                foreach (var m in new[] { "20260124120000_AddReservationDoubleConfirmAndPlatformCredits", "20260125120000_AddCreditPurchasePaymentColumns" })
                    await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ({0}, '8.0.0')", m);

                var pending = await _context.Database.GetPendingMigrationsAsync();
                var list = pending.ToList();
                if (list.Count == 0)
                {
                    TempData["Info"] = "Não há migrations pendentes. O banco já está atualizado.";
                    return RedirectToAction("DatabaseManager");
                }
                await _context.Database.MigrateAsync();
                TempData["Success"] = $"Migrations executadas com sucesso: {string.Join(", ", list)}";
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData["Error"] = "Erro ao executar migrations: " + msg;
            }
            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Verifica se a tabela CoworkingOperatingHours existe e se há dados (horários de funcionamento).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCoworkingOperatingHours()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();
                var sb = new System.Text.StringBuilder();

                try
                {
                    // 1. Verificar se a tabela existe
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM information_schema.tables 
                            WHERE table_schema = DATABASE() AND table_name = 'CoworkingOperatingHours'";
                        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                        sb.AppendLine(exists ? "[OK] Tabela CoworkingOperatingHours existe." : "[FALTA] Tabela CoworkingOperatingHours NÃO existe. Use 'Criar tabela' abaixo.");
                        sb.AppendLine();

                        if (!exists)
                        {
                            TempData["VerifyOperatingHoursResult"] = sb.ToString();
                            return RedirectToAction("DatabaseManager");
                        }
                    }

                    // 2. Contar registros
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM CoworkingOperatingHours";
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        sb.AppendLine($"Total de registros: {count}");
                        sb.AppendLine();
                    }

                    // 3. Amostra (até 20)
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Id, CoworkingId, DayOfWeek, OpeningTime, ClosingTime, IsActive, CreatedAt 
                            FROM CoworkingOperatingHours 
                            ORDER BY CoworkingId, DayOfWeek 
                            LIMIT 20";
                        await using var reader = await cmd.ExecuteReaderAsync();
                        if (!reader.HasRows)
                        {
                            sb.AppendLine("(Nenhum registro ainda. Os horários são gravados ao salvar em Parceiro > Meu Perfil > Horários de Funcionamento.)");
                        }
                        else
                        {
                            sb.AppendLine("Amostra (Id | CoworkingId | DayOfWeek | OpeningTime | ClosingTime | IsActive | CreatedAt):");
                            while (await reader.ReadAsync())
                            {
                                var id = reader.GetInt32(0);
                                var cid = reader.GetInt32(1);
                                var day = reader.GetString(2);
                                var op = reader.IsDBNull(3) ? "NULL" : reader.GetFieldValue<TimeSpan>(3).ToString(@"hh\:mm");
                                var cl = reader.IsDBNull(4) ? "NULL" : reader.GetFieldValue<TimeSpan>(4).ToString(@"hh\:mm");
                                var act = reader.GetBoolean(5);
                                var ca = reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm");
                                sb.AppendLine($"  {id} | {cid} | {day} | {op} | {cl} | {act} | {ca}");
                            }
                        }
                    }
                }
                finally
                {
                    if (!wasOpen) await connection.CloseAsync();
                }

                TempData["VerifyOperatingHoursResult"] = sb.ToString();
            }
            catch (Exception ex)
            {
                TempData["VerifyOperatingHoursResult"] = $"[ERRO] {ex.Message}";
                if (ex.InnerException != null)
                    TempData["VerifyOperatingHoursResult"] += "\r\n" + ex.InnerException.Message;
            }

            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Cria a tabela CoworkingOperatingHours (horários de funcionamento) se não existir. Execução manual pelo admin.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingOperatingHoursTable()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

                var tableExists = false;
                try
                {
                    await using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = @"
                        SELECT COUNT(*) FROM information_schema.tables 
                        WHERE table_schema = DATABASE() AND table_name = 'CoworkingOperatingHours'";
                    tableExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                }
                catch { tableExists = false; }
                finally { if (!wasOpen) await connection.CloseAsync(); }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela CoworkingOperatingHours já existe. Nenhuma alteração feita.";
                    return RedirectToAction("DatabaseManager");
                }

                var createSql = @"
                    CREATE TABLE IF NOT EXISTS CoworkingOperatingHours (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        CoworkingId INT NOT NULL,
                        DayOfWeek VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        OpeningTime TIME(6) NULL,
                        ClosingTime TIME(6) NULL,
                        IsActive TINYINT(1) NOT NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        CONSTRAINT FK_CoworkingOperatingHours_Coworkings_CoworkingId 
                            FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
                        UNIQUE KEY IX_CoworkingOperatingHours_CoworkingId_DayOfWeek (CoworkingId, DayOfWeek)
                    ) CHARACTER SET utf8mb4";
                await _context.Database.ExecuteSqlRawAsync(createSql);

                TempData["Success"] = "Tabela CoworkingOperatingHours criada com sucesso. Os horários de funcionamento passam a ser persistidos ao salvar no perfil do parceiro.";
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData["Error"] = "Erro ao criar CoworkingOperatingHours: " + msg;
            }

            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Adiciona a coluna D4SignDocumentUuid na tabela Documents se não existir (D4Sign / assinatura eletrônica).
        /// Corrige o erro "Unknown column 'd.D4SignDocumentUuid' in 'field list'" em PaymentDetails e outros.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddD4SignDocumentUuidColumn()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

                var columnExists = false;
                try
                {
                    await using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = @"
                        SELECT COUNT(*) FROM information_schema.columns 
                        WHERE table_schema = DATABASE() AND table_name = 'Documents' AND column_name = 'D4SignDocumentUuid'";
                    columnExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                }
                catch { columnExists = false; }
                finally { if (!wasOpen) await connection.CloseAsync(); }

                if (columnExists)
                {
                    TempData["Info"] = "A coluna Documents.D4SignDocumentUuid já existe. Nenhuma alteração feita.";
                    return RedirectToAction("DatabaseManager");
                }

                const string alterSql = @"ALTER TABLE Documents ADD COLUMN D4SignDocumentUuid VARCHAR(64) NULL CHARACTER SET utf8mb4;";
                await _context.Database.ExecuteSqlRawAsync(alterSql);

                TempData["Success"] = "Coluna Documents.D4SignDocumentUuid criada com sucesso. O erro em PaymentDetails/contratos deve estar resolvido.";
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData["Error"] = "Erro ao adicionar coluna D4SignDocumentUuid: " + msg;
            }

            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Cria índices de desempenho no banco (MySQL). Idempotente: ignora índices que já existem.
        /// Melhora consultas em Reservations, CreditPurchases, ClientCredits, Payments, Contracts, Coworkings, etc.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyIndexes()
        {
            var created = new List<string>();
            var indexes = new (string Name, string Sql)[]
            {
                ("IX_Reservations_CoworkingAddressId_ReservationDate", "CREATE INDEX IX_Reservations_CoworkingAddressId_ReservationDate ON Reservations (CoworkingAddressId, ReservationDate)"),
                ("IX_Reservations_ClientId_Status", "CREATE INDEX IX_Reservations_ClientId_Status ON Reservations (ClientId, Status)"),
                ("IX_Reservations_ReservationDate_Status", "CREATE INDEX IX_Reservations_ReservationDate_Status ON Reservations (ReservationDate, Status)"),
                ("IX_CreditPurchases_ClientId_Status", "CREATE INDEX IX_CreditPurchases_ClientId_Status ON CreditPurchases (ClientId, Status)"),
                ("IX_CreditPurchases_Status", "CREATE INDEX IX_CreditPurchases_Status ON CreditPurchases (Status)"),
                ("IX_CreditPurchases_PaidAt", "CREATE INDEX IX_CreditPurchases_PaidAt ON CreditPurchases (PaidAt)"),
                ("IX_CreditPurchases_CreatedAt", "CREATE INDEX IX_CreditPurchases_CreatedAt ON CreditPurchases (CreatedAt)"),
                ("IX_ClientCredits_ClientId_ExpiresAt", "CREATE INDEX IX_ClientCredits_ClientId_ExpiresAt ON ClientCredits (ClientId, ExpiresAt)"),
                ("IX_Payments_SubscriptionId_Status", "CREATE INDEX IX_Payments_SubscriptionId_Status ON Payments (SubscriptionId, Status(100))"),
                ("IX_Payments_Status", "CREATE INDEX IX_Payments_Status ON Payments (Status(100))"),
                ("IX_Payments_PaidAt", "CREATE INDEX IX_Payments_PaidAt ON Payments (PaidAt)"),
                ("IX_Contracts_Status", "CREATE INDEX IX_Contracts_Status ON Contracts (Status(100))"),
                ("IX_Coworkings_ApprovalStatus", "CREATE INDEX IX_Coworkings_ApprovalStatus ON Coworkings (ApprovalStatus)"),
                ("IX_Coworkings_IsActive_IsPartner", "CREATE INDEX IX_Coworkings_IsActive_IsPartner ON Coworkings (IsActive, IsPartner)"),
                ("IX_Subscriptions_Status", "CREATE INDEX IX_Subscriptions_Status ON Subscriptions (Status(100))"),
                ("IX_Transfers_CoworkingId", "CREATE INDEX IX_Transfers_CoworkingId ON Transfers (CoworkingId)"),
                ("IX_Interests_CoworkingAddressId", "CREATE INDEX IX_Interests_CoworkingAddressId ON Interests (CoworkingAddressId)"),
                ("IX_Commissions_PaymentId", "CREATE INDEX IX_Commissions_PaymentId ON Commissions (PaymentId)"),
                ("IX_Commissions_CoworkingId", "CREATE INDEX IX_Commissions_CoworkingId ON Commissions (CoworkingId)"),
                ("IX_ReservationCommissions_ReservationId", "CREATE INDEX IX_ReservationCommissions_ReservationId ON ReservationCommissions (ReservationId)"),
                ("IX_CoworkingRooms_CoworkingAddressId", "CREATE INDEX IX_CoworkingRooms_CoworkingAddressId ON CoworkingRooms (CoworkingAddressId)"),
                ("IX_Documents_ContractId", "CREATE INDEX IX_Documents_ContractId ON Documents (ContractId)"),
            };

            foreach (var (name, sql) in indexes)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(sql);
                    created.Add(name);
                }
                catch (Exception ex)
                {
                    var msg = (ex.InnerException?.Message ?? ex.Message) ?? "";
                    if (msg.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (msg.IndexOf("doesn't exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("Unknown table", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("Unknown column", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    TempData["Error"] = $"Erro ao criar índice {name}: {ex.Message}";
                    return RedirectToAction("DatabaseManager");
                }
            }

            if (created.Count > 0)
                TempData["Success"] = $"{created.Count} índice(s) criado(s) para desempenho: {string.Join(", ", created)}";
            else
                TempData["Info"] = "Todos os índices já existem. Nenhuma alteração necessária.";
            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Adiciona colunas de confirmação dupla (ClientConfirmedAt, CoworkingConfirmedAt em Reservations;
        /// ReservationId em Transfers) quando a migração AddReservationDoubleConfirmAndPlatformCredits não foi aplicada.
        /// Idempotente: ignora se a coluna já existir.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixReservationAndTransferColumns()
        {
            var added = new List<string>();
            var alters = new[]
            {
                ("Reservations", "ClientConfirmedAt", "ALTER TABLE Reservations ADD COLUMN ClientConfirmedAt datetime(6) NULL"),
                ("Reservations", "CoworkingConfirmedAt", "ALTER TABLE Reservations ADD COLUMN CoworkingConfirmedAt datetime(6) NULL"),
                ("Transfers", "ReservationId", "ALTER TABLE Transfers ADD COLUMN ReservationId int NULL")
            };
            foreach (var (table, column, sql) in alters)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(sql);
                    added.Add($"{table}.{column}");
                }
                catch (Exception ex) when (ex.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true) { }
            }
            // Índice e FK em Transfers.ReservationId (idempotente: ignora se já existirem)
            try { await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IX_Transfers_ReservationId ON Transfers (ReservationId)"); added.Add("IX_Transfers_ReservationId"); } catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            try { await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Transfers ADD CONSTRAINT FK_Transfers_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL"); added.Add("FK_Transfers_Reservations_ReservationId"); } catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            if (added.Count > 0)
                TempData["Success"] = "Colunas/índice/FK adicionados: " + string.Join(", ", added);
            else
                TempData["Info"] = "As colunas já existem. Nenhuma alteração necessária.";
            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Adiciona colunas de pagamento (PIX, Boleto, Cartão) em CreditPurchases quando a migração
        /// AddCreditPurchasePaymentColumns não foi aplicada. Resolve erro "Unknown column 'c.BoletoBarcode'".
        /// Idempotente: ignora se a coluna já existir.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixCreditPurchaseColumns()
        {
            var added = new List<string>();
            var alters = new[]
            {
                ("CreditPurchases", "PaymentMethod", "ALTER TABLE CreditPurchases ADD COLUMN PaymentMethod varchar(50) NULL"),
                ("CreditPurchases", "ExternalPaymentId", "ALTER TABLE CreditPurchases ADD COLUMN ExternalPaymentId varchar(255) NULL"),
                ("CreditPurchases", "PixCode", "ALTER TABLE CreditPurchases ADD COLUMN PixCode varchar(500) NULL"),
                ("CreditPurchases", "PixExpiresAt", "ALTER TABLE CreditPurchases ADD COLUMN PixExpiresAt datetime(6) NULL"),
                ("CreditPurchases", "BoletoCode", "ALTER TABLE CreditPurchases ADD COLUMN BoletoCode varchar(100) NULL"),
                ("CreditPurchases", "BoletoBarcode", "ALTER TABLE CreditPurchases ADD COLUMN BoletoBarcode varchar(100) NULL"),
                ("CreditPurchases", "BoletoExpiresAt", "ALTER TABLE CreditPurchases ADD COLUMN BoletoExpiresAt datetime(6) NULL"),
                ("CreditPurchases", "TransactionId", "ALTER TABLE CreditPurchases ADD COLUMN TransactionId varchar(255) NULL")
            };
            foreach (var (table, column, sql) in alters)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(sql);
                    added.Add($"{table}.{column}");
                }
                catch (Exception ex) when (ex.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true) { }
            }
            if (added.Count > 0)
                TempData["Success"] = "Colunas de CreditPurchases adicionadas: " + string.Join(", ", added);
            else
                TempData["Info"] = "As colunas de CreditPurchases já existem. Nenhuma alteração necessária.";
            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInterestsTable()
        {
            try
            {
                // Verificar se a tabela já existe
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'Interests'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    // Se der erro ao verificar, assume que não existe e tenta criar
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela Interests já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS Interests (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingAddressId INT NOT NULL,
    AllocationDate DATETIME NULL,
    StartTime TIME NULL,
    EndTime TIME NULL,
    Name VARCHAR(255) NOT NULL,
    LastName VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    Phone VARCHAR(50) NULL,
    Status VARCHAR(50) DEFAULT 'Pending',
    Notes TEXT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    ContactedByUserId INT NULL,
    ContactedAt DATETIME NULL,
    FOREIGN KEY (CoworkingAddressId) REFERENCES CoworkingAddresses(Id) ON DELETE CASCADE,
    FOREIGN KEY (ContactedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela Interests criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela Interests: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingProductsTable()
        {
            try
            {
                // Verificar se a tabela já existe
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'CoworkingProducts'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    // Se der erro ao verificar, assume que não existe e tenta criar
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela CoworkingProducts já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS CoworkingProducts (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingId INT NOT NULL,
    ProductType VARCHAR(50) NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
    UNIQUE KEY UK_CoworkingProduct (CoworkingId, ProductType)
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela CoworkingProducts criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingProducts: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearDatabase()
        {
            try
            {
                // Desabilitar foreign keys temporariamente
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                // Lista completa de tabelas para limpar (na ordem correta - dependentes primeiro)
                // Preservar: Roles, UserRoles (do admin), SystemConfigurations, Languages, Locations, Services, Users (admin)
                var tablesToClear = new[]
                {
                    // Tabelas dependentes primeiro
                    "AuditLogs",
                    "ReservationCommissions",
                    "Commissions",
                    "Transfers",
                    "FiscalNotes",
                    "Reservations",
                    "Payments",
                    "Subscriptions",
                    "Contracts",
                    "AddressDeclarations",
                    "CoworkingRoomPhotos",
                    "CoworkingRooms",
                    "CoworkingPhotos",
                    "CoworkingDocuments",
                    "CoworkingOperatingHours",
                    "BlockedDates",
                    "CoworkingPartners",
                    "CoworkingAddresses",
                    "PartnerServices",
                    "LocationCommissions",
                    "PlatformCreditBalances",
                    "Coworkings",
                    "Clients",
                    "CreditPurchases",
                    "ClientCredits",
                    "CreditPlans",
                    "CreditCards",
                    "Documents",
                    "Interests",
                    "CoworkingProducts",
                    "CoworkingRatings",
                    "Translations"
                };

                var clearedTables = new List<string>();
                var errors = new List<string>();

                // Preservar apenas o usuário admin e seus roles antes de limpar
                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@klyon.com");
                var adminUserRolePairs = new List<(int UserId, int RoleId)>();
                
                if (adminUser != null)
                {
                    var adminRoles = await _context.UserRoles
                        .Where(ur => ur.UserId == adminUser.Id)
                        .ToListAsync();
                    
                    adminUserRolePairs = adminRoles.Select(ur => (ur.UserId, ur.RoleId)).ToList();
                }

                // Limpar todas as tabelas usando uma abordagem mais robusta
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                foreach (var tableName in tablesToClear)
                {
                    try
                    {
                        // Verificar se a tabela existe
                        using var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = DATABASE() 
                            AND table_name = '{tableName}'";
                        
                        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                        
                        if (exists)
                        {
                            // Limpar dados da tabela usando DELETE (mais seguro que TRUNCATE com foreign keys)
                            using var deleteCmd = connection.CreateCommand();
                            deleteCmd.CommandText = $"DELETE FROM `{tableName}`;";
                            var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync();
                            clearedTables.Add($"{tableName} ({rowsDeleted} registros)");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Tentar TRUNCATE se DELETE falhar
                        try
                        {
                            using var truncateCmd = connection.CreateCommand();
                            truncateCmd.CommandText = $"TRUNCATE TABLE `{tableName}`;";
                            await truncateCmd.ExecuteNonQueryAsync();
                            clearedTables.Add($"{tableName} (truncate)");
                        }
                        catch (Exception ex2)
                        {
                            errors.Add($"{tableName}: {ex2.Message}");
                            System.Diagnostics.Debug.WriteLine($"Erro ao limpar tabela {tableName}: {ex2.Message}");
                        }
                    }
                }
                
                await connection.CloseAsync();

                // Limpar UserRoles e Users preservando o admin
                if (adminUser != null && adminUserRolePairs.Any())
                {
                    // Remover todos os UserRoles exceto os do admin
                    var roleConditions = string.Join(" OR ", adminUserRolePairs.Select(pair => $"(UserId = {pair.UserId} AND RoleId = {pair.RoleId})"));
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            $"DELETE FROM UserRoles WHERE NOT ({roleConditions});");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"UserRoles: {ex.Message}");
                    }
                    
                    // Remover todos os usuários exceto o admin
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            $"DELETE FROM Users WHERE Id != {adminUser.Id};");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Users: {ex.Message}");
                    }
                }
                else
                {
                    // Se não houver admin, limpar todos os usuários e UserRoles
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM UserRoles;");
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users;");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Users/UserRoles: {ex.Message}");
                    }
                }

                // Reabilitar foreign keys
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");

                // Executar seed novamente para garantir que dados essenciais existam
                var seedService = new Application.Services.SeedService(_context);
                await seedService.SeedInitialDataAsync();

                var successMessage = $"Base de dados limpa com sucesso! {clearedTables.Count} tabela(s) foram limpas. Dados essenciais (Roles, Configurações, Localidades, Serviços) foram preservados.";
                if (errors.Any())
                {
                    successMessage += $" Avisos: {string.Join("; ", errors.Take(3))}";
                }
                TempData["Success"] = successMessage;
            }
            catch (Exception ex)
            {
                // Reabilitar foreign keys em caso de erro
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                }
                catch { }

                TempData["Error"] = $"Erro ao limpar base de dados: {ex.Message}";
                if (ex.InnerException != null)
                {
                    TempData["Error"] += $" Detalhes: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteSql(string sqlQuery)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                TempData["Error"] = "A query SQL não pode estar vazia.";
                return RedirectToAction("DatabaseManager");
            }

            // Validações de segurança básicas
            var upperQuery = sqlQuery.ToUpper().Trim();
            
            // Bloquear operações perigosas
            var blockedKeywords = new[] { "DROP DATABASE", "DROP TABLE", "TRUNCATE", "DELETE FROM", "UPDATE", "ALTER DATABASE" };
            if (blockedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData["Error"] = "Operações perigosas não são permitidas. Use apenas ALTER TABLE, CREATE TABLE, ADD COLUMN ou SELECT.";
                return RedirectToAction("DatabaseManager");
            }
            
            // Permitir apenas operações seguras
            var allowedKeywords = new[] { "ALTER TABLE", "CREATE TABLE", "CREATE INDEX", "ADD COLUMN", "ADD", "SELECT", "SHOW", "DESCRIBE", "DESC" };
            if (!allowedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData["Error"] = "Apenas operações ALTER TABLE, CREATE TABLE, CREATE INDEX, ADD COLUMN, SELECT, SHOW e DESCRIBE são permitidas.";
                return RedirectToAction("DatabaseManager");
            }

            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sqlQuery;
                command.CommandTimeout = 300; // 5 minutos

                var result = new System.Text.StringBuilder();
                var rowsAffected = 0;

                // Verificar se é uma query SELECT, SHOW ou DESCRIBE
                if (upperQuery.TrimStart().StartsWith("SELECT") || 
                    upperQuery.TrimStart().StartsWith("SHOW") || 
                    upperQuery.TrimStart().StartsWith("DESCRIBE") ||
                    upperQuery.TrimStart().StartsWith("DESC"))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    
                    if (reader.HasRows)
                    {
                        // Cabeçalho das colunas
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }
                        result.AppendLine(string.Join(" | ", columnNames));
                        result.AppendLine(new string('-', Math.Min(200, columnNames.Sum(c => c.Length) + (columnNames.Count * 3))));

                        // Dados (limitar a 1000 linhas para não sobrecarregar)
                        int rowCount = 0;
                        while (await reader.ReadAsync() && rowCount < 1000)
                        {
                            var row = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL";
                                // Limitar tamanho de cada campo para não quebrar a visualização
                                if (value.Length > 50)
                                {
                                    value = value.Substring(0, 47) + "...";
                                }
                                row.Add(value);
                            }
                            result.AppendLine(string.Join(" | ", row));
                            rowCount++;
                        }
                        
                        if (rowCount >= 1000)
                        {
                            result.AppendLine($"\n... (limitado a 1000 linhas)");
                        }
                    }
                    else
                    {
                        result.AppendLine("Nenhum resultado encontrado.");
                    }
                }
                else
                {
                    // Para comandos não-SELECT (ALTER, CREATE, etc.)
                    rowsAffected = await command.ExecuteNonQueryAsync();
                    result.AppendLine($"Comando executado com sucesso!");
                    result.AppendLine($"Linhas afetadas: {rowsAffected}");
                }

                await connection.CloseAsync();

                TempData["Success"] = "Query executada com sucesso!";
                TempData["QueryResult"] = result.ToString();
                TempData["ExecutedQuery"] = sqlQuery;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao executar query: {ex.Message}";
                TempData["ExecutedQuery"] = sqlQuery;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingRatingsTable()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'CoworkingRatings'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela CoworkingRatings já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS CoworkingRatings (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingId INT NOT NULL,
    ClientId INT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Comment VARCHAR(1000) NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela CoworkingRatings criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingRatings: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCreditsTables()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                var createTablesSql = @"
CREATE TABLE IF NOT EXISTS ClientCredits (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    Credits INT NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS CreditPlans (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    Credits INT NOT NULL,
    BonusCredits INT DEFAULT 0,
    Period VARCHAR(50) NOT NULL,
    ValidityMonths INT NOT NULL DEFAULT 6,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

CREATE TABLE IF NOT EXISTS CreditPurchases (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    CreditPlanId INT NOT NULL,
    AmountPaid DECIMAL(10,2) NOT NULL,
    CreditsReceived INT NOT NULL,
    BonusCredits INT DEFAULT 0,
    Status VARCHAR(50) DEFAULT 'Pending',
    PaymentMethod VARCHAR(50) NULL,
    ExternalPaymentId VARCHAR(255) NULL,
    PixCode VARCHAR(500) NULL,
    PixExpiresAt DATETIME NULL,
    BoletoCode VARCHAR(100) NULL,
    BoletoBarcode VARCHAR(100) NULL,
    BoletoExpiresAt DATETIME NULL,
    TransactionId VARCHAR(255) NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PaidAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE,
    FOREIGN KEY (CreditPlanId) REFERENCES CreditPlans(Id)
);

CREATE TABLE IF NOT EXISTS Reservations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    CoworkingAddressId INT NOT NULL,
    ReservationDate DATETIME NOT NULL,
    StartTime TIME NULL,
    EndTime TIME NULL,
    CreditsUsed INT NOT NULL,
    Status VARCHAR(50) DEFAULT 'Confirmed',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CancelledAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE,
    FOREIGN KEY (CoworkingAddressId) REFERENCES CoworkingAddresses(Id) ON DELETE CASCADE
);";

                await _context.Database.ExecuteSqlRawAsync(createTablesSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabelas de créditos criadas com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabelas de créditos: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeCreditPlans()
        {
            try
            {
                // Verificar se já existem planos
                var existingPlans = await _context.CreditPlans.AnyAsync();
                if (existingPlans)
                {
                    TempData["Info"] = "Os planos de créditos já foram inicializados.";
                    return RedirectToAction("DatabaseManager");
                }

                // Criar planos mensais
                var plans = new List<Domain.Entities.CreditPlan>
                {
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Básico",
                        Price = 300m,
                        Credits = 300,
                        BonusCredits = 0,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    },
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Profissional",
                        Price = 500m,
                        Credits = 520,
                        BonusCredits = 20,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    },
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Business",
                        Price = 1000m,
                        Credits = 1080,
                        BonusCredits = 80,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    }
                };

                _context.CreditPlans.AddRange(plans);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Planos de créditos inicializados com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao inicializar planos: {ex.Message}";
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        public async Task<IActionResult> AddRoomConfigurationColumns()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                // Verificar e adicionar cada coluna individualmente
                var columns = new[]
                {
                    ("OffersSharedRoom", "BOOLEAN DEFAULT FALSE"),
                    ("SharedRoomPricePerHour", "DECIMAL(10,2) NULL"),
                    ("SharedRoomMaxSeats", "INT NULL"),
                    ("OffersPrivateRoom", "BOOLEAN DEFAULT FALSE"),
                    ("PrivateRoomPricePerHour", "DECIMAL(10,2) NULL"),
                    ("PrivateRoomMaxSeats", "INT NULL")
                };

                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        // Verificar se a coluna já existe
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.COLUMNS 
                            WHERE TABLE_SCHEMA = DATABASE() 
                            AND TABLE_NAME = 'CoworkingAddresses' 
                            AND COLUMN_NAME = '{columnName}'";
                        
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                        
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE CoworkingAddresses ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignorar se a coluna já existe ou outro erro específico
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }

                await connection.CloseAsync();
                TempData["Success"] = "Colunas de configuração de salas adicionadas com sucesso na tabela CoworkingAddresses!";
                return RedirectToAction("DatabaseManager");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar colunas: {ex.Message}";
                return RedirectToAction("DatabaseManager");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddReservationColumns()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                var columns = new[]
                {
                    ("RoomType", "VARCHAR(20) DEFAULT 'Shared'"),
                    ("TotalValue", "DECIMAL(10,2) NOT NULL DEFAULT 0")
                };

                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        // Verificar se a coluna já existe
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.COLUMNS 
                            WHERE TABLE_SCHEMA = DATABASE() 
                            AND TABLE_NAME = 'Reservations' 
                            AND COLUMN_NAME = '{columnName}'";
                        
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                        
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE Reservations ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignorar se a coluna já existe
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }

                await connection.CloseAsync();
                TempData["Success"] = "Colunas RoomType e TotalValue adicionadas com sucesso na tabela Reservations!";
                return RedirectToAction("DatabaseManager");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar colunas: {ex.Message}";
                return RedirectToAction("DatabaseManager");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePartnerService(int Id, int CoworkingId, int ServiceId)
        {
            try
            {
                // Parse da percentagem: aceita vírgula ou ponto (ex.: 12,5 ou 12.5)
                var percentageStr = (Request.Form["Percentage"].ToString() ?? "").Trim().Replace(",", ".");
                if (!decimal.TryParse(percentageStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var percentage))
                {
                    TempData["Error"] = "Percentagem inválida. Use números decimais (ex.: 12,5 ou 12.5).";
                    return RedirectToAction("ViewCoworking", new { id = CoworkingId });
                }

                // Validar se o coworking existe
                var coworking = await _context.Coworkings.FindAsync(CoworkingId);
                if (coworking == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Coworkings");
                }

                // Validar se o serviço existe
                var service = await _context.Services.FindAsync(ServiceId);
                if (service == null)
                {
                    TempData["Error"] = "Serviço não encontrado.";
                    return RedirectToAction("ViewCoworking", new { id = CoworkingId });
                }

                // Validar percentagem
                if (percentage < 0 || percentage > 100)
                {
                    TempData["Error"] = "A percentagem deve estar entre 0 e 100.";
                    return RedirectToAction("ViewCoworking", new { id = CoworkingId });
                }

                if (Id > 0)
                {
                    // Editar existente
                    var existing = await _context.PartnerServices.FindAsync(Id);
                    if (existing == null || existing.CoworkingId != CoworkingId)
                    {
                        TempData["Error"] = "Serviço do parceiro não encontrado.";
                        return RedirectToAction("ViewCoworking", new { id = CoworkingId });
                    }

                    existing.ServiceId = ServiceId;
                    existing.Percentage = percentage;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Verificar se já existe este serviço para este parceiro
                    var existing = await _context.PartnerServices
                        .FirstOrDefaultAsync(ps => ps.CoworkingId == CoworkingId && ps.ServiceId == ServiceId);

                    if (existing != null)
                    {
                        TempData["Error"] = "Este serviço já está cadastrado para este parceiro.";
                        return RedirectToAction("ViewCoworking", new { id = CoworkingId });
                    }

                    // Criar novo
                    var partnerService = new PartnerService
                    {
                        CoworkingId = CoworkingId,
                        ServiceId = ServiceId,
                        Percentage = percentage,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.PartnerServices.Add(partnerService);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Serviço salvo com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao salvar serviço: {ex.Message}";
            }

            return RedirectToAction("ViewCoworking", new { id = CoworkingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartnerService(int id, int coworkingId)
        {
            try
            {
                var partnerService = await _context.PartnerServices.FindAsync(id);
                if (partnerService == null || partnerService.CoworkingId != coworkingId)
                {
                    TempData["Error"] = "Serviço do parceiro não encontrado.";
                    return RedirectToAction("ViewCoworking", new { id = coworkingId });
                }

                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Serviço removido com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao remover serviço: {ex.Message}";
            }

            return RedirectToAction("ViewCoworking", new { id = coworkingId });
        }

        [HttpGet]
        public async Task<IActionResult> SefazConfiguration(string? state = null)
        {
            // Lista de estados brasileiros
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };

            // Estados com integração implementada
            ViewBag.SupportedStates = new List<string> { "ES" };

            // Se não especificado, usar ES como padrão
            var selectedState = state ?? "ES";

            // Buscar configuração existente para o estado selecionado
            var config = await _context.SefazConfigurations
                .Where(c => c.State == selectedState)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new SefazConfiguration
                {
                    State = selectedState,
                    Environment = "Homologacao",
                    IntermediationServiceDescription = "Intermediação de serviços",
                    ISSAliquot = 5.0m,
                    TimeoutSeconds = 30,
                    IsActive = false
                };
            }

            ViewBag.SelectedState = selectedState;
            ViewBag.IsStateSupported = _sefazService.IsStateSupported(selectedState);

            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSefazConfiguration(SefazConfiguration config)
        {
            try
            {
                // Normalizar estado para maiúsculas
                if (!string.IsNullOrWhiteSpace(config.State))
                {
                    config.State = config.State.Trim().ToUpper();
                }
                else
                {
                    config.State = "ES"; // Padrão
                }

                if (config.Id > 0)
                {
                    // Atualizar existente
                    var existing = await _context.SefazConfigurations.FindAsync(config.Id);
                    if (existing != null)
                    {
                        existing.State = config.State;
                        existing.Environment = config.Environment;
                        existing.PlatformCNPJ = config.PlatformCNPJ;
                        existing.PlatformMunicipalRegistration = config.PlatformMunicipalRegistration;
                        existing.CertificatePath = config.CertificatePath;
                        existing.CertificatePassword = config.CertificatePassword;
                        if (!string.IsNullOrEmpty(config.CertificateContent))
                        {
                            existing.CertificateContent = config.CertificateContent;
                        }
                        existing.IntermediationServiceCode = config.IntermediationServiceCode;
                        existing.IntermediationServiceDescription = config.IntermediationServiceDescription;
                        existing.PlatformCNAE = config.PlatformCNAE;
                        existing.TaxRegime = config.TaxRegime;
                        existing.ISSAliquot = config.ISSAliquot;
                        existing.ISSMunicipality = config.ISSMunicipality;
                        existing.HomologationUrl = config.HomologationUrl;
                        existing.ProductionUrl = config.ProductionUrl;
                        existing.ApiToken = config.ApiToken;
                        existing.ApiKey = config.ApiKey;
                        existing.TimeoutSeconds = config.TimeoutSeconds;
                        existing.IsActive = config.IsActive;
                        existing.Notes = config.Notes;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Criar novo
                    config.CreatedAt = DateTime.UtcNow;
                    _context.SefazConfigurations.Add(config);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Configuração da SEFAZ ES salva com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao salvar configuração: {ex.Message}";
            }

            return RedirectToAction("SefazConfiguration");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteFiscalNoteScript(string noteType, DateTime startDate, DateTime endDate)
        {
            try
            {
                int notesEmitted = 0;
                int errors = 0;
                int unsupportedStates = 0;
                var errorMessages = new List<string>();
                var unsupportedStateMessages = new List<string>();

                if (noteType == "Intermediacao")
                {
                    // Buscar reservas confirmadas e pagas que ainda não têm NF de intermediação
                    var reservations = await _context.Reservations
                        .Include(r => r.Client)
                        .Include(r => r.CoworkingAddress)
                            .ThenInclude(a => a.Coworking)
                        .Where(r => r.Status == "Completed" &&
                                   r.ReservationDate >= startDate &&
                                   r.ReservationDate <= endDate &&
                                   !_context.FiscalNotes.Any(fn => fn.ReservationId == r.Id && fn.Type == "Intermediacao"))
                        .ToListAsync();

                    foreach (var reservation in reservations)
                    {
                        try
                        {
                            // Obter estado do coworking
                            var coworkingState = reservation.CoworkingAddress?.State?.Trim().ToUpper();
                            
                            if (string.IsNullOrWhiteSpace(coworkingState))
                            {
                                errors++;
                                errorMessages.Add($"Reserva #{reservation.Id}: Estado do coworking não informado.");
                                continue;
                            }

                            // Verificar se o estado tem integração implementada
                            if (!_sefazService.IsStateSupported(coworkingState))
                            {
                                unsupportedStates++;
                                unsupportedStateMessages.Add($"Reserva #{reservation.Id}: Integração SEFAZ ainda não implementada para o estado {coworkingState}.");
                                continue;
                            }

                            // Buscar configuração SEFAZ para o estado
                            var config = await _sefazService.GetConfigurationByStateAsync(coworkingState);
                            
                            if (config == null)
                            {
                                errors++;
                                errorMessages.Add($"Reserva #{reservation.Id}: Configuração SEFAZ não encontrada para o estado {coworkingState}. Configure na tela de parametrização.");
                                continue;
                            }

                            // Buscar percentagem de comissão do parceiro para este serviço
                            var partnerService = await _context.PartnerServices
                                .Include(ps => ps.Service)
                                .FirstOrDefaultAsync(ps => ps.CoworkingId == reservation.CoworkingAddress.CoworkingId &&
                                                          ps.Service.Name.Contains("Reservar Sala"));

                            if (partnerService == null)
                            {
                                // Usar percentagem padrão se não houver configuração específica
                                partnerService = await _context.PartnerServices
                                    .Include(ps => ps.Service)
                                    .FirstOrDefaultAsync(ps => ps.CoworkingId == reservation.CoworkingAddress.CoworkingId);
                            }

                            if (partnerService == null)
                            {
                                errors++;
                                errorMessages.Add($"Reserva #{reservation.Id}: Percentagem de comissão não configurada para o parceiro.");
                                continue;
                            }

                            // Calcular comissão
                            var commissionAmount = Math.Round(reservation.TotalValue * (partnerService.Percentage / 100m), 2, MidpointRounding.AwayFromZero);

                            // Buscar pagamento relacionado à reserva
                            var payment = await _context.Payments
                                .FirstOrDefaultAsync(p => p.ReservationId == reservation.Id && p.Status == PaymentStatus.Paid);

                            // Criar registro de NF (a emissão real será feita pelo serviço SEFAZ)
                            var fiscalNote = new FiscalNote
                            {
                                Type = "Intermediacao",
                                ReservationId = reservation.Id,
                                PaymentId = payment?.Id,
                                CoworkingId = reservation.CoworkingAddress.CoworkingId,
                                ClientId = reservation.ClientId,
                                Amount = commissionAmount,
                                CommissionAmount = commissionAmount,
                                ServiceAmount = reservation.TotalValue,
                                IssueDate = DateTime.UtcNow,
                                Status = "Pendente",
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.FiscalNotes.Add(fiscalNote);
                            notesEmitted++;
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            errorMessages.Add($"Reserva #{reservation.Id}: {ex.Message}");
                        }
                    }

                    // Buscar contratos ativos que ainda não têm NF de intermediação
                    var contracts = await _context.Contracts
                        .Include(c => c.Client)
                        .Include(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Coworking)
                        .Where(c => c.Status == ContractStatus.Active &&
                                   c.StartDate >= startDate &&
                                   c.StartDate <= endDate &&
                                   !_context.FiscalNotes.Any(fn => fn.ContractId == c.Id && fn.Type == "Intermediacao"))
                        .ToListAsync();

                    foreach (var contract in contracts)
                    {
                        try
                        {
                            // Obter estado do coworking
                            var coworkingState = contract.CoworkingAddress?.State?.Trim().ToUpper();
                            
                            if (string.IsNullOrWhiteSpace(coworkingState))
                            {
                                errors++;
                                errorMessages.Add($"Contrato #{contract.Id}: Estado do coworking não informado.");
                                continue;
                            }

                            // Verificar se o estado tem integração implementada
                            if (!_sefazService.IsStateSupported(coworkingState))
                            {
                                unsupportedStates++;
                                unsupportedStateMessages.Add($"Contrato #{contract.Id}: Integração SEFAZ ainda não implementada para o estado {coworkingState}.");
                                continue;
                            }

                            // Buscar configuração SEFAZ para o estado
                            var config = await _sefazService.GetConfigurationByStateAsync(coworkingState);
                            
                            if (config == null)
                            {
                                errors++;
                                errorMessages.Add($"Contrato #{contract.Id}: Configuração SEFAZ não encontrada para o estado {coworkingState}. Configure na tela de parametrização.");
                                continue;
                            }

                            // Buscar percentagem de comissão do parceiro para endereço fiscal
                            var partnerService = await _context.PartnerServices
                                .Include(ps => ps.Service)
                                .FirstOrDefaultAsync(ps => ps.CoworkingId == contract.CoworkingAddress.CoworkingId &&
                                                          ps.Service.Name.Contains("Endereço Fiscal"));

                            if (partnerService == null)
                            {
                                partnerService = await _context.PartnerServices
                                    .Include(ps => ps.Service)
                                    .FirstOrDefaultAsync(ps => ps.CoworkingId == contract.CoworkingAddress.CoworkingId);
                            }

                            if (partnerService == null)
                            {
                                errors++;
                                errorMessages.Add($"Contrato #{contract.Id}: Percentagem de comissão não configurada.");
                                continue;
                            }

                            // Calcular comissão mensal
                            var commissionAmount = Math.Round(contract.MonthlyValue * (partnerService.Percentage / 100m), 2, MidpointRounding.AwayFromZero);

                            var fiscalNote = new FiscalNote
                            {
                                Type = "Intermediacao",
                                ContractId = contract.Id,
                                CoworkingId = contract.CoworkingAddress.CoworkingId,
                                ClientId = contract.ClientId,
                                Amount = commissionAmount,
                                CommissionAmount = commissionAmount,
                                ServiceAmount = contract.MonthlyValue,
                                IssueDate = DateTime.UtcNow,
                                Status = "Pendente",
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.FiscalNotes.Add(fiscalNote);
                            notesEmitted++;
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            errorMessages.Add($"Contrato #{contract.Id}: {ex.Message}");
                        }
                    }
                }
                else if (noteType == "Servico")
                {
                    // NF de Serviço é emitida pelo parceiro, mas podemos criar registros para rastreamento
                    TempData["Info"] = "Notas Fiscais de Serviço devem ser emitidas pelo parceiro diretamente na SEFAZ.";
                }

                await _context.SaveChangesAsync();

                // Emitir as notas fiscais criadas via SEFAZ
                var pendingNotes = await _context.FiscalNotes
                    .Include(fn => fn.Coworking)
                        .ThenInclude(c => c.Addresses)
                    .Where(fn => fn.Status == "Pendente" && fn.Type == "Intermediacao")
                    .ToListAsync();

                int emittedCount = 0;
                int emissionErrors = 0;

                foreach (var note in pendingNotes)
                {
                    try
                    {
                        // Obter estado do coworking
                        string? coworkingState = null;
                        
                        if (note.ReservationId.HasValue)
                        {
                            var reservation = await _context.Reservations
                                .Include(r => r.CoworkingAddress)
                                .FirstOrDefaultAsync(r => r.Id == note.ReservationId);
                            coworkingState = reservation?.CoworkingAddress?.State?.Trim().ToUpper();
                        }
                        else if (note.ContractId.HasValue)
                        {
                            var contract = await _context.Contracts
                                .Include(c => c.CoworkingAddress)
                                .FirstOrDefaultAsync(c => c.Id == note.ContractId);
                            coworkingState = contract?.CoworkingAddress?.State?.Trim().ToUpper();
                        }

                        if (string.IsNullOrWhiteSpace(coworkingState))
                        {
                            emissionErrors++;
                            errorMessages.Add($"NF #{note.Id}: Estado do coworking não encontrado.");
                            continue;
                        }

                        // Buscar configuração SEFAZ para o estado
                        var config = await _sefazService.GetConfigurationByStateAsync(coworkingState);
                        
                        if (config == null)
                        {
                            emissionErrors++;
                            errorMessages.Add($"NF #{note.Id}: Configuração SEFAZ não encontrada para o estado {coworkingState}.");
                            continue;
                        }

                        var emittedNote = await _sefazService.IssueIntermediationNoteAsync(note, config);
                        _context.FiscalNotes.Update(emittedNote);
                        emittedCount++;
                    }
                    catch (NotSupportedException ex)
                    {
                        emissionErrors++;
                        errorMessages.Add($"NF #{note.Id}: {ex.Message}");
                        note.Status = "Erro";
                        note.ErrorMessage = ex.Message;
                        note.UpdatedAt = DateTime.UtcNow;
                        _context.FiscalNotes.Update(note);
                    }
                    catch (Exception ex)
                    {
                        emissionErrors++;
                        errorMessages.Add($"NF #{note.Id}: {ex.Message}");
                        note.Status = "Erro";
                        note.ErrorMessage = ex.Message;
                        note.UpdatedAt = DateTime.UtcNow;
                        _context.FiscalNotes.Update(note);
                    }
                }

                await _context.SaveChangesAsync();

                var message = $"Script executado com sucesso! {notesEmitted} nota(s) fiscal(is) criada(s), {emittedCount} emitida(s) na SEFAZ.";
                if (unsupportedStates > 0)
                {
                    message += $" {unsupportedStates} registro(s) ignorado(s) - estado sem integração implementada.";
                    if (unsupportedStateMessages.Any())
                    {
                        TempData["UnsupportedStates"] = string.Join("<br>", unsupportedStateMessages.Take(10));
                    }
                }
                if (errors > 0 || emissionErrors > 0)
                {
                    message += $" {errors + emissionErrors} erro(s) encontrado(s).";
                    if (errorMessages.Any())
                    {
                        TempData["ErrorDetails"] = string.Join("<br>", errorMessages.Take(10));
                    }
                }
                TempData["Success"] = message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao executar script: {ex.Message}";
            }

            return RedirectToAction("SefazConfiguration");
        }

        [HttpGet]
        public async Task<IActionResult> FiscalNotes(int page = 1, int pageSize = 20, string? status = null, string? type = null)
        {
            var query = _context.FiscalNotes
                .Include(fn => fn.Client)
                    .ThenInclude(c => c.User)
                .Include(fn => fn.Coworking)
                .Include(fn => fn.Reservation)
                .Include(fn => fn.Contract)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(fn => fn.Status == status);
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(fn => fn.Type == type);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var fiscalNotes = await query
                .OrderByDescending(fn => fn.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(fiscalNotes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelFiscalNote(int fiscalNoteId, string cancellationReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cancellationReason))
                {
                    TempData["Error"] = "Justificativa do cancelamento é obrigatória.";
                    return RedirectToAction("FiscalNotes");
                }

                var result = await _sefazService.CancelFiscalNoteAsync(fiscalNoteId, cancellationReason);
                
                if (result)
                {
                    TempData["Success"] = "Nota fiscal cancelada com sucesso!";
                }
                else
                {
                    TempData["Error"] = "Erro ao cancelar nota fiscal.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cancelar nota fiscal: {ex.Message}";
            }

            return RedirectToAction("FiscalNotes");
        }
    }
}
