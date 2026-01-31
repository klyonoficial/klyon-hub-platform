using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.IO;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;

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
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ICommissionService _commissionService;
        private readonly IMonthlySettlementService _monthlySettlementService;
        private readonly StorageServiceFactory _storageFactory;

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
            ISefazService sefazService,
            IContractService contractService,
            ICoworkingService coworkingService,
            IAuthService authService,
            IEmailService emailService,
            ICommissionService commissionService,
            IMonthlySettlementService monthlySettlementService,
            StorageServiceFactory storageFactory)
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
            _contractService = contractService;
            _coworkingService = coworkingService;
            _authService = authService;
            _emailService = emailService;
            _commissionService = commissionService;
            _monthlySettlementService = monthlySettlementService;
            _storageFactory = storageFactory;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var dashboardData = await _reportService.GetDashboardDataAsync();

            ViewBag.TotalUsers = dashboardData.TotalUsers;
            ViewBag.TotalCoworkings = dashboardData.TotalCoworkings;
            ViewBag.ActiveContracts = dashboardData.ActiveContracts;
            ViewBag.TotalRevenue = dashboardData.TotalRevenue;
            ViewBag.GrossRevenue = dashboardData.GrossRevenue;
            ViewBag.PartnerRevenue = dashboardData.PartnerRevenue;
            ViewBag.NetRevenue = dashboardData.NetRevenue;
            ViewBag.TotalLocations = dashboardData.TotalLocations;
            ViewBag.PendingPayments = dashboardData.PendingPayments;
            ViewBag.TotalCommissions = dashboardData.TotalCommissions;
            ViewBag.CompletedCommissions = dashboardData.CompletedCommissions;
            ViewBag.InactiveUsers = dashboardData.InactiveUsers;
            ViewBag.UnverifiedUsers = dashboardData.UnverifiedUsers;
            ViewBag.MonthlyRevenue = dashboardData.MonthlyRevenue;
            ViewBag.UsersByRole = dashboardData.UsersByRole;
            ViewBag.ContractsByStatus = dashboardData.ContractsByStatus;
            ViewBag.TopLocations = dashboardData.TopLocations;
            ViewBag.RecentPayments = dashboardData.RecentPayments;

            // Novos dados para gráficos melhorados
            ViewBag.MonthlyContracts = dashboardData.MonthlyContracts;
            ViewBag.TopCoworkingsByRevenue = dashboardData.TopCoworkingsByRevenue;
            ViewBag.PaymentsByMethod = dashboardData.PaymentsByMethod;
            ViewBag.MonthlyNewClients = dashboardData.MonthlyNewClients;
            ViewBag.MonthlyRevenueBreakdown = dashboardData.MonthlyRevenueBreakdown;
            ViewBag.CancelledContractsLast6Months = dashboardData.CancelledContractsLast6Months;
            ViewBag.NewContractsLast6Months = dashboardData.NewContractsLast6Months;

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

                // Estatísticas de reservas (apenas se EnableCoworkingMenus estiver habilitado)
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

                if (showCoworkingMenus)
                {
                    var totalReservations = await _context.Reservations.CountAsync();
                    ViewBag.TotalReservations = totalReservations;

                    var reservationsByStatus = await _context.Reservations
                        .GroupBy(r => r.Status)
                        .Select(g => new { status = g.Key, count = g.Count() })
                        .ToListAsync();
                    ViewBag.ReservationsByStatus = reservationsByStatus;
                }
                else
                {
                    ViewBag.TotalReservations = 0;
                    ViewBag.ReservationsByStatus = new List<dynamic>();
                }
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

            // Contar solicitações de remoção pendentes
            try
            {
                var pendingRemovalRequests = await _context.PartnerServices
                    .Where(ps => ps.RemovalRequestStatus == RemovalRequestStatus.Pending)
                    .CountAsync();

                ViewBag.PendingRemovalRequests = pendingRemovalRequests;
            }
            catch
            {
                ViewBag.PendingRemovalRequests = 0;
            }

            // Alertas administrativos
            var alerts = new List<dynamic>();

            try
            {
                // 1. Comissões pendentes há mais de X dias (padrão: 30 dias)
                var commissionAlertDays = await _systemConfigurationService.GetValueAsync("Alerts.CommissionPendingDays");
                var daysThreshold = int.TryParse(commissionAlertDays, out var parsedDays) ? parsedDays : 30;
                var thresholdDate = DateTime.UtcNow.AddDays(-daysThreshold);

                var pendingCommissionsCount = await _context.Commissions
                    .Where(c => !c.IsTransferred && c.CreatedAt < thresholdDate)
                    .CountAsync();

                if (pendingCommissionsCount > 0)
                {
                    var pendingCommissionsAmount = await _context.Commissions
                        .Where(c => !c.IsTransferred && c.CreatedAt < thresholdDate)
                        .SumAsync(c => c.Amount);

                    alerts.Add(new
                    {
                        Type = "warning",
                        Icon = "bi-exclamation-triangle-fill",
                        Title = $"Comissões Pendentes há mais de {daysThreshold} dias",
                        Message = $"{pendingCommissionsCount} comissão(ões) pendente(s) há mais de {daysThreshold} dias. Valor total: R$ {pendingCommissionsAmount:N2}",
                        Count = pendingCommissionsCount,
                        ActionUrl = Url.Action("CommissionFiscalNotes", "Admin"),
                        ActionText = "Ver Comissões"
                    });
                }
            }
            catch (Exception)
            {
                // Log error silently
            }

            try
            {
                // 2. Pagamentos atrasados
                var overduePayments = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.Client)
                    .Where(p => p.Status == PaymentStatus.Pending && p.DueDate < DateTime.UtcNow)
                    .ToListAsync();

                if (overduePayments.Any())
                {
                    var overdueAmount = overduePayments.Sum(p => p.Amount);
                    var oldestOverdue = overduePayments.OrderBy(p => p.DueDate).First();
                    var daysOverdue = (DateTime.UtcNow - oldestOverdue.DueDate).Days;

                    alerts.Add(new
                    {
                        Type = "danger",
                        Icon = "bi-exclamation-circle-fill",
                        Title = "Pagamentos Atrasados",
                        Message = $"{overduePayments.Count} pagamento(s) atrasado(s). Valor total: R$ {overdueAmount:N2}. Mais antigo há {daysOverdue} dia(s).",
                        Count = overduePayments.Count,
                        ActionUrl = Url.Action("Payments", "Admin", new { status = "Pending" }),
                        ActionText = "Ver Pagamentos Atrasados"
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error silently
            }

            try
            {
                // 3. Capacidade próxima do limite (80% ou mais)
                var capacityThreshold = 0.8m; // 80% da capacidade

                var addressesNearCapacity = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .Where(a => a.AddressStatus == AddressStatus.Active &&
                               ((a.MaxContracts > 0 && (decimal)a.CurrentContracts / a.MaxContracts >= capacityThreshold) ||
                                (a.ApprovedFiscalCapacity > 0 && (decimal)a.CurrentContracts / a.ApprovedFiscalCapacity >= capacityThreshold)))
                    .ToListAsync();

                if (addressesNearCapacity.Any())
                {
                    var atCapacityCount = addressesNearCapacity.Count(a =>
                        (a.MaxContracts > 0 && a.CurrentContracts >= a.MaxContracts) ||
                        (a.ApprovedFiscalCapacity > 0 && a.CurrentContracts >= a.ApprovedFiscalCapacity));

                    alerts.Add(new
                    {
                        Type = "info",
                        Icon = "bi-info-circle-fill",
                        Title = "Capacidade Próxima do Limite",
                        Message = $"{addressesNearCapacity.Count} endereço(s) com capacidade próxima ou no limite (≥80%). {atCapacityCount} endereço(s) já atingiram a capacidade máxima.",
                        Count = addressesNearCapacity.Count,
                        ActionUrl = Url.Action("Coworkings", "Admin", new { capacityAlert = "true" }),
                        ActionText = "Ver Coworkings"
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error silently
            }

            try
            {
                // 4. Coworkings com alterações recentes aguardando análise
                var recentUpdateThreshold = DateTime.UtcNow.AddHours(-24); // Últimas 24 horas

                var coworkingsWithRecentUpdates = await _context.Coworkings
                    .Include(c => c.User)
                    .Where(c => c.ApprovalStatus == CoworkingApprovalStatus.UnderReview &&
                               c.SubmittedAt.HasValue &&
                               c.SubmittedAt.Value >= recentUpdateThreshold)
                    .OrderByDescending(c => c.SubmittedAt)
                    .ToListAsync();

                if (coworkingsWithRecentUpdates.Any())
                {
                    var coworkingNames = coworkingsWithRecentUpdates
                        .Take(3)
                        .Select(c => c.Name)
                        .ToList();

                    var moreCount = coworkingsWithRecentUpdates.Count - coworkingNames.Count;
                    var message = $"{coworkingsWithRecentUpdates.Count} parceiro(s) fizeram alterações recentes e estão aguardando análise: {string.Join(", ", coworkingNames)}";
                    if (moreCount > 0)
                    {
                        message += $" e mais {moreCount} outro(s)";
                    }

                    alerts.Add(new
                    {
                        Type = "warning",
                        Icon = "bi-bell-fill",
                        Title = "Alterações de Parceiros Aguardando Análise",
                        Message = message,
                        Count = coworkingsWithRecentUpdates.Count,
                        ActionUrl = Url.Action("Coworkings", "Admin", new { approvalStatus = "UnderReview" }),
                        ActionText = "Ver Parceiros em Análise"
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error silently
            }

            try
            {
                // 5. Comprovantes de pagamento aguardando validação (status "Aguardando Validação" na tela Client/PaymentDetails)
                var paymentsAwaitingValidation = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                    .Where(p => p.Status == PaymentStatus.AwaitingValidation)
                    .ToListAsync();

                if (paymentsAwaitingValidation.Any())
                {
                    var distinctClientsCount = paymentsAwaitingValidation
                        .Where(p => p.Subscription?.Contract?.ClientId != null)
                        .Select(p => p.Subscription!.Contract!.ClientId)
                        .Distinct()
                        .Count();
                    var paymentCount = paymentsAwaitingValidation.Count;
                    var count = distinctClientsCount > 0 ? distinctClientsCount : paymentCount;
                    var message = distinctClientsCount == 0
                        ? $"{paymentCount} pagamento(s) aguardando validação do comprovante."
                        : (distinctClientsCount == 1
                            ? "1 cliente aguardando validação do comprovante de pagamento."
                            : $"{distinctClientsCount} clientes aguardando validação do comprovante de pagamento ({paymentCount} pagamento(s)).");

                    alerts.Add(new
                    {
                        Type = "danger",
                        Icon = "bi-cash-coin",
                        Title = "Comprovantes de Pagamento Aguardando Validação",
                        Message = message,
                        Count = count,
                        ActionUrl = Url.Action("Payments", "Admin", new { status = "AwaitingValidation" }),
                        ActionText = "Ver Pagamentos"
                    });
                }
            }
            catch (Exception)
            {
                // Log error silently
            }

            ViewBag.AdminAlerts = alerts;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardList(string type, string? filter = null)
        {
            try
            {
                var data = new List<object>();
                string viewAllUrl = "#";

                switch (type.ToLower())
                {
                    case "users":
                        var usersQuery = _context.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsQueryable();

                        if (filter == "inactive")
                        {
                            usersQuery = usersQuery.Where(u => !u.IsActive);
                            viewAllUrl = Url.Action("Users", "Admin", new { status = "inactive" });
                        }
                        else if (filter == "unverified")
                        {
                            usersQuery = usersQuery.Where(u => !u.EmailVerified);
                            viewAllUrl = Url.Action("Users", "Admin", new { emailVerified = "false" });
                        }
                        else
                        {
                            viewAllUrl = Url.Action("Users", "Admin");
                        }

                        var users = await usersQuery
                            .OrderByDescending(u => u.CreatedAt)
                            .Take(50)
                            .ToListAsync();

                        data = users.Select(u => new
                        {
                            id = u.Id,
                            fullName = u.FullName,
                            email = u.Email,
                            isActive = u.IsActive,
                            emailVerified = u.EmailVerified
                        }).Cast<object>().ToList();
                        break;

                    case "coworkings":
                        var coworkingsQuery = _context.Coworkings.Include(c => c.User).AsQueryable();
                        var coworkings = await coworkingsQuery
                            .OrderByDescending(c => c.CreatedAt)
                            .Take(50)
                            .ToListAsync();

                        data = coworkings.Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            cnpj = c.CNPJ,
                            isActive = c.IsActive,
                            approvalStatus = c.ApprovalStatus.ToString(),
                            approvalStatusDisplay = c.ApprovalStatus == CoworkingApprovalStatus.Approved ? "Aprovado" :
                                                    c.ApprovalStatus == CoworkingApprovalStatus.UnderReview ? "Aguardando Aprovação" :
                                                    c.ApprovalStatus == CoworkingApprovalStatus.AwaitingAdjustment ? "Aguardando Ajustes" : "Desconhecido"
                        }).Cast<object>().ToList();
                        viewAllUrl = Url.Action("Coworkings", "Admin");
                        break;

                    case "contracts":
                        var contractsQuery = _context.Contracts
                            .Include(c => c.Client)
                            .Include(c => c.CoworkingAddress)
                                .ThenInclude(a => a.Location)
                            .AsQueryable();

                        if (filter == "Active")
                        {
                            contractsQuery = contractsQuery.Where(c => c.Status == ContractStatus.Active);
                            viewAllUrl = Url.Action("Contracts", "Admin", new { status = "Active" });
                        }
                        else
                        {
                            viewAllUrl = Url.Action("Contracts", "Admin");
                        }

                        var contracts = await contractsQuery
                            .OrderByDescending(c => c.CreatedAt)
                            .Take(50)
                            .ToListAsync();

                        data = contracts.Select(c => new
                        {
                            id = c.Id,
                            contractNumber = c.ContractNumber,
                            clientName = c.Client?.RazaoSocial ?? c.Client?.NomeFantasia ?? c.Client?.ContactName ?? "N/A",
                            location = c.CoworkingAddress?.Location != null
                                ? $"{c.CoworkingAddress.Location.City}/{c.CoworkingAddress.Location.State}"
                                : "N/A",
                            status = c.Status.ToString(),
                            statusDisplay = c.Status == ContractStatus.Active ? "Ativo" :
                                           c.Status == ContractStatus.PendingPayment ? "Aguardando Pagamento" :
                                           c.Status == ContractStatus.Suspended ? "Suspenso" :
                                           c.Status == ContractStatus.Cancelled ? "Cancelado" : c.Status.ToString()
                        }).Cast<object>().ToList();
                        break;

                    case "locations":
                        var locations = await _context.Locations
                            .OrderBy(l => l.State)
                            .ThenBy(l => l.City)
                            .Take(50)
                            .ToListAsync();

                        data = locations.Select(l => new
                        {
                            id = l.Id,
                            city = l.City,
                            state = l.State,
                            region = l.Region,
                            isActive = l.IsActive
                        }).Cast<object>().ToList();
                        viewAllUrl = Url.Action("Locations", "Admin");
                        break;

                    case "payments":
                        var paymentsQuery = _context.Payments
                            .Include(p => p.Subscription)
                                .ThenInclude(s => s.Contract)
                                    .ThenInclude(c => c.Client)
                            .AsQueryable();

                        if (filter == "Pending")
                        {
                            paymentsQuery = paymentsQuery.Where(p => p.Status == PaymentStatus.Pending);
                            viewAllUrl = Url.Action("Payments", "Admin", new { status = "Pending" });
                        }
                        else
                        {
                            viewAllUrl = Url.Action("Payments", "Admin");
                        }

                        var payments = await paymentsQuery
                            .OrderByDescending(p => p.CreatedAt)
                            .Take(50)
                            .ToListAsync();

                        data = payments.Select(p => new
                        {
                            id = p.Id,
                            clientName = p.Subscription?.Contract?.Client?.RazaoSocial ??
                                       p.Subscription?.Contract?.Client?.NomeFantasia ??
                                       p.Subscription?.Contract?.Client?.ContactName ?? "N/A",
                            amount = p.Amount,
                            status = p.Status.ToString(),
                            statusDisplay = p.Status == PaymentStatus.Paid ? "Pago" :
                                          p.Status == PaymentStatus.Pending ? "Pendente" :
                                          p.Status == PaymentStatus.AwaitingValidation ? "Aguardando Validação" :
                                          p.Status == PaymentStatus.Processing ? "Processando" :
                                          p.Status == PaymentStatus.Failed ? "Falhou" :
                                          p.Status == PaymentStatus.Cancelled ? "Cancelado" :
                                          p.Status == PaymentStatus.Refunded ? "Reembolsado" : p.Status.ToString(),
                            createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                        }).Cast<object>().ToList();
                        break;

                    case "reservations":
                        var reservations = await _context.Reservations
                            .Include(r => r.Client)
                            .Include(r => r.CoworkingAddress)
                                .ThenInclude(a => a.Coworking)
                            .OrderByDescending(r => r.CreatedAt)
                            .Take(50)
                            .ToListAsync();

                        data = reservations.Select(r => new
                        {
                            id = r.Id,
                            clientName = r.Client?.RazaoSocial ?? r.Client?.NomeFantasia ?? r.Client?.ContactName ?? "N/A",
                            coworkingName = r.CoworkingAddress?.Coworking?.Name ?? "N/A",
                            reservationDate = r.ReservationDate.ToString("dd/MM/yyyy"),
                            status = r.Status,
                            statusDisplay = r.Status == "Confirmed" ? "Confirmada" :
                                          r.Status == "Pending" ? "Pendente" :
                                          r.Status == "Cancelled" ? "Cancelada" :
                                          r.Status == "Completed" ? "Concluída" : r.Status
                        }).Cast<object>().ToList();
                        viewAllUrl = Url.Action("ReportCoworkingRevenue", "Admin");
                        break;

                    default:
                        return Json(new { success = false, message = "Tipo inválido" });
                }

                return Json(new { success = true, data = data, viewAllUrl = viewAllUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao carregar dados: {ex.Message}" });
            }
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

                // Capturar valores antigos para auditoria
                var oldFullName = user.FullName;
                var oldEmail = user.Email;
                var oldPhone = user.Phone;
                var oldIsActive = user.IsActive;
                var oldRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == id)
                    .Include(ur => ur.Role)
                    .Select(ur => ur.Role.Name)
                    .ToListAsync();

                user.FullName = fullName;
                user.Email = email;
                user.Phone = phone;
                user.IsActive = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                user.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _userService.UpdateAsync(user);

                // Atualizar roles
                var newRoleNames = new List<string>();
                if (roleIds != null)
                {
                    var existingUserRoles = await _context.UserRoles
                        .Where(ur => ur.UserId == id)
                        .ToListAsync();

                    _context.UserRoles.RemoveRange(existingUserRoles);

                    foreach (var roleId in roleIds)
                    {
                        var role = await _context.Roles.FindAsync(roleId);
                        if (role != null)
                        {
                            _context.UserRoles.Add(new UserRole
                            {
                                UserId = id,
                                RoleId = roleId
                            });
                            newRoleNames.Add(role.Name);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // Log de auditoria: edição de usuário (ação administrativa)
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                await _auditService.LogActionAsync(
                    userId,
                    "Usuário editado (ação administrativa)",
                    "User",
                    id,
                    System.Text.Json.JsonSerializer.Serialize(new { FullName = oldFullName, Email = oldEmail, Phone = oldPhone, IsActive = oldIsActive, Roles = oldRoles }),
                    System.Text.Json.JsonSerializer.Serialize(new { FullName = user.FullName, Email = user.Email, Phone = user.Phone, IsActive = user.IsActive, Roles = newRoleNames }),
                    ipAddress,
                    userAgent
                );

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
        public async Task<IActionResult> Coworkings(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, string? capacityAlert = null, int page = 1, int pageSize = 15)
        {
            var coworkings = await _adminService.GetCoworkingsFilteredAsync(search, status, isPartner, approvalStatus, page, pageSize);
            var totalItems = await _adminService.GetCoworkingsFilteredCountAsync(search, status, isPartner, approvalStatus);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Carregar contagem de serviços para cada coworking
            Dictionary<int, int> servicesCount = new Dictionary<int, int>();
            // Carregar coworkings com percentagens pendentes (Percentage <= 0)
            Dictionary<int, bool> hasPendingPercentages = new Dictionary<int, bool>();
            // Carregar coworkings com capacidade máxima
            Dictionary<int, bool> hasMaxCapacity = new Dictionary<int, bool>();

            if (coworkings != null && coworkings.Any())
            {
                var coworkingIds = coworkings.Select(c => c.Id).ToList();
                servicesCount = await _context.PartnerServices
                    .Where(ps => coworkingIds.Contains(ps.CoworkingId))
                    .GroupBy(ps => ps.CoworkingId)
                    .Select(g => new { CoworkingId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CoworkingId, x => x.Count);

                // Verificar quais coworkings têm serviços com percentagem pendente (<= 0)
                var coworkingsWithPendingPercentages = await _context.PartnerServices
                    .Where(ps => coworkingIds.Contains(ps.CoworkingId) && ps.Percentage <= 0)
                    .Select(ps => ps.CoworkingId)
                    .Distinct()
                    .ToListAsync();

                hasPendingPercentages = coworkingIds
                    .ToDictionary(id => id, id => coworkingsWithPendingPercentages.Contains(id));

                // Se veio do alerta de capacidade, identificar coworkings com capacidade máxima
                if (capacityAlert == "true")
                {
                    var coworkingsAtMaxCapacity = await _context.CoworkingAddresses
                        .Where(a => a.AddressStatus == AddressStatus.Active &&
                                   coworkingIds.Contains(a.CoworkingId) &&
                                   ((a.MaxContracts > 0 && a.CurrentContracts >= a.MaxContracts) ||
                                    (a.ApprovedFiscalCapacity > 0 && a.CurrentContracts >= a.ApprovedFiscalCapacity)))
                        .Select(a => a.CoworkingId)
                        .Distinct()
                        .ToListAsync();

                    hasMaxCapacity = coworkingIds
                        .ToDictionary(id => id, id => coworkingsAtMaxCapacity.Contains(id));
                }
            }

            ViewBag.ServicesCount = servicesCount;
            ViewBag.HasPendingPercentages = hasPendingPercentages;
            ViewBag.HasMaxCapacity = hasMaxCapacity;
            ViewBag.CapacityAlert = capacityAlert == "true";
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
        public async Task<IActionResult> ViewCoworking(int id, int page = 1, int pageSize = 4)
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

            // Verificar se os menus de coworking estão habilitados
            var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync("Features.EnableCoworkingMenus");
            var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

            var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();

            // Carregar serviços disponíveis
            IQueryable<Service> servicesQuery = _context.Services.Where(s => s.IsActive);
            if (!showCoworkingMenus)
                servicesQuery = servicesQuery.Where(s => !coworkingServiceNames.Contains(s.Name));

            var services = await servicesQuery.OrderBy(s => s.DisplayOrder).ToListAsync();
            ViewBag.Services = services;

            // Carregar serviços do parceiro (apenas os que têm serviço ativo)
            var partnerServicesQuery = _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == id && ps.Service != null && ps.Service.IsActive);

            if (!showCoworkingMenus)
                partnerServicesQuery = partnerServicesQuery.Where(ps => ps.Service != null && !coworkingServiceNames.Contains(ps.Service.Name));

            var partnerServices = await partnerServicesQuery.ToListAsync();
            ViewBag.PartnerServices = partnerServices;
            ViewBag.ShowCoworkingMenus = showCoworkingMenus;

            // Configurar paginação para documentos obrigatórios
            var requiredDocTypes = new[]
            {
                (CoworkingDocumentType.AddressCessionContract, "Contrato de cessão de endereço", "Documento que comprova a cessão do endereço para uso fiscal"),
                (CoworkingDocumentType.AddressProof, "Comprovante de endereço do coworking", "Conta de água, luz ou IPTU emitido em nome do coworking (últimos 90 dias)"),
                (CoworkingDocumentType.CNPJCard, "Cartão CNPJ do coworking", "Documento que comprova que o coworking está ativo e possui CNAE compatível"),
                (CoworkingDocumentType.AddressAuthorization, "Declaração de autorização", "Declaração de autorização de uso de endereço fiscal")
            };

            var totalDocuments = requiredDocTypes.Length;
            var totalPages = (int)Math.Ceiling(totalDocuments / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            ViewBag.DocumentsPage = page;
            ViewBag.DocumentsTotalPages = totalPages;
            ViewBag.DocumentsPageSize = pageSize;
            ViewBag.DocumentsTotalItems = totalDocuments;

            return View(coworking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCoworking(int id, string? search, string? status, string? isPartner, string? approvalStatus, int? page)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _context.Coworkings.FindAsync(id);
            if (coworking == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Coworkings"); }

            // Verificar se tem percentagem configurada antes de aprovar
            var fiscalAddressService = await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive);

            if (fiscalAddressService != null)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworking.Id
                        && ps.ServiceId == fiscalAddressService.Id
                        && ps.Percentage > 0);

                if (partnerService == null)
                {
                    TempData["Warning"] = $"Atenção: O coworking '{coworking.Name}' foi aprovado, mas não possui percentagem configurada para o serviço 'Ofertar Endereço Fiscal'. Configure a percentagem para que os endereços fiquem disponíveis para compra.";
                }
            }

            var oldStatus = coworking.ApprovalStatus;
            coworking.ApprovalStatus = CoworkingApprovalStatus.Approved;
            coworking.ApprovedAt = DateTimeHelper.GetBrazilianTime();
            coworking.ApprovedBy = userId;
            coworking.RejectionReason = null;
            coworking.RejectedAt = null;
            coworking.RejectedBy = null;
            coworking.UpdatedAt = DateTimeHelper.GetBrazilianTime();

            // Marcar notificação de mudança de status
            coworking.HasPendingStatusNotification = true;
            coworking.LastNotifiedStatus = CoworkingApprovalStatus.Approved;

            // Aprovar automaticamente todos os endereços pendentes deste coworking
            var pendingAddresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworking.Id && a.AddressStatus == AddressStatus.PendingApproval)
                .ToListAsync();

            foreach (var address in pendingAddresses)
            {
                address.AddressStatus = AddressStatus.Active;
                address.IsAvailable = true;
                address.UpdatedAt = DateTime.UtcNow;

                // Log de auditoria para cada endereço aprovado
                await _auditService.LogActionAsync(userId, "AddressStatusChanged", "CoworkingAddress", address.Id,
                    $"{{\"AddressStatus\":\"PendingApproval\"}}", $"{{\"AddressStatus\":\"Active\",\"Reason\":\"Aprovado automaticamente ao aprovar coworking\"}}");
            }

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
            coworking.RejectedAt = DateTimeHelper.GetBrazilianTime();
            coworking.RejectedBy = userId;
            coworking.UpdatedAt = DateTimeHelper.GetBrazilianTime();

            // Marcar notificação de mudança de status
            coworking.HasPendingStatusNotification = true;
            coworking.LastNotifiedStatus = CoworkingApprovalStatus.AwaitingAdjustment;

            await _context.SaveChangesAsync();
            await _auditService.LogActionAsync(userId, "ApprovalStatusChange", "Coworking", id, "{\"ApprovalStatus\":\"" + oldStatus + "\"}", "{\"ApprovalStatus\":\"AwaitingAdjustment\",\"RejectionReason\":" + System.Text.Json.JsonSerializer.Serialize(coworking.RejectionReason) + "}");
            TempData["Success"] = "Cadastro reprovado. O coworking foi notificado e poderá reenviar após os ajustes.";
            if (page.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(isPartner) || !string.IsNullOrEmpty(approvalStatus))
                return RedirectToAction("Coworkings", new { search, status, isPartner, approvalStatus, page });
            return RedirectToAction("ViewCoworking", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveDocument(int documentId, int? page = null, int? pageSize = null)
        {
            var doc = await _context.CoworkingDocuments.FindAsync(documentId);
            if (doc == null) { TempData["Error"] = "Documento não encontrado."; return RedirectToAction("Coworkings"); }
            doc.Status = CoworkingDocumentStatus.Approved;
            doc.RejectionReason = null;
            doc.UpdatedAt = DateTimeHelper.GetBrazilianTime();
            await _context.SaveChangesAsync();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _auditService.LogActionAsync(userId, "DocumentApproved", "CoworkingDocument", documentId, "{\"Status\":\"Pending\"}", "{\"Status\":\"Approved\"}");
            TempData["Success"] = "Documento aprovado.";
            if (page.HasValue)
            {
                return RedirectToAction("ViewCoworking", new { id = doc.CoworkingId, page = page.Value, pageSize = pageSize ?? 4 });
            }
            return RedirectToAction("ViewCoworking", new { id = doc.CoworkingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectDocument(int documentId, string rejectionReason, int? page = null, int? pageSize = null)
        {
            var docForId = await _context.CoworkingDocuments.FindAsync(documentId);
            if (string.IsNullOrWhiteSpace(rejectionReason)) { TempData["Error"] = "Informe o motivo da rejeição."; return RedirectToAction("ViewCoworking", new { id = docForId?.CoworkingId ?? 0, page = page ?? 1, pageSize = pageSize ?? 4 }); }
            var doc = await _context.CoworkingDocuments.FindAsync(documentId);
            if (doc == null) { TempData["Error"] = "Documento não encontrado."; return RedirectToAction("Coworkings"); }
            var oldStatus = doc.Status.ToString();
            doc.Status = CoworkingDocumentStatus.Rejected;
            doc.RejectionReason = rejectionReason.Trim();
            doc.UpdatedAt = DateTimeHelper.GetBrazilianTime();
            await _context.SaveChangesAsync();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _auditService.LogActionAsync(userId, "DocumentRejected", "CoworkingDocument", documentId, "{\"Status\":\"" + oldStatus + "\"}", "{\"Status\":\"Rejected\",\"RejectionReason\":" + System.Text.Json.JsonSerializer.Serialize(doc.RejectionReason) + "}");
            TempData["Success"] = "Documento rejeitado. O coworking verá o motivo no perfil.";
            if (page.HasValue)
            {
                return RedirectToAction("ViewCoworking", new { id = doc.CoworkingId, page = page.Value, pageSize = pageSize ?? 4 });
            }
            return RedirectToAction("ViewCoworking", new { id = doc.CoworkingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCapacityChange(int requestId)
        {
            try
            {
                var request = await _context.CapacityChangeRequests
                    .Include(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    TempData["Error"] = "Solicitação de alteração de capacidade não encontrada.";
                    return RedirectToAction("Dashboard");
                }

                if (request.Status != CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ViewCoworking", new { id = request.CoworkingAddress.CoworkingId });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var address = request.CoworkingAddress;

                // Aprovar a capacidade
                address.ApprovedFiscalCapacity = request.RequestedCapacity;
                address.RequestedFiscalCapacity = request.RequestedCapacity;
                address.CapacityApprovalStatus = CapacityApprovalStatus.Approved;
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                // Atualizar a solicitação
                request.Status = CapacityApprovalStatus.Approved;
                request.ReviewedBy = userId;
                request.ReviewedAt = DateTimeHelper.GetBrazilianTime();
                request.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(userId, "CapacityChangeApproved", "CapacityChangeRequest", requestId,
                    $"{{\"PreviousCapacity\":{request.PreviousCapacity},\"RequestedCapacity\":{request.RequestedCapacity},\"Status\":\"Pending\"}}",
                    $"{{\"ApprovedCapacity\":{request.RequestedCapacity},\"Status\":\"Approved\"}}");

                TempData["Success"] = $"Capacidade fiscal aprovada. Nova capacidade: {request.RequestedCapacity}";
                return RedirectToAction("ViewCoworking", new { id = address.CoworkingId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar alteração de capacidade: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCapacityChange(int requestId, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "Informe o motivo da reprovação.";
                    return RedirectToAction("Dashboard");
                }

                var request = await _context.CapacityChangeRequests
                    .Include(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    TempData["Error"] = "Solicitação de alteração de capacidade não encontrada.";
                    return RedirectToAction("Dashboard");
                }

                if (request.Status != CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ViewCoworking", new { id = request.CoworkingAddress.CoworkingId });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var address = request.CoworkingAddress;

                // Reprovar a solicitação
                address.CapacityApprovalStatus = CapacityApprovalStatus.Rejected;
                address.RequestedFiscalCapacity = request.PreviousCapacity; // Reverter para capacidade anterior
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                // Atualizar a solicitação
                request.Status = CapacityApprovalStatus.Rejected;
                request.RejectionReason = rejectionReason.Trim();
                request.ReviewedBy = userId;
                request.ReviewedAt = DateTimeHelper.GetBrazilianTime();
                request.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(userId, "CapacityChangeRejected", "CapacityChangeRequest", requestId,
                    $"{{\"PreviousCapacity\":{request.PreviousCapacity},\"RequestedCapacity\":{request.RequestedCapacity},\"Status\":\"Pending\"}}",
                    $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}");

                TempData["Success"] = "Alteração de capacidade reprovada. O parceiro foi notificado.";
                return RedirectToAction("ViewCoworking", new { id = address.CoworkingId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao reprovar alteração de capacidade: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveMaxContractsChange(int requestId)
        {
            try
            {
                var request = await _context.MaxContractsChangeRequests
                    .Include(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    TempData["Error"] = "Solicitação de alteração de quantidade máxima não encontrada.";
                    return RedirectToAction("Dashboard");
                }

                if (request.Status != CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ViewCoworking", new { id = request.CoworkingAddress.CoworkingId });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var address = request.CoworkingAddress;

                // Aprovar a quantidade máxima
                address.MaxContracts = request.RequestedMaxContracts;
                address.MaxContractsApprovalStatus = CapacityApprovalStatus.Approved;
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                // Atualizar a solicitação
                request.Status = CapacityApprovalStatus.Approved;
                request.ReviewedBy = userId;
                request.ReviewedAt = DateTimeHelper.GetBrazilianTime();
                request.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(userId, "MaxContractsChangeApproved", "MaxContractsChangeRequest", requestId,
                    $"{{\"PreviousMaxContracts\":{request.PreviousMaxContracts},\"RequestedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Pending\"}}",
                    $"{{\"ApprovedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Approved\"}}");

                TempData["Success"] = $"Quantidade máxima de contratos aprovada. Nova quantidade: {request.RequestedMaxContracts}";
                return RedirectToAction("ViewCoworking", new { id = address.CoworkingId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar alteração de quantidade máxima: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectMaxContractsChange(int requestId, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "Informe o motivo da reprovação.";
                    return RedirectToAction("Dashboard");
                }

                var request = await _context.MaxContractsChangeRequests
                    .Include(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    TempData["Error"] = "Solicitação de alteração de quantidade máxima não encontrada.";
                    return RedirectToAction("Dashboard");
                }

                if (request.Status != CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ViewCoworking", new { id = request.CoworkingAddress.CoworkingId });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var address = request.CoworkingAddress;

                // Reprovar a solicitação
                address.MaxContractsApprovalStatus = CapacityApprovalStatus.Rejected;
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                // Atualizar a solicitação
                request.Status = CapacityApprovalStatus.Rejected;
                request.RejectionReason = rejectionReason.Trim();
                request.ReviewedBy = userId;
                request.ReviewedAt = DateTimeHelper.GetBrazilianTime();
                request.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(userId, "MaxContractsChangeRejected", "MaxContractsChangeRequest", requestId,
                    $"{{\"PreviousMaxContracts\":{request.PreviousMaxContracts},\"RequestedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Pending\"}}",
                    $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}");

                TempData["Success"] = "Alteração de quantidade máxima reprovada. O parceiro foi notificado.";
                return RedirectToAction("ViewCoworking", new { id = address.CoworkingId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao reprovar alteração de quantidade máxima: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        /// <summary>
        /// Altera o status do endereço (Ativo/Suspenso/Removido). Não afeta contratos em andamento.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAddressStatus(int addressId, int coworkingId, string status)
        {
            var address = await _context.CoworkingAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId);
            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado.";
                return RedirectToAction("ViewCoworking", new { id = coworkingId });
            }
            if (!Enum.TryParse<AddressStatus>(status, ignoreCase: true, out var newStatus))
            {
                TempData["Error"] = $"Status inválido: {status}";
                return RedirectToAction("ViewCoworking", new { id = coworkingId });
            }
            var oldStatus = address.AddressStatus;

            // Quando aprovar (mudar para Active), tornar disponível
            if (newStatus == AddressStatus.Active)
            {
                address.AddressStatus = AddressStatus.Active;
                address.IsAvailable = true;
            }
            // Quando voltar para pendente, suspender ou remover, tornar indisponível
            else if (newStatus == AddressStatus.PendingApproval || newStatus == AddressStatus.Suspended || newStatus == AddressStatus.Removed)
            {
                address.AddressStatus = newStatus;
                address.IsAvailable = false;
            }
            else
            {
                address.AddressStatus = newStatus;
            }

            address.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _auditService.LogActionAsync(userId, "AddressStatusChanged", "CoworkingAddress", addressId,
                $"{{\"AddressStatus\":\"{oldStatus}\"}}", $"{{\"AddressStatus\":\"{newStatus}\"}}");

            var label = newStatus == AddressStatus.PendingApproval ? "Pendente aprovação"
                : newStatus == AddressStatus.Active ? "Ativo"
                : newStatus == AddressStatus.Suspended ? "Suspenso"
                : "Removido";
            TempData["Success"] = $"Status do endereço alterado para {label}.";
            return RedirectToAction("ViewCoworking", new { id = coworkingId });
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
        public async Task<IActionResult> GetAuditLogDetails(int id)
        {
            var log = await _context.AuditLogs
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return Json(new { success = false, message = "Log não encontrado." });
            }

            var actionLabel = log.Action switch
            {
                "Submitted" => "Cadastro enviado",
                "ApprovalStatusChange" => "Mudança de status",
                "ResubmitForApproval" => "Reenviado para análise",
                "ReturnedToReview" => "Retornou para análise",
                _ => log.Action
            };

            return Json(new
            {
                success = true,
                log = new
                {
                    id = log.Id,
                    action = log.Action,
                    actionLabel = actionLabel,
                    entityType = log.EntityType,
                    entityId = log.EntityId,
                    actionDate = log.ActionDate.ToString("dd/MM/yyyy HH:mm:ss"),
                    userId = log.UserId,
                    userName = log.User?.FullName ?? "Sistema",
                    userEmail = log.User?.Email,
                    ipAddress = log.IpAddress,
                    userAgent = log.UserAgent,
                    oldValues = log.OldValues,
                    newValues = log.NewValues
                }
            });
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
        public async Task<IActionResult> UpdateCoworking(
            int Id,
            string Name,
            string CNPJ,
            string? TradeName,
            string? ContactEmail,
            string? ContactPhone,
            string? SecondaryEmail,
            string? SecondaryPhone,
            string? BankName,
            string? BankAgency,
            string? BankAccount,
            string? BankAccountType,
            string? BankPixKey,
            string? Description,
            string? Website,
            string? Instagram,
            string? Facebook,
            string? LinkedIn,
            string? Address,
            string? AddressNumber,
            string? AddressComplement,
            string? AddressNeighborhood,
            string? AddressCity,
            string? AddressState,
            string? AddressZipCode,
            bool IsActive,
            bool IsPartner,
            CoworkingApprovalStatus ApprovalStatus,
            string? RejectionReason)
        {
            try
            {
                var coworking = await _context.Coworkings.FindAsync(Id);
                if (coworking == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Coworkings");
                }

                // Validar CNPJ se foi alterado
                if (!string.IsNullOrEmpty(CNPJ) && CNPJ != coworking.CNPJ)
                {
                    var cnpjClean = CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                    {
                        TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
                        return RedirectToAction("EditCoworking", new { id = Id });
                    }

                    // Verificar se o CNPJ já está cadastrado em outro coworking
                    var existingCNPJ = await _context.Coworkings
                        .Where(c => c.Id != Id && c.CNPJ != null &&
                            c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                        .FirstOrDefaultAsync();

                    if (existingCNPJ != null)
                    {
                        TempData["Error"] = $"O CNPJ {CNPJ} já está cadastrado para outro coworking.";
                        return RedirectToAction("EditCoworking", new { id = Id });
                    }

                    // Formatar CNPJ com máscara
                    CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                }

                // Atualizar campos
                coworking.Name = Name;
                coworking.CNPJ = CNPJ;
                coworking.TradeName = TradeName;
                coworking.ContactEmail = ContactEmail;
                coworking.ContactPhone = ContactPhone;
                coworking.SecondaryEmail = SecondaryEmail;
                coworking.SecondaryPhone = SecondaryPhone;
                coworking.BankName = BankName;
                coworking.BankAgency = BankAgency;
                coworking.BankAccount = BankAccount;
                coworking.BankAccountType = BankAccountType;
                coworking.BankPixKey = BankPixKey;
                coworking.Description = Description;
                coworking.Website = Website;
                coworking.Instagram = Instagram;
                coworking.Facebook = Facebook;
                coworking.LinkedIn = LinkedIn;
                coworking.Address = Address;
                coworking.AddressNumber = AddressNumber;
                coworking.AddressComplement = AddressComplement;
                coworking.AddressNeighborhood = AddressNeighborhood;
                coworking.AddressCity = AddressCity;
                coworking.AddressState = AddressState;
                coworking.AddressZipCode = AddressZipCode;
                coworking.IsActive = IsActive;
                coworking.IsPartner = IsPartner;
                coworking.ApprovalStatus = ApprovalStatus;
                coworking.RejectionReason = RejectionReason;
                coworking.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Coworking '{coworking.Name}' atualizado com sucesso.";
                return RedirectToAction("ViewCoworking", new { id = Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar coworking: {ex.Message}";
                return RedirectToAction("EditCoworking", new { id = Id });
            }
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

                // Se estiver tentando ativar, verificar se tem percentagem informada
                if (!coworking.IsActive)
                {
                    // Buscar o serviço "Ofertar Endereço Fiscal"
                    var fiscalAddressService = await _context.Services
                        .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive);

                    if (fiscalAddressService != null)
                    {
                        // Verificar se existe PartnerService com percentagem > 0
                        var partnerService = await _context.PartnerServices
                            .FirstOrDefaultAsync(ps => ps.CoworkingId == coworking.Id
                                && ps.ServiceId == fiscalAddressService.Id
                                && ps.Percentage > 0);

                        if (partnerService == null)
                        {
                            TempData["Error"] = $"Não é possível ativar o coworking '{coworking.Name}' porque não possui percentagem informada para o serviço 'Ofertar Endereço Fiscal'. Configure a percentagem antes de ativar.";
                            return RedirectToAction("Coworkings", new
                            {
                                search = Request.Query["search"],
                                status = Request.Query["status"],
                                isPartner = Request.Query["isPartner"],
                                approvalStatus = Request.Query["approvalStatus"],
                                page = Request.Query["page"]
                            });
                        }
                    }
                }

                coworking.IsActive = !coworking.IsActive;
                coworking.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();

                var statusText = coworking.IsActive ? "ativado" : "inativado";
                TempData["Success"] = $"Coworking '{coworking.Name}' foi {statusText} com sucesso.";

                return RedirectToAction("Coworkings", new
                {
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
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string? product, string? assistedSale, int page = 1, int pageSize = 15)
        {
            var start = startDate?.Date ?? DateTimeHelper.GetBrazilianTime().AddMonths(-1).Date;
            var end = endDate?.Date ?? DateTimeHelper.GetBrazilianTime().Date;
            if (end < start) end = start;

            var report = await _reportService.GetFinancialReportAsync(start, end, product, assistedSale);

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
            ViewBag.AssistedSale = assistedSale ?? ReportAssistedSaleFilter.All;
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

                // Traduzir status para português
                string statusText = payment.Status switch
                {
                    PaymentStatus.Pending => "Aguardando Pagamento",
                    PaymentStatus.Processing => "Processando",
                    PaymentStatus.AwaitingValidation => "Aguardando Validação",
                    PaymentStatus.Paid => "Pago",
                    PaymentStatus.Failed => "Falhou",
                    PaymentStatus.Refunded => "Reembolsado",
                    PaymentStatus.Cancelled => "Cancelado",
                    _ => payment.Status.ToString()
                };

                var paymentData = new
                {
                    type = "Pagamento",
                    id = payment.Id,
                    amount = payment.Amount,
                    status = statusText,
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
        public async Task<IActionResult> ReportCoworkingProductDetail(int id, string productType, DateTime? dateFrom, DateTime? dateTo)
        {
            var dto = await _reportService.GetCoworkingProductDetailAsync(id, productType, dateFrom, dateTo);
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

            // Inicializar configurações de percentual de cartão de crédito se não existirem
            var creditCardFeeConfig = await _systemConfigurationService.GetByKeyAsync("Payment.CreditCardFeePercentage");
            if (creditCardFeeConfig == null)
            {
                await _systemConfigurationService.CreateOrUpdateAsync(
                    key: "Payment.CreditCardFeePercentage",
                    value: "0",
                    description: "Percentual adicional a ser cobrado quando o pagamento for realizado com cartão de crédito. Exemplo: 2.5 para 2,5%",
                    category: "Payment",
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
                    storageProvider.UpdatedAt = DateTimeHelper.GetBrazilianTime();
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
                        CreatedAt = DateTimeHelper.GetBrazilianTime()
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
                        existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
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
                            CreatedAt = DateTimeHelper.GetBrazilianTime()
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
                // Credenciais Sandbox fornecidas pelo usuário
                // Email: hudsonlope@gmail.com (usado apenas para login no painel, não na API)
                // Token: db3453dd-8c96-46c8-9c91-b5549372ed51c6e68eb54829b960fbe67c1d261e73a27c75-0f2a-47d9-9fbe-da8211abbff4

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
                        existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
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
                            CreatedAt = DateTimeHelper.GetBrazilianTime()
                        });
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ Configurações do PagBank (Sandbox) atualizadas com sucesso!<br/>" +
                                     $"<strong>Email da conta:</strong> hudsonlope@gmail.com<br/>" +
                                     $"<strong>Ambiente:</strong> Sandbox (Testes)<br/>" +
                                     $"<strong>Token:</strong> Configurado e salvo<br/><br/>" +
                                     $"💡 <strong>Próximo passo:</strong> Use o botão 'Testar Conexão PagBank' para validar se o token está funcionando corretamente.";
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
                // Verificar se as configurações estão definidas
                var accessToken = await _systemConfigurationService.GetValueAsync("PagBank.AccessToken");
                var useSandbox = await _systemConfigurationService.GetValueAsync("PagBank.UseSandbox");

                if (string.IsNullOrEmpty(accessToken))
                {
                    TempData["Error"] = "Token de acesso PagBank não configurado. Configure primeiro em 'Configurar PagBank Sandbox'.";
                    return RedirectToAction("SystemConfigurations");
                }

                var isSandbox = useSandbox?.ToLower() == "true";
                var environment = isSandbox ? "Sandbox (Testes)" : "Produção";

                // Tentar fazer uma requisição simples para testar a conexão
                // Vamos tentar criar um pedido de teste muito pequeno (R$ 0,01)
                var testOrder = await _pagBankService.CreateOrderAsync(
                    referenceId: $"TEST-{DateTimeHelper.GetBrazilianTime():yyyyMMddHHmmss}",
                    amount: 0.01m,
                    method: PaymentMethod.PIX,
                    clientName: "Teste Conexão",
                    clientEmail: "teste@teste.com",
                    clientTaxId: "12345678909"
                );

                TempData["Success"] = $"✅ Conexão com PagBank ({environment}) validada com sucesso!<br/>" +
                                     $"Pedido de teste criado: <strong>{testOrder.Id}</strong><br/>" +
                                     $"Token configurado e funcionando corretamente.";
                return RedirectToAction("SystemConfigurations");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Token de acesso") || ex.Message.Contains("não configurado"))
            {
                TempData["Error"] = $"❌ Token não configurado: {ex.Message}<br/>Configure primeiro em 'Configurar PagBank Sandbox'.";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (errorMessage.Contains("Unauthorized") || errorMessage.Contains("401"))
                {
                    errorMessage = "Token inválido ou expirado. Verifique se o token está correto e se está usando o token do ambiente correto (sandbox vs produção).";
                }
                else if (errorMessage.Contains("Forbidden") || errorMessage.Contains("403"))
                {
                    errorMessage = "Token sem permissões suficientes. Verifique as permissões do token no painel do PagBank.";
                }

                TempData["Error"] = $"❌ Erro ao validar conexão PagBank: {errorMessage}";
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
                            .ThenInclude(cl => cl.User)
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
                    (p.Subscription.Contract.Client.NomeFantasia != null && p.Subscription.Contract.Client.NomeFantasia.Contains(search)) ||
                    (p.Subscription.Contract.Client.ContactName != null && p.Subscription.Contract.Client.ContactName.Contains(search)) ||
                    (p.Subscription.Contract.Client.User != null && p.Subscription.Contract.Client.User.FullName.Contains(search)));
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
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("Payments");
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Confirmar pagamento (usa transação interna)
                await _paymentService.ConfirmPaymentAsync(paymentId, Guid.NewGuid().ToString());

                // Registrar histórico após confirmação (commit separado para evitar deadlock)
                var history = new PaymentHistory
                {
                    PaymentId = paymentId,
                    Action = "Approved",
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.PaymentHistories.Add(history);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Pagamento aprovado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar pagamento: {ex.Message}";
            }

            return RedirectToAction("Payments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string rejectionReason)
        {
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("Payments");
                }

                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "É necessário informar o motivo da reprovação.";
                    return RedirectToAction("Payments");
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Usar transação única para evitar deadlocks
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Registrar histórico dentro da transação
                    var history = new PaymentHistory
                    {
                        PaymentId = paymentId,
                        Action = "Rejected",
                        RejectionReason = rejectionReason,
                        ActionBy = userId,
                        ActionAt = DateTimeHelper.GetBrazilianTime()
                    };
                    _context.PaymentHistories.Add(history);

                    // Voltar status para Pending
                    payment.Status = PaymentStatus.Pending;
                    payment.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                    payment.ReceiptFilePath = null; // Limpar comprovante para permitir novo envio
                    _context.Payments.Update(payment);

                    // Commit de tudo junto
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Pagamento reprovado. O status foi alterado para 'Aguardando Pagamento'.";
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao reprovar pagamento: {ex.Message}";
            }

            return RedirectToAction("Payments");
        }

        [HttpGet]
        public async Task<IActionResult> GetPaymentHistory(int paymentId, int page = 1, int pageSize = 10)
        {
            var query = _context.PaymentHistories
                .Include(h => h.ActionByUser)
                .Where(h => h.PaymentId == paymentId)
                .OrderByDescending(h => h.ActionAt);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var history = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Json(new
            {
                items = history.Select(h => new
                {
                    id = h.Id,
                    action = h.Action,
                    rejectionReason = h.RejectionReason,
                    actionBy = h.ActionByUser?.FullName ?? "Sistema",
                    actionAt = h.ActionAt.ToString("dd/MM/yyyy HH:mm")
                }),
                pagination = new
                {
                    currentPage = page,
                    totalPages = totalPages,
                    totalItems = totalItems,
                    pageSize = pageSize
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> ViewReceipt(int paymentId)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }

            var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
            var storageService = await storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(payment.ReceiptFilePath);

            if (fileBytes == null)
            {
                return NotFound();
            }

            var fileName = Path.GetFileName(payment.ReceiptFilePath);
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = fileExtension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            // Retornar o arquivo com headers apropriados para visualização inline
            // Para imagens, não adicionar Content-Disposition para permitir exibição direta
            if (fileExtension != ".pdf")
            {
                // Para imagens, adicionar headers para permitir exibição inline e evitar cache
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                return File(fileBytes, contentType);
            }
            else
            {
                // Para PDFs, adicionar Content-Disposition inline
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                return File(fileBytes, contentType);
            }
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

                // Verificar se a tabela CreditPlans já existe e marcar a migration como aplicada se necessário
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT 1 FROM CreditPlans LIMIT 1");
                    // Se chegou aqui, a tabela existe
                    // Verificar se a migration que cria CreditPlans está pendente
                    var pendingCheck = await _context.Database.GetPendingMigrationsAsync();
                    if (pendingCheck.Contains("20260124000542_AddCoworkingSefazConfiguration"))
                    {
                        // Marcar como aplicada se a tabela já existe
                        await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')");
                    }
                }
                catch
                {
                    // Tabela não existe, continuar normalmente
                }

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                var list = pendingMigrations.ToList();
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

                // Tratamento específico para erro de tabela já existente
                if (msg.Contains("already exists") || msg.Contains("Table") && msg.Contains("exists"))
                {
                    // Tentar identificar qual migration causou o erro e marcá-la como aplicada
                    var pending = await _context.Database.GetPendingMigrationsAsync();
                    var pendingList = pending.ToList();

                    // Se o erro menciona CreditPlans, marcar a migration correspondente
                    if (msg.Contains("creditplans", StringComparison.OrdinalIgnoreCase) || msg.Contains("CreditPlans"))
                    {
                        try
                        {
                            await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')");
                            TempData["Info"] = "Migration marcada como aplicada (tabela já existia). Tente executar novamente.";
                            return RedirectToAction("DatabaseManager");
                        }
                        catch
                        {
                            // Continuar com o erro original
                        }
                    }
                }

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
        /// Limpa dados de teste das tabelas relacionadas a Payments, Contracts, Declarations e Commissions.
        /// Permite fazer testes ponta a ponta limpando apenas os dados de negócio, preservando estrutura e configurações.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearTestData()
        {
            try
            {
                // Desabilitar foreign keys temporariamente
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                // Tabelas relacionadas às telas mencionadas (na ordem correta - dependentes primeiro)
                var tablesToClear = new[]
                {
                    "PaymentHistories",      // Histórico de pagamentos
                    "ReservationCommissions", // Comissões de reservas
                    "Commissions",           // Comissões (Dashboard Coworking, Parceiro/Commissions)
                    "Transfers",            // Transferências (relacionado a Commissions)
                    "Payments",              // Pagamentos (MyPayments)
                    "Documents",             // Documentos (relacionado a Contracts)
                    "AddressDeclarations",   // Declarações (Declarations)
                    "Subscriptions",         // Assinaturas (relacionado a Contracts)
                    "Contracts"              // Contratos (MyContracts)
                };

                var clearedTables = new List<string>();
                var errors = new List<string>();

                var connection = _context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

                try
                {
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
                                // Contar registros antes de limpar
                                using var countCmd = connection.CreateCommand();
                                countCmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}`;";
                                var countBefore = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                                // Limpar dados da tabela usando DELETE
                                using var deleteCmd = connection.CreateCommand();
                                deleteCmd.CommandText = $"DELETE FROM `{tableName}`;";
                                var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync();

                                clearedTables.Add($"{tableName} ({rowsDeleted} registro(s) removido(s))");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{tableName}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    // Reabilitar foreign keys
                    await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                    if (!wasOpen) await connection.CloseAsync();
                }

                if (errors.Any())
                {
                    TempData["Error"] = $"Alguns erros ocorreram ao limpar dados: {string.Join("; ", errors)}";
                }

                if (clearedTables.Any())
                {
                    var message = $"Dados de teste limpos com sucesso!<br><br><strong>Tabelas limpas:</strong><ul>";
                    foreach (var table in clearedTables)
                    {
                        message += $"<li>{table}</li>";
                    }
                    message += "</ul>";
                    TempData["Success"] = message;
                }
                else
                {
                    TempData["Info"] = "Nenhuma tabela foi limpa. Verifique se as tabelas existem no banco de dados.";
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData["Error"] = $"Erro ao limpar dados de teste: {msg}";
            }

            return RedirectToAction("DatabaseManager");
        }

        /// <summary>
        /// Adiciona a coluna MustChangePassword na tabela Users se não existir.
        /// Corrige o erro "Unknown column 'u.MustChangePassword' in 'field list'" no login.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMustChangePasswordColumn()
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
                        WHERE table_schema = DATABASE() AND table_name = 'Users' AND column_name = 'MustChangePassword'";
                    columnExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                }
                catch { columnExists = false; }
                finally { if (!wasOpen) await connection.CloseAsync(); }

                if (columnExists)
                {
                    TempData["Info"] = "A coluna Users.MustChangePassword já existe. Nenhuma alteração feita.";
                    return RedirectToAction("DatabaseManager");
                }

                const string alterSql = @"ALTER TABLE Users ADD COLUMN MustChangePassword tinyint(1) NOT NULL DEFAULT 0;";
                await _context.Database.ExecuteSqlRawAsync(alterSql);

                TempData["Success"] = "Coluna Users.MustChangePassword criada com sucesso. O erro no login deve estar resolvido.";
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData["Error"] = "Erro ao adicionar coluna MustChangePassword: " + msg;
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
                // Desabilitar auditoria durante a limpeza para evitar erros
                HttpContext.Items["SkipAudit"] = true;

                // Desabilitar foreign keys temporariamente
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                // Lista completa de tabelas para limpar (na ordem correta - dependentes primeiro)
                // 
                // TABELAS ESSENCIAIS QUE SERÃO MANTIDAS (NÃO LIMPAR):
                // - __EFMigrationsHistory: Histórico de migrações do EF Core (essencial para controle de schema)
                // - SefazConfigurations: Configurações SEFAZ globais (essencial para emissão de notas fiscais, NÃO tem seed)
                //
                // TABELAS ESSENCIAIS COM SEED (SERÃO LIMPAS E RECRIADAS PELO SEED):
                // - Roles: Papéis do sistema (tem seed no SeedService)
                // - Languages: Idiomas suportados (tem seed no SeedService)
                // - Locations: Localidades (tem seed no SeedService)
                // - Services: Serviços disponíveis (tem seed no SeedService)
                // - SystemConfigurations: Configurações globais (tem seed no SeedService)
                //
                // TABELAS COM PRESERVAÇÃO ESPECIAL (lógica customizada no código):
                // - Users: Preservar apenas o admin (tem seed no SeedService)
                // - UserRoles: Preservar apenas vínculos do admin (tem seed no SeedService)
                //
                var tablesToClear = new[]
                {
                    // Tabelas dependentes primeiro (ordem de dependência)
                    "AuditLogs",                      // Logs de auditoria
                    "PaymentHistories",               // Histórico de pagamentos
                    "FiscalNoteScriptExecutions",     // Execuções de scripts de notas fiscais
                    "FiscalNotes",                    // Notas fiscais emitidas
                    "ReservationCommissions",         // Comissões de reservas
                    "Commissions",                     // Comissões gerais
                    "Transfers",                       // Transferências
                    "Reservations",                   // Reservas
                    "Payments",                        // Pagamentos
                    "Subscriptions",                   // Assinaturas
                    "Contracts",                      // Contratos
                    "AddressDeclarations",             // Declarações de endereço
                    "CapacityChangeRequests",          // Solicitações de mudança de capacidade
                    "CoworkingRoomPhotos",            // Fotos de salas
                    "CoworkingRooms",                 // Salas de coworking
                    "CoworkingPhotos",                // Fotos de coworkings
                    "CoworkingDocuments",             // Documentos de coworkings
                    "CoworkingOperatingHours",        // Horários de funcionamento
                    "BlockedDates",                    // Datas bloqueadas
                    "CoworkingPartners",              // Sócios/parceiros de coworkings
                    "CoworkingSefazConfigurations",   // Configurações SEFAZ por coworking (específicas)
                    "CoworkingAddresses",             // Endereços de coworkings
                    "PartnerServices",                // Serviços dos parceiros
                    "LocationCommissions",            // Comissões por localidade (tem seed, será recriado)
                    "PlatformCreditBalances",         // Saldos de créditos da plataforma (tem seed, será recriado)
                    "Coworkings",                     // Coworkings
                    "Clients",                        // Clientes
                    "CreditPurchases",                 // Compras de créditos
                    "ClientCredits",                   // Créditos dos clientes
                    "CreditPlans",                     // Planos de crédito
                    "CreditCards",                     // Cartões de crédito
                    "Documents",                       // Documentos gerais
                    "Interests",                       // Interesses/contatos
                    "CoworkingProducts",              // Produtos de coworkings
                    "CoworkingRatings",               // Avaliações de coworkings
                    "Translations",                    // Traduções (dados dinâmicos, não essenciais)
                    "Services",                        // Serviços (tem seed)
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

                            // Resetar AUTO_INCREMENT após DELETE para evitar problemas com IDs
                            try
                            {
                                using var resetCmd = connection.CreateCommand();
                                resetCmd.CommandText = $"ALTER TABLE `{tableName}` AUTO_INCREMENT = 1;";
                                await resetCmd.ExecuteNonQueryAsync();
                            }
                            catch (Exception resetEx)
                            {
                                // Algumas tabelas podem não ter AUTO_INCREMENT, ignorar erro
                                System.Diagnostics.Debug.WriteLine($"Aviso: Não foi possível resetar AUTO_INCREMENT da tabela {tableName}: {resetEx.Message}");
                            }

                            clearedTables.Add($"{tableName} ({rowsDeleted} registros)");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Tentar TRUNCATE se DELETE falhar (TRUNCATE já reseta AUTO_INCREMENT automaticamente)
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

                // Resetar AUTO_INCREMENT de todas as tabelas importantes ANTES de reabilitar foreign keys
                // Isso garante que os IDs comecem do 1 após a limpeza
                var tablesToResetAutoIncrement = new[] { "AuditLogs", "Users", "Roles", "Clients", "Coworkings", "Contracts", "Payments", "Subscriptions", "Locations", "Services", "SystemConfigurations", "Languages", "LocationCommissions", "UserRoles" };
                var wasConnectionOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasConnectionOpen) await connection.OpenAsync();

                try
                {
                    foreach (var tableName in tablesToResetAutoIncrement)
                    {
                        try
                        {
                            // Verificar se a tabela existe e tem coluna Id com AUTO_INCREMENT
                            // Nomes das tabelas são hardcoded no array, então não há risco de SQL injection
                            using var checkCmd = connection.CreateCommand();
                            var escapedTableName = tableName.Replace("`", "``"); // Escapar backticks para evitar problemas
                            checkCmd.CommandText = $@"
                                SELECT COUNT(*) 
                                FROM information_schema.columns 
                                WHERE table_schema = DATABASE() 
                                AND table_name = '{escapedTableName}' 
                                AND column_name = 'Id' 
                                AND extra LIKE '%auto_increment%'";

                            var hasAutoIncrement = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                            if (hasAutoIncrement)
                            {
                                using var resetCmd = connection.CreateCommand();
                                resetCmd.CommandText = $"ALTER TABLE `{escapedTableName}` AUTO_INCREMENT = 1;";
                                await resetCmd.ExecuteNonQueryAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            // Ignorar erros ao resetar AUTO_INCREMENT (algumas tabelas podem não ter)
                            System.Diagnostics.Debug.WriteLine($"Aviso: Não foi possível resetar AUTO_INCREMENT da tabela {tableName}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    if (!wasConnectionOpen) await connection.CloseAsync();
                }

                // Reabilitar foreign keys
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");

                // Executar seed novamente para garantir que dados essenciais existam
                var seedService = new Application.Services.SeedService(_context);
                await seedService.SeedInitialDataAsync();

                var successMessage = $"Base de dados limpa com sucesso! {clearedTables.Count} tabela(s) foram limpas. " +
                    $"Dados essenciais preservados: Users (admin), UserRoles (admin), SefazConfigurations, __EFMigrationsHistory. " +
                    $"Tabelas com seed recriadas automaticamente: Roles, Languages, Locations, Services, SystemConfigurations.";
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
            finally
            {
                // Reabilitar auditoria após a limpeza
                HttpContext.Items.Remove("SkipAudit");
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

                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();

                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                {
                    TempData["Error"] = "Este serviço não está disponível quando os menus de coworking estão desabilitados.";
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
                    existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
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
                        CreatedAt = DateTimeHelper.GetBrazilianTime()
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
        public async Task<IActionResult> ServiceRemovalRequests(int page = 1, int pageSize = 15)
        {
            var query = _context.PartnerServices
                .Include(ps => ps.Coworking)
                    .ThenInclude(c => c.User)
                .Include(ps => ps.Service)
                .Where(ps => ps.RemovalRequestStatus == RemovalRequestStatus.Pending)
                .OrderByDescending(ps => ps.RemovalRequestedAt);

            var totalItems = await query.CountAsync();
            var totalPages = totalItems > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveServiceRemoval(int partnerServiceId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var partnerService = await _context.PartnerServices
                    .Include(ps => ps.Service)
                    .Include(ps => ps.Coworking)
                    .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId);

                if (partnerService == null)
                {
                    TempData["Error"] = "Solicitação não encontrada.";
                    return RedirectToAction("ServiceRemovalRequests");
                }

                if (partnerService.RemovalRequestStatus != RemovalRequestStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ServiceRemovalRequests");
                }

                var serviceName = partnerService.Service.Name;
                var coworkingName = partnerService.Coworking.Name;

                // Remover o serviço
                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();

                // Registrar no audit log
                await _auditService.LogActionAsync(userId, "ServiceRemovalApproved", "PartnerService", partnerServiceId,
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}",
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Approved\",\"Removed\":true}}");

                TempData["Success"] = $"Remoção do serviço '{serviceName}' do parceiro '{coworkingName}' aprovada e executada com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar remoção: {ex.Message}";
            }

            return RedirectToAction("ServiceRemovalRequests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectServiceRemoval(int partnerServiceId, string rejectionReason)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var partnerService = await _context.PartnerServices
                    .Include(ps => ps.Service)
                    .Include(ps => ps.Coworking)
                    .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId);

                if (partnerService == null)
                {
                    TempData["Error"] = "Solicitação não encontrada.";
                    return RedirectToAction("ServiceRemovalRequests");
                }

                if (partnerService.RemovalRequestStatus != RemovalRequestStatus.Pending)
                {
                    TempData["Error"] = "Esta solicitação já foi processada.";
                    return RedirectToAction("ServiceRemovalRequests");
                }

                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "É necessário informar o motivo da rejeição.";
                    return RedirectToAction("ServiceRemovalRequests");
                }

                var serviceName = partnerService.Service.Name;
                var coworkingName = partnerService.Coworking.Name;

                // Rejeitar a solicitação
                partnerService.RemovalRequestStatus = RemovalRequestStatus.Rejected;
                partnerService.RemovalReviewedAt = DateTimeHelper.GetBrazilianTime();
                partnerService.RemovalReviewedBy = userId;
                partnerService.RemovalRejectionReason = rejectionReason.Trim();
                partnerService.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                // Registrar no audit log
                await _auditService.LogActionAsync(userId, "ServiceRemovalRejected", "PartnerService", partnerServiceId,
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}",
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Rejected\",\"Reason\":\"{rejectionReason}\"}}");

                TempData["Success"] = $"Solicitação de remoção do serviço '{serviceName}' do parceiro '{coworkingName}' rejeitada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao rejeitar remoção: {ex.Message}";
            }

            return RedirectToAction("ServiceRemovalRequests");
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

            var lastExecutions = await _context.FiscalNoteScriptExecutions
                .OrderByDescending(e => e.ExecutedAt)
                .Take(10)
                .ToListAsync();
            ViewBag.LastExecutions = lastExecutions;

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
                        existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                    }
                }
                else
                {
                    // Criar novo
                    config.CreatedAt = DateTimeHelper.GetBrazilianTime();
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
            var executedAt = DateTimeHelper.GetBrazilianTime();
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
            var userName = User.Identity?.Name ?? "Sistema";

            try
            {
                int notesCreated = 0;
                int errors = 0;
                int unsupportedStates = 0;
                var errorMessages = new List<string>();
                var unsupportedStateMessages = new List<string>();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    if (noteType == "Intermediacao")
                    {
                        // Buscar reservas confirmadas e pagas que ainda não têm NF de intermediação (idempotência)
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
                                var idempotencyKey = $"INTERMEDIACAO-R-{reservation.Id}";
                                if (await _context.FiscalNotes.AnyAsync(fn => fn.IdempotencyKey == idempotencyKey))
                                    continue;

                                var coworkingState = reservation.CoworkingAddress?.State?.Trim().ToUpper();
                                if (string.IsNullOrWhiteSpace(coworkingState))
                                {
                                    errors++;
                                    errorMessages.Add($"Reserva #{reservation.Id}: Estado do coworking não informado.");
                                    continue;
                                }
                                if (!_sefazService.IsStateSupported(coworkingState))
                                {
                                    unsupportedStates++;
                                    unsupportedStateMessages.Add($"Reserva #{reservation.Id}: Integração SEFAZ ainda não implementada para o estado {coworkingState}.");
                                    continue;
                                }
                                var config = await _sefazService.GetConfigurationByStateAsync(coworkingState);
                                if (config == null)
                                {
                                    errors++;
                                    errorMessages.Add($"Reserva #{reservation.Id}: Configuração SEFAZ não encontrada para o estado {coworkingState}. Configure na tela de parametrização.");
                                    continue;
                                }
                                var partnerService = await _context.PartnerServices
                                    .Include(ps => ps.Service)
                                    .FirstOrDefaultAsync(ps => ps.CoworkingId == reservation.CoworkingAddress!.CoworkingId && ps.Service!.Name.Contains("Reservar Sala"))
                                    ?? await _context.PartnerServices
                                        .Include(ps => ps.Service)
                                        .FirstOrDefaultAsync(ps => ps.CoworkingId == reservation.CoworkingAddress!.CoworkingId);
                                if (partnerService == null)
                                {
                                    errors++;
                                    errorMessages.Add($"Reserva #{reservation.Id}: Percentagem de comissão não configurada para o parceiro.");
                                    continue;
                                }
                                var commissionAmount = Math.Round(reservation.TotalValue * (partnerService.Percentage / 100m), 2, MidpointRounding.AwayFromZero);
                                var payment = await _context.Payments
                                    .FirstOrDefaultAsync(p => p.ReservationId == reservation.Id && p.Status == PaymentStatus.Paid);

                                var fiscalNote = new FiscalNote
                                {
                                    Type = "Intermediacao",
                                    IdempotencyKey = idempotencyKey,
                                    ReservationId = reservation.Id,
                                    PaymentId = payment?.Id,
                                    CoworkingId = reservation.CoworkingAddress!.CoworkingId,
                                    ClientId = reservation.ClientId,
                                    Amount = commissionAmount,
                                    CommissionAmount = commissionAmount,
                                    ServiceAmount = reservation.TotalValue,
                                    IssueDate = executedAt,
                                    Status = "Pendente",
                                    CreatedAt = executedAt
                                };
                                _context.FiscalNotes.Add(fiscalNote);
                                notesCreated++;
                            }
                            catch (DbUpdateException ex) when (IsDuplicateIdempotencyKeyException(ex))
                            {
                                // Duplicata por concorrência: ignorar (idempotência)
                            }
                            catch (Exception ex)
                            {
                                errors++;
                                errorMessages.Add($"Reserva #{reservation.Id}: {ex.Message}");
                            }
                        }

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
                                var idempotencyKey = $"INTERMEDIACAO-C-{contract.Id}";
                                if (await _context.FiscalNotes.AnyAsync(fn => fn.IdempotencyKey == idempotencyKey))
                                    continue;

                                var coworkingState = contract.CoworkingAddress?.State?.Trim().ToUpper();
                                if (string.IsNullOrWhiteSpace(coworkingState))
                                {
                                    errors++;
                                    errorMessages.Add($"Contrato #{contract.Id}: Estado do coworking não informado.");
                                    continue;
                                }
                                if (!_sefazService.IsStateSupported(coworkingState))
                                {
                                    unsupportedStates++;
                                    unsupportedStateMessages.Add($"Contrato #{contract.Id}: Integração SEFAZ ainda não implementada para o estado {coworkingState}.");
                                    continue;
                                }
                                var config = await _sefazService.GetConfigurationByStateAsync(coworkingState);
                                if (config == null)
                                {
                                    errors++;
                                    errorMessages.Add($"Contrato #{contract.Id}: Configuração SEFAZ não encontrada para o estado {coworkingState}. Configure na tela de parametrização.");
                                    continue;
                                }
                                var partnerService = await _context.PartnerServices
                                    .Include(ps => ps.Service)
                                    .FirstOrDefaultAsync(ps => ps.CoworkingId == contract.CoworkingAddress!.CoworkingId && ps.Service!.Name.Contains("Endereço Fiscal"))
                                    ?? await _context.PartnerServices
                                        .Include(ps => ps.Service)
                                        .FirstOrDefaultAsync(ps => ps.CoworkingId == contract.CoworkingAddress!.CoworkingId);
                                if (partnerService == null)
                                {
                                    errors++;
                                    errorMessages.Add($"Contrato #{contract.Id}: Percentagem de comissão não configurada.");
                                    continue;
                                }
                                var commissionAmount = Math.Round(contract.MonthlyValue * (partnerService.Percentage / 100m), 2, MidpointRounding.AwayFromZero);

                                var fiscalNote = new FiscalNote
                                {
                                    Type = "Intermediacao",
                                    IdempotencyKey = idempotencyKey,
                                    ContractId = contract.Id,
                                    CoworkingId = contract.CoworkingAddress!.CoworkingId,
                                    ClientId = contract.ClientId,
                                    Amount = commissionAmount,
                                    CommissionAmount = commissionAmount,
                                    ServiceAmount = contract.MonthlyValue,
                                    IssueDate = executedAt,
                                    Status = "Pendente",
                                    CreatedAt = executedAt
                                };
                                _context.FiscalNotes.Add(fiscalNote);
                                notesCreated++;
                            }
                            catch (DbUpdateException ex) when (IsDuplicateIdempotencyKeyException(ex))
                            {
                                // Duplicata por concorrência: ignorar (idempotência)
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
                        TempData["Info"] = "Notas Fiscais de Serviço devem ser emitidas pelo parceiro diretamente na SEFAZ.";
                        await transaction.RollbackAsync();
                        return RedirectToAction("SefazConfiguration");
                    }

                    await _context.SaveChangesAsync();

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
                            note.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                            _context.FiscalNotes.Update(note);
                        }
                        catch (Exception ex)
                        {
                            emissionErrors++;
                            errorMessages.Add($"NF #{note.Id}: {ex.Message}");
                            note.Status = "Erro";
                            note.ErrorMessage = ex.Message;
                            note.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                            _context.FiscalNotes.Update(note);
                        }
                    }

                    await _context.SaveChangesAsync();

                    var log = new FiscalNoteScriptExecution
                    {
                        ExecutedAt = executedAt,
                        ExecutedByUserId = userId,
                        ExecutedByUserName = userName,
                        NoteType = noteType,
                        StartDate = startDate,
                        EndDate = endDate,
                        NotesCreated = notesCreated,
                        NotesEmitted = emittedCount,
                        ErrorsCount = errors + emissionErrors,
                        UnsupportedStatesCount = unsupportedStates,
                        ErrorDetails = errorMessages.Count > 0 ? string.Join("; ", errorMessages.Take(20)) : null
                    };
                    _context.FiscalNoteScriptExecutions.Add(log);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    var message = $"Script executado com sucesso! {notesCreated} nota(s) fiscal(is) criada(s), {emittedCount} emitida(s) na SEFAZ.";
                    if (unsupportedStates > 0)
                    {
                        message += $" {unsupportedStates} registro(s) ignorado(s) - estado sem integração implementada.";
                        if (unsupportedStateMessages.Count > 0)
                            TempData["UnsupportedStates"] = string.Join("<br>", unsupportedStateMessages.Take(10));
                    }
                    if (errors + emissionErrors > 0)
                    {
                        message += $" {errors + emissionErrors} erro(s) encontrado(s).";
                        if (errorMessages.Count > 0)
                            TempData["ErrorDetails"] = string.Join("<br>", errorMessages.Take(10));
                    }
                    TempData["Success"] = message;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
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
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .Include(fn => fn.Contract)
                    .ThenInclude(c => c.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .AsQueryable();

            // Excluir notas "Rejeitada" e "Importada" da lista principal por padrão
            // Notas "Importada" devem aparecer apenas em ImportedFiscalNotes para validação
            // Notas "Rejeitada" já foram processadas e não devem aparecer na lista principal
            if (string.IsNullOrEmpty(status))
            {
                query = query.Where(fn => fn.Status != "Rejeitada" && fn.Status != "Importada");
            }
            else
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

        /// <summary>
        /// NF de serviço importadas pelo parceiro — aguardando validação do admin. Aprovar ou rejeitar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ImportedFiscalNotes(int page = 1, int pageSize = 20, int? coworkingId = null)
        {
            try
            {
                var query = _context.FiscalNotes
                    .Include(fn => fn.Payment)
                    .Include(fn => fn.Client).ThenInclude(c => c.User)
                    .Include(fn => fn.Coworking)
                    .Where(fn => fn.Type == "Servico" && fn.Status == "Importada")
                    .AsQueryable();

                if (coworkingId.HasValue)
                    query = query.Where(fn => fn.CoworkingId == coworkingId.Value);

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;

                var list = await query
                    .OrderBy(fn => fn.Coworking != null ? fn.Coworking.Name : "zzz")
                    .ThenByDescending(fn => fn.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.CoworkingId = coworkingId;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = pageSize;
                ViewBag.Coworkings = await _context.Coworkings.OrderBy(c => c.Name).ToListAsync();

                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao carregar notas fiscais importadas: {ex.Message}";
                ViewBag.Coworkings = await _context.Coworkings.OrderBy(c => c.Name).ToListAsync();
                return View(new List<KlyonHubPlatform.Domain.Entities.FiscalNote>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveImportedFiscalNote(int fiscalNoteId)
        {
            var fn = await _context.FiscalNotes.FindAsync(fiscalNoteId);
            if (fn == null || fn.Type != "Servico" || fn.Status != "Importada")
            {
                TempData["Error"] = "Nota fiscal importada não encontrada ou já processada.";
                return RedirectToAction("ImportedFiscalNotes");
            }
            fn.Status = "Emitida";
            fn.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "NF importada aprovada com sucesso.";
            return RedirectToAction("ImportedFiscalNotes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectImportedFiscalNote(int fiscalNoteId, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = "Informe o motivo da rejeição.";
                return RedirectToAction("ImportedFiscalNotes");
            }
            var fn = await _context.FiscalNotes.FindAsync(fiscalNoteId);
            if (fn == null || fn.Type != "Servico" || fn.Status != "Importada")
            {
                TempData["Error"] = "Nota fiscal importada não encontrada ou já processada.";
                return RedirectToAction("ImportedFiscalNotes");
            }
            fn.Status = "Rejeitada";
            fn.ErrorMessage = rejectionReason;
            fn.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "NF importada rejeitada. O parceiro pode enviar uma nova NF.";
            return RedirectToAction("ImportedFiscalNotes");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFiscalNotePdf(int fiscalNoteId)
        {
            var fn = await _context.FiscalNotes.FindAsync(fiscalNoteId);
            if (fn == null || string.IsNullOrEmpty(fn.PdfUrl))
            {
                return NotFound();
            }

            var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
            var storageService = await storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(fn.PdfUrl);

            if (fileBytes == null)
            {
                return NotFound();
            }

            var fileName = Path.GetFileName(fn.PdfUrl) ?? $"nota_fiscal_{fn.Id}.pdf";
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
            return File(fileBytes, "application/pdf");
        }

        [HttpGet]
        public async Task<IActionResult> GetFiscalNoteDetails(int fiscalNoteId)
        {
            var fn = await _context.FiscalNotes
                .Include(f => f.Payment)
                    .ThenInclude(p => p!.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.CoworkingAddress)
                                .ThenInclude(a => a!.Coworking)
                .Include(f => f.Payment)
                    .ThenInclude(p => p!.Reservation)
                        .ThenInclude(r => r!.CoworkingAddress)
                            .ThenInclude(a => a!.Coworking)
                .Include(f => f.Client)
                    .ThenInclude(c => c!.User)
                .Include(f => f.Coworking)
                .FirstOrDefaultAsync(f => f.Id == fiscalNoteId);

            if (fn == null)
            {
                return Json(new { success = false, message = "Nota fiscal não encontrada" });
            }

            // Buscar comissões relacionadas ao pagamento
            var commissions = new List<object>();
            var paymentCommissions = new List<Commission>();
            if (fn.PaymentId.HasValue)
            {
                paymentCommissions = await _context.Commissions
                    .Include(c => c.Coworking)
                    .Where(c => c.PaymentId == fn.PaymentId.Value)
                    .ToListAsync();

                commissions = paymentCommissions.Select(c => new
                {
                    id = c.Id,
                    amount = c.Amount,
                    percentage = c.Percentage,
                    totalPaymentAmount = c.TotalPaymentAmount,
                    isTransferred = c.IsTransferred,
                    transferredAt = c.TransferredAt,
                    createdAt = c.CreatedAt,
                    coworkingName = c.Coworking?.Name ?? "—"
                }).ToList<object>();
            }

            // Buscar repasses (transfers) relacionados
            var transfers = new List<object>();
            var coworkingTransfers = new List<Transfer>();
            if (fn.CoworkingId.HasValue)
            {
                coworkingTransfers = await _context.Transfers
                    .Where(t => t.CoworkingId == fn.CoworkingId.Value)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                transfers = coworkingTransfers.Select(t => new
                {
                    id = t.Id,
                    amount = t.Amount,
                    status = t.Status.ToString(),
                    scheduledDate = t.ScheduledDate,
                    processedAt = t.ProcessedAt,
                    description = t.Description,
                    failureReason = t.FailureReason,
                    createdAt = t.CreatedAt
                }).ToList<object>();
            }

            // Buscar histórico de status da nota fiscal (através de audit logs se disponível)
            var history = new List<object>();
            var auditLogs = await _context.AuditLogs
                .Where(a => a.EntityType == "FiscalNote" && a.EntityId == fiscalNoteId)
                .OrderByDescending(a => a.ActionDate)
                .ToListAsync();

            // Carregar usuários relacionados de uma vez para evitar múltiplas consultas
            var userIds = auditLogs.Where(a => a.UserId.HasValue).Select(a => a.UserId.Value).Distinct().ToList();
            var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

            history = auditLogs.Select(a => new
            {
                action = a.Action,
                details = !string.IsNullOrEmpty(a.NewValues) ? a.NewValues : (a.OldValues ?? "—"),
                createdAt = a.ActionDate,
                user = a.UserId.HasValue && users.ContainsKey(a.UserId.Value) ? users[a.UserId.Value] ?? "—" : "Sistema"
            }).ToList<object>();

            // Identificar o serviço
            string serviceType = "—";
            string serviceDetails = "—";
            if (fn.Payment?.Subscription?.Contract != null)
            {
                serviceType = "Endereço Fiscal";
                var contract = fn.Payment.Subscription.Contract;
                serviceDetails = $"Contrato #{contract.ContractNumber} - {(contract.CoworkingAddress?.Location != null ? $"{contract.CoworkingAddress.Location.City}/{contract.CoworkingAddress.Location.State}" : "—")}";
            }
            else if (fn.Payment?.Reservation != null)
            {
                serviceType = "Reserva de Sala";
                var reservation = fn.Payment.Reservation;
                serviceDetails = $"Reserva #{reservation.Id} - {(reservation.CoworkingAddress?.Location != null ? $"{reservation.CoworkingAddress.Location.City}/{reservation.CoworkingAddress.Location.State}" : "—")}";
            }

            // Calcular valores
            var valorPago = fn.Payment?.Amount ?? 0;
            var valorFaturado = fn.Amount;
            var totalComissoes = paymentCommissions.Sum(c => c.Amount);
            var valorLiquido = valorFaturado - totalComissoes;
            var totalRepassado = coworkingTransfers
                .Where(t => t.Status == TransferStatus.Completed)
                .Sum(t => t.Amount);

            var result = new
            {
                success = true,
                fiscalNote = new
                {
                    id = fn.Id,
                    number = fn.Number ?? "—",
                    accessKey = fn.AccessKey ?? "—",
                    verificationCode = fn.VerificationCode ?? "—",
                    amount = fn.Amount,
                    issueDate = fn.IssueDate,
                    dueDate = fn.DueDate,
                    status = fn.Status,
                    type = fn.Type,
                    errorMessage = fn.ErrorMessage,
                    authorizationProtocol = fn.AuthorizationProtocol,
                    authorizedAt = fn.AuthorizedAt,
                    cancelledAt = fn.CancelledAt,
                    cancellationReason = fn.CancellationReason,
                    createdAt = fn.CreatedAt,
                    updatedAt = fn.UpdatedAt,
                    pdfUrl = fn.PdfUrl,
                    viewUrl = fn.ViewUrl
                },
                client = fn.Client != null ? new
                {
                    id = fn.Client.Id,
                    razaoSocial = fn.Client.RazaoSocial ?? "—",
                    nomeFantasia = fn.Client.NomeFantasia ?? "—",
                    cnpj = fn.Client.CNPJ ?? "—",
                    cpf = fn.Client.CPF ?? "—",
                    contactName = fn.Client.ContactName ?? "—",
                    contactEmail = fn.Client.ContactEmail ?? "—",
                    contactPhone = fn.Client.ContactPhone ?? "—",
                    userFullName = fn.Client.User?.FullName ?? "—"
                } : null,
                coworking = fn.Coworking != null ? new
                {
                    id = fn.Coworking.Id,
                    name = fn.Coworking.Name ?? "—",
                    cnpj = fn.Coworking.CNPJ ?? "—"
                } : null,
                payment = fn.Payment != null ? new
                {
                    id = fn.Payment.Id,
                    amount = fn.Payment.Amount,
                    status = fn.Payment.Status.ToString(),
                    method = fn.Payment.Method.ToString(),
                    dueDate = fn.Payment.DueDate,
                    paidAt = fn.Payment.PaidAt,
                    createdAt = fn.Payment.CreatedAt
                } : null,
                serviceType = serviceType,
                serviceDetails = serviceDetails,
                valorPago = valorPago,
                valorFaturado = valorFaturado,
                valorLiquido = valorLiquido,
                totalComissoes = totalComissoes,
                totalRepassado = totalRepassado,
                commissions = commissions,
                transfers = transfers,
                history = history
            };

            return Json(result);
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

        /// <summary>
        /// Endpoint AJAX para buscar clientes com autocomplete e paginação
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchClients(string term = "", int page = 1, int pageSize = 10)
        {
            var query = _context.Clients
                .Include(c => c.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                var termLower = term.ToLower();
                query = query.Where(c =>
                    (c.RazaoSocial != null && c.RazaoSocial.ToLower().Contains(termLower)) ||
                    (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(termLower)) ||
                    (c.ContactName != null && c.ContactName.ToLower().Contains(termLower)) ||
                    (c.CNPJ != null && c.CNPJ.Contains(term)) ||
                    (c.CPF != null && c.CPF.Contains(term)) ||
                    (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(termLower))
                );
            }

            var totalItems = await query.CountAsync();
            var clients = await query
                .OrderBy(c => c.RazaoSocial ?? c.NomeFantasia ?? c.ContactName ?? "")
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var results = clients.Select(c => new
            {
                id = c.Id,
                text = $"{c.RazaoSocial ?? c.NomeFantasia ?? c.ContactName ?? "Sem nome"} - {c.CNPJ ?? c.CPF ?? "Sem documento"}",
                razaoSocial = c.RazaoSocial,
                nomeFantasia = c.NomeFantasia,
                contactName = c.ContactName,
                cnpj = c.CNPJ,
                cpf = c.CPF
            }).ToList();

            return Json(new
            {
                results = results,
                pagination = new
                {
                    more = (page * pageSize) < totalItems
                }
            });
        }

        /// <summary>
        /// Lista comissões pendentes de emissão de NF de comissão
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CommissionFiscalNotes(int page = 1, int pageSize = 10, int? coworkingId = null, int? clientId = null)
        {
            var query = _context.Commissions
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(ct => ct.CoworkingAddress)
                                .ThenInclude(ca => ca.Location)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(ct => ct.Client)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Reservation)
                        .ThenInclude(r => r.CoworkingAddress)
                            .ThenInclude(ca => ca.Location)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Reservation)
                        .ThenInclude(r => r.Client)
                .Include(c => c.Coworking)
                .Where(c => !_context.FiscalNotes.Any(fn =>
                    (fn.PaymentId == c.PaymentId && fn.Type == "Intermediacao") ||
                    (fn.CommissionAmount == c.Amount && fn.CoworkingId == c.CoworkingId && fn.Type == "Intermediacao")
                ))
                .AsQueryable();

            if (coworkingId.HasValue)
            {
                query = query.Where(c => c.CoworkingId == coworkingId.Value);
            }

            if (clientId.HasValue)
            {
                query = query.Where(c =>
                    (c.Payment.Subscription != null && c.Payment.Subscription.Contract != null && c.Payment.Subscription.Contract.ClientId == clientId.Value) ||
                    (c.Payment.Reservation != null && c.Payment.Reservation.ClientId == clientId.Value)
                );
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var commissions = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CoworkingId = coworkingId;
            ViewBag.ClientId = clientId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Coworkings = await _context.Coworkings.OrderBy(c => c.Name).ToListAsync();

            // Carregar cliente selecionado para exibir no autocomplete
            if (clientId.HasValue)
            {
                var selectedClient = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == clientId.Value);
                ViewBag.SelectedClient = selectedClient;
            }

            return View(commissions);
        }

        /// <summary>
        /// Emite NF de comissão para uma comissão específica
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueCommissionFiscalNote(int commissionId)
        {
            try
            {
                var commission = await _context.Commissions
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.CoworkingAddress)
                                    .ThenInclude(ca => ca.Location)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.CoworkingAddress)
                                .ThenInclude(ca => ca.Location)
                    .Include(c => c.Coworking)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.Client)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.Client)
                    .FirstOrDefaultAsync(c => c.Id == commissionId);

                if (commission == null)
                {
                    TempData["Error"] = "Comissão não encontrada.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Verificar se já existe NF emitida para esta comissão
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn =>
                        (fn.PaymentId == commission.PaymentId && fn.Type == "Intermediacao") ||
                        (fn.CommissionAmount == commission.Amount && fn.CoworkingId == commission.CoworkingId && fn.Type == "Intermediacao" && fn.Status == "Emitida")
                    );

                if (existingNote != null)
                {
                    TempData["Error"] = "Já existe uma NF de comissão emitida para esta comissão.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Determinar o estado baseado no endereço
                string state = "ES"; // Default
                if (commission.Payment?.Subscription?.Contract?.CoworkingAddress?.Location != null)
                {
                    state = commission.Payment.Subscription.Contract.CoworkingAddress.Location.State;
                }
                else if (commission.Payment?.Reservation?.CoworkingAddress?.Location != null)
                {
                    state = commission.Payment.Reservation.CoworkingAddress.Location.State;
                }

                // Verificar se o estado tem integração
                if (!_sefazService.IsStateSupported(state))
                {
                    TempData["Error"] = $"Integração SEFAZ ainda não implementada para o estado {state}.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Buscar configuração SEFAZ da plataforma
                var sefazConfig = await _sefazService.GetConfigurationByStateAsync(state, "Producao");
                if (sefazConfig == null)
                {
                    TempData["Error"] = $"Configuração SEFAZ não encontrada para o estado {state}. Configure em SEFAZ > Configurações.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Criar registro de NF
                var fiscalNote = new FiscalNote
                {
                    Type = "Intermediacao",
                    PaymentId = commission.PaymentId,
                    CoworkingId = commission.CoworkingId,
                    ClientId = commission.Payment?.Subscription?.Contract?.ClientId ?? commission.Payment?.Reservation?.ClientId,
                    ContractId = commission.Payment?.Subscription?.ContractId,
                    ReservationId = commission.Payment?.ReservationId,
                    Amount = commission.Amount,
                    CommissionAmount = commission.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = "Pendente",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();

                // Emitir NF via SEFAZ
                try
                {
                    var emittedNote = await _sefazService.IssueIntermediationNoteAsync(fiscalNote, sefazConfig);
                    _context.FiscalNotes.Update(emittedNote);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"NF de comissão emitida com sucesso! Chave de acesso: {emittedNote.AccessKey}";
                }
                catch (Exception ex)
                {
                    _context.FiscalNotes.Update(fiscalNote);
                    await _context.SaveChangesAsync();
                    TempData["Error"] = $"Erro ao emitir NF: {ex.Message}";
                }

                return RedirectToAction("CommissionFiscalNotes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao processar emissão de NF: {ex.Message}";
                return RedirectToAction("CommissionFiscalNotes");
            }
        }

        /// <summary>
        /// Importa NF de comissão (sem validação - apenas para administrador)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCommissionFiscalNote(int commissionId, IFormFile pdfFile, IFormFile? xmlFile)
        {
            try
            {
                var commission = await _context.Commissions
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.CoworkingAddress)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.CoworkingAddress)
                    .Include(c => c.Coworking)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.Client)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.Client)
                    .FirstOrDefaultAsync(c => c.Id == commissionId);

                if (commission == null)
                {
                    TempData["Error"] = "Comissão não encontrada.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Verificar se já existe NF emitida para esta comissão
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn =>
                        (fn.PaymentId == commission.PaymentId && fn.Type == "Intermediacao") ||
                        (fn.CommissionAmount == commission.Amount && fn.CoworkingId == commission.CoworkingId && fn.Type == "Intermediacao")
                    );

                if (existingNote != null)
                {
                    TempData["Error"] = "Já existe uma NF de comissão para esta comissão.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData["Error"] = "Selecione o arquivo PDF da nota fiscal.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "O arquivo deve ser um PDF.";
                    return RedirectToAction("CommissionFiscalNotes");
                }

                // Salvar PDF
                var storage = await _storageFactory.GetStorageServiceAsync();
                var pdfFileName = $"imported_commission_{commissionId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                using var ms = new MemoryStream();
                await pdfFile.CopyToAsync(ms);
                var pdfPath = await storage.SaveFileAsync(ms.ToArray(), pdfFileName, $"invoices/imported/admin");

                // Criar registro de NF (sem validação - status direto como "Emitida")
                var fiscalNote = new FiscalNote
                {
                    Type = "Intermediacao",
                    PaymentId = commission.PaymentId,
                    CoworkingId = commission.CoworkingId,
                    ClientId = commission.Payment?.Subscription?.Contract?.ClientId ?? commission.Payment?.Reservation?.ClientId,
                    ContractId = commission.Payment?.Subscription?.ContractId,
                    ReservationId = commission.Payment?.ReservationId,
                    Amount = commission.Amount,
                    CommissionAmount = commission.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = "Emitida", // Status direto como "Emitida" - sem validação
                    PdfUrl = pdfPath,
                    CreatedAt = DateTime.UtcNow
                };

                // Se houver XML, salvar também
                if (xmlFile != null && xmlFile.Length > 0 && xmlFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var xmlMs = new MemoryStream();
                    await xmlFile.CopyToAsync(xmlMs);
                    var xmlBytes = xmlMs.ToArray();
                    var xmlContent = System.Text.Encoding.UTF8.GetString(xmlBytes);
                    fiscalNote.XmlContent = xmlContent;
                }

                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();

                TempData["Success"] = "NF de comissão importada com sucesso!";
                return RedirectToAction("CommissionFiscalNotes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao importar NF: {ex.Message}";
                return RedirectToAction("CommissionFiscalNotes");
            }
        }

        /// <summary>
        /// Lista solicitações de repasse (Transfers) com paginação e filtros para o administrador aprovar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Transfers(
            int page = 1,
            int pageSize = 15,
            int? coworkingId = null,
            TransferStatus? status = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var query = _context.Transfers
                .Include(t => t.Coworking)
                .AsQueryable();

            if (coworkingId.HasValue)
                query = query.Where(t => t.CoworkingId == coworkingId.Value);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (dateFrom.HasValue)
                query = query.Where(t => t.CreatedAt >= dateFrom.Value);

            if (dateTo.HasValue)
            {
                var endOfDay = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.CreatedAt <= endOfDay);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var transfers = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CoworkingId = coworkingId;
            ViewBag.Status = status;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Coworkings = await _context.Coworkings.OrderBy(c => c.Name).ToListAsync();
            ViewBag.PendingCount = await _context.Transfers.CountAsync(t => t.Status == TransferStatus.Pending);

            return View(transfers);
        }

        /// <summary>
        /// Aprova e processa uma solicitação de repasse (chama ProcessTransferAsync).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTransfer(int transferId)
        {
            try
            {
                var transfer = await _context.Transfers.FindAsync(transferId);
                if (transfer == null)
                {
                    TempData["Error"] = "Solicitação de repasse não encontrada.";
                    return RedirectToAction("Transfers");
                }

                if (transfer.Status != TransferStatus.Pending)
                {
                    TempData["Error"] = "Apenas repasses com status \"Pendente\" podem ser aprovados.";
                    return RedirectToAction("Transfers");
                }

                await _commissionService.ApproveTransferAsync(transferId);
                TempData["Success"] = $"Repasse de R$ {transfer.Amount:N2} aprovado. Status: Aguardando NF. A transferência está agendada para o último dia do mês subsequente.";
                return RedirectToAction("Transfers");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Transfers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar repasse: {ex.Message}";
                return RedirectToAction("Transfers");
            }
        }

        /// <summary>
        /// Exibe todos os detalhes de uma transferência, incluindo comissões, pagamentos e clientes relacionados.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TransferDetails(int id)
        {
            var transfer = await _context.Transfers
                .Include(t => t.Coworking)
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null)
            {
                TempData["Error"] = "Transferência não encontrada.";
                return RedirectToAction("Transfers");
            }

            var relatedCommissions = new List<Commission>();

            // Preferir IDs armazenados em Description (ex.: Aguardando NF)
            var commissionIds = new List<int>();
            if (!string.IsNullOrEmpty(transfer.Description) && transfer.Description.Contains("CommissionIds:"))
            {
                var idsPart = transfer.Description.Split("CommissionIds:")[1];
                if (!string.IsNullOrEmpty(idsPart))
                {
                    foreach (var idStr in idsPart.Split(','))
                    {
                        if (int.TryParse(idStr.Trim(), out var cid))
                            commissionIds.Add(cid);
                    }
                }
            }

            if (commissionIds.Count > 0)
            {
                relatedCommissions = await _context.Commissions
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.Client)
                                    .ThenInclude(cl => cl.User)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.Client)
                                .ThenInclude(cl => cl.User)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.CoworkingAddress)
                    .Where(c => commissionIds.Contains(c.Id))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }

            if (relatedCommissions.Count == 0)
            {
                // Fallback: comissões transferidas no mesmo período
                var transferDateStart = transfer.CreatedAt.AddHours(-1);
                var transferDateEnd = transfer.CreatedAt.AddHours(1);
                relatedCommissions = await _context.Commissions
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.Client)
                                    .ThenInclude(cl => cl.User)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.Client)
                                .ThenInclude(cl => cl.User)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.CoworkingAddress)
                    .Where(c => c.CoworkingId == transfer.CoworkingId
                        && c.IsTransferred
                        && c.TransferredAt.HasValue
                        && c.TransferredAt >= transferDateStart
                        && c.TransferredAt <= transferDateEnd)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }

            // Se ainda não encontrou, buscar pelo ReservationId (repasse automático)
            if (relatedCommissions.Count == 0 && transfer.ReservationId.HasValue)
            {
                var reservationPayment = await _context.Payments
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r.Client)
                            .ThenInclude(c => c.User)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(p => p.ReservationId == transfer.ReservationId);

                if (reservationPayment != null)
                {
                    var commission = await _context.Commissions
                        .Include(c => c.Payment)
                            .ThenInclude(p => p.Reservation)
                                .ThenInclude(r => r.Client)
                                    .ThenInclude(cl => cl.User)
                        .Include(c => c.Payment)
                            .ThenInclude(p => p.Reservation)
                                .ThenInclude(r => r.CoworkingAddress)
                        .FirstOrDefaultAsync(c => c.PaymentId == reservationPayment.Id && c.CoworkingId == transfer.CoworkingId);

                    if (commission != null)
                    {
                        relatedCommissions.Add(commission);
                    }
                }
            }

            ViewBag.RelatedCommissions = relatedCommissions;
            return View(transfer);
        }

        /// <summary>
        /// Programação de repasses: lista transferências agendadas (Aguardando NF) por parceiro, com modal de detalhes.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TransferSchedule()
        {
            var list = await _context.Transfers
                .Include(t => t.Coworking)
                .Where(t => t.Status == TransferStatus.AwaitingInvoice)
                .OrderBy(t => t.ScheduledDate)
                .ThenBy(t => t.Coworking!.Name)
                .ToListAsync();

            var byCoworking = list
                .GroupBy(t => t.CoworkingId)
                .Select(g => new
                {
                    Coworking = g.First().Coworking,
                    Transfers = g.OrderBy(t => t.ScheduledDate).ToList()
                })
                .OrderBy(x => x.Coworking?.Name ?? "")
                .ToList();

            ViewBag.ByCoworking = byCoworking;
            ViewBag.TotalAmount = list.Sum(t => t.Amount);
            return View(list);
        }

        /// <summary>
        /// Retorna HTML parcial com detalhes da transferência (para modal na Programação).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TransferDetailPartial(int id)
        {
            var transfer = await _context.Transfers
                .Include(t => t.Coworking)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null)
                return NotFound();

            var commissionIds = new List<int>();
            if (!string.IsNullOrEmpty(transfer.Description) && transfer.Description.Contains("CommissionIds:"))
            {
                var idsPart = transfer.Description.Split("CommissionIds:")[1];
                if (!string.IsNullOrEmpty(idsPart))
                {
                    foreach (var idStr in idsPart.Split(','))
                    {
                        if (int.TryParse(idStr.Trim(), out var cid))
                            commissionIds.Add(cid);
                    }
                }
            }

            var relatedCommissions = new List<Commission>();
            if (commissionIds.Count > 0)
            {
                relatedCommissions = await _context.Commissions
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Subscription)
                            .ThenInclude(s => s.Contract)
                                .ThenInclude(ct => ct.Client)
                                    .ThenInclude(cl => cl.User)
                    .Include(c => c.Payment)
                        .ThenInclude(p => p.Reservation)
                            .ThenInclude(r => r.Client)
                                .ThenInclude(cl => cl.User)
                    .Where(c => commissionIds.Contains(c.Id))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }

            ViewBag.RelatedCommissions = relatedCommissions;
            return PartialView("_TransferDetailPartial", transfer);
        }

        [HttpGet]
        public async Task<IActionResult> QuickClientRegistration()
        {
            // Buscar endereços disponíveis
            var availableAddresses = await _coworkingService.GetAvailableAddressesAsync();
            var addressesWithLocation = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .Where(a => availableAddresses.Select(addr => addr.Id).Contains(a.Id))
                .ToListAsync();

            ViewBag.AvailableAddresses = addressesWithLocation;
            ViewBag.Locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickClientRegistration(
            string contactEmail,
            string contactName,
            string? razaoSocial,
            string? nomeFantasia,
            string? cnpj,
            string? cpf,
            string? contactPhone,
            string? address,
            string? city,
            string? state,
            string? zipCode,
            int coworkingAddressId,
            decimal monthlyValue,
            string paymentMethod = "PIX")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(contactEmail) || string.IsNullOrWhiteSpace(contactName))
                {
                    TempData["Error"] = "Email e Nome do Contato são obrigatórios.";
                    return RedirectToAction("QuickClientRegistration");
                }

                if (monthlyValue <= 0)
                {
                    TempData["Error"] = "O valor anual deve ser maior que zero.";
                    return RedirectToAction("QuickClientRegistration");
                }

                // Verificar se o endereço está disponível (parceiro ativo, endereço ativo)
                var selectedAddress = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);

                if (selectedAddress == null)
                {
                    TempData["Error"] = "Endereço não encontrado.";
                    return RedirectToAction("QuickClientRegistration");
                }
                if (!selectedAddress.Coworking.IsActive)
                {
                    TempData["Error"] = "O parceiro deste endereço está suspenso. Não é possível criar novos contratos.";
                    return RedirectToAction("QuickClientRegistration");
                }
                if (selectedAddress.AddressStatus != AddressStatus.Active)
                {
                    TempData["Error"] = "O endereço não está ativo para novas vendas (suspenso ou removido).";
                    return RedirectToAction("QuickClientRegistration");
                }
                if (!selectedAddress.IsAvailable || selectedAddress.CurrentContracts >= selectedAddress.MaxContracts)
                {
                    TempData["Error"] = "O endereço selecionado não está disponível para contrato.";
                    return RedirectToAction("QuickClientRegistration");
                }

                // Buscar ou criar usuário
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == contactEmail);
                User user;

                if (existingUser != null)
                {
                    user = existingUser;
                    // Verificar se já existe cliente para este usuário
                    var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    if (existingClient != null)
                    {
                        TempData["Error"] = $"Já existe um cliente cadastrado com o email {contactEmail}. Use a edição de cliente para criar um novo contrato.";
                        return RedirectToAction("QuickClientRegistration");
                    }
                }
                else
                {
                    // Gerar senha com 4 primeiras letras do email
                    var defaultPassword = contactEmail.Length >= 4
                        ? contactEmail.Substring(0, 4).ToLower()
                        : contactEmail.ToLower();

                    // Criar novo usuário
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                    user = new User
                    {
                        Email = contactEmail,
                        PasswordHash = passwordHash,
                        FullName = contactName,
                        IsActive = true,
                        EmailVerified = false,
                        MustChangePassword = true // Forçar alteração de senha no primeiro login
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Atribuir role de Client
                    var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Client");
                    if (clientRole != null)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = clientRole.Id
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // Criar cliente
                var client = new Client
                {
                    UserId = user.Id,
                    ContactEmail = contactEmail,
                    ContactName = contactName,
                    RazaoSocial = razaoSocial,
                    NomeFantasia = nomeFantasia,
                    ContactPhone = contactPhone,
                    Address = address,
                    City = city,
                    State = state,
                    ZipCode = zipCode?.Replace("-", ""),
                    IsActive = true
                };

                // Processar CNPJ
                if (!string.IsNullOrWhiteSpace(cnpj))
                {
                    var cnpjClean = cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    if (cnpjClean.Length == 14)
                    {
                        // Verificar duplicação
                        var existingCNPJ = await _context.Clients
                            .Where(c => c.CNPJ != null &&
                                c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                            .FirstOrDefaultAsync();

                        if (existingCNPJ != null)
                        {
                            TempData["Error"] = $"O CNPJ {cnpj} já está cadastrado para outro cliente.";
                            return RedirectToAction("QuickClientRegistration");
                        }
                        client.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    }
                }

                // Processar CPF
                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    var cpfClean = cpf.Replace(".", "").Replace("-", "").Trim();
                    if (cpfClean.Length == 11)
                    {
                        // Verificar duplicação
                        var existingCPF = await _context.Clients
                            .Where(c => c.CPF != null &&
                                c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean)
                            .FirstOrDefaultAsync();

                        if (existingCPF != null)
                        {
                            TempData["Error"] = $"O CPF {cpf} já está cadastrado para outro cliente.";
                            return RedirectToAction("QuickClientRegistration");
                        }
                        client.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    }
                }

                // Remover máscaras do telefone
                if (!string.IsNullOrEmpty(client.ContactPhone))
                {
                    client.ContactPhone = client.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                }

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Converter valor anual para mensal (o monthlyValue recebido é anual, precisa dividir por 12)
                var monthlyValueForContract = monthlyValue / 12;

                var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                // Criar contrato (venda assistida: admin responsável registrado)
                var contract = await _contractService.CreateContractAsync(client.Id, coworkingAddressId, monthlyValueForContract, assistedSaleAdminId: adminUserId > 0 ? adminUserId : null);

                var newValues = System.Text.Json.JsonSerializer.Serialize(new { IsAssistedSale = true, AssistedSaleAdminId = adminUserId, Origin = "VendaAssistida" });
                await _auditService.LogActionAsync(adminUserId > 0 ? adminUserId : null, "ContractCreated", "Contract", contract.Id, null, newValues,
                    HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.FirstOrDefault());

                // Buscar o pagamento criado para gerar o código PIX
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.ContractId == contract.Id);

                Payment? payment = null;
                if (subscription != null)
                {
                    payment = await _context.Payments
                        .Where(p => p.SubscriptionId == subscription.Id && p.Status == PaymentStatus.Pending)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (payment != null)
                    {
                        try
                        {
                            // Gerar código PIX automaticamente
                            var pixCode = await _paymentService.GeneratePixCodeAsync(payment.Id);
                            TempData["PixCode"] = pixCode;
                            TempData["PaymentId"] = payment.Id;
                        }
                        catch (Exception pixEx)
                        {
                            // Se falhar ao gerar PIX, ainda mostra sucesso mas avisa que o PIX será gerado depois
                            TempData["Warning"] = $"Contrato criado, mas o código PIX não pôde ser gerado automaticamente: {pixEx.Message}. O código será gerado quando o cliente acessar a página de pagamento.";
                        }
                    }
                }

                // Enviar email com credenciais e link para PaymentDetails
                if (payment != null && existingUser == null) // Só envia email se for novo usuário
                {
                    try
                    {
                        var baseUrl = await _systemConfigurationService.GetValueAsync("AppSettings.BaseUrl") ?? "https://localhost:5001";
                        var defaultPassword = contactEmail.Length >= 4
                            ? contactEmail.Substring(0, 4).ToLower()
                            : contactEmail.ToLower();

                        // Criar token de autenticação automática usando PasswordResetToken
                        var authToken = Guid.NewGuid().ToString();
                        user.PasswordResetToken = authToken;
                        user.PasswordResetExpires = DateTimeHelper.GetBrazilianTime().AddDays(7); // Token válido por 7 dias
                        await _context.SaveChangesAsync();

                        var paymentDetailsUrl = $"{baseUrl}/Auth/AutoLogin?token={authToken}&redirect=/Client/PaymentDetails/{payment.Id}";

                        await _emailService.SendClientCredentialsEmailAsync(
                            contactEmail,
                            contactName,
                            contactEmail,
                            defaultPassword,
                            paymentDetailsUrl
                        );
                    }
                    catch (Exception emailEx)
                    {
                        // Log do erro mas não falha o cadastro
                        TempData["Warning"] = $"Cliente cadastrado com sucesso, mas houve um erro ao enviar o email: {emailEx.Message}";
                    }
                }

                TempData["Success"] = $"Cliente cadastrado e contrato criado com sucesso! Contrato #{contract.ContractNumber}";
                if (payment != null)
                {
                    return RedirectToAction("QuickClientRegistrationSuccess", "Admin", new { paymentId = payment.Id });
                }
                return RedirectToAction("Contracts", "Admin");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar cliente e criar contrato: {ex.Message}";
                return RedirectToAction("QuickClientRegistration");
            }
        }

        [HttpGet]
        public async Task<IActionResult> QuickClientRegistrationSuccess(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Pagamento não encontrado.";
                return RedirectToAction("Contracts", "Admin");
            }

            ViewBag.Payment = payment;
            ViewBag.PixCode = payment.PixCode;
            ViewBag.PixExpiresAt = payment.PixExpiresAt;

            return View();
        }

        private static bool IsDuplicateIdempotencyKeyException(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("IX_FiscalNotes_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
        }

        #region Endpoints AJAX para Modal de Percentagem

        /// <summary>
        /// Retorna os serviços de um coworking específico (para modal de percentagem)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCoworkingServicesJson(int coworkingId)
        {
            try
            {
                var coworking = await _context.Coworkings.FindAsync(coworkingId);
                if (coworking == null)
                    return Json(new { success = false, message = "Coworking não encontrado." });

                // Verificar se os menus de coworking estão habilitados
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

                var inactiveServices = _context.Services.Where(ps => !ps.IsActive).Select(x => x.Name).ToList();

                // Carregar serviços do parceiro
                var partnerServicesQuery = _context.PartnerServices
                    .Include(ps => ps.Service)
                    .Where(ps => ps.CoworkingId == coworkingId && ps.Service != null && ps.Service.IsActive);

                if (!showCoworkingMenus)
                    partnerServicesQuery = partnerServicesQuery.Where(ps => ps.Service != null && !inactiveServices.Contains(ps.Service.Name));

                var partnerServices = await partnerServicesQuery
                    .OrderBy(ps => ps.Service.DisplayOrder)
                    .Select(ps => new
                    {
                        id = ps.Id,
                        serviceId = ps.ServiceId,
                        serviceName = ps.Service.Name,
                        serviceDescription = ps.Service.Description,
                        percentage = ps.Percentage,
                        createdAt = ps.CreatedAt.ToString("dd/MM/yyyy")
                    })
                    .ToListAsync();

                // Carregar serviços disponíveis para adicionar
                IQueryable<Service> servicesQuery = _context.Services.Where(s => s.IsActive && s.Category == CategoryService.Partner);
                if (!showCoworkingMenus)
                    servicesQuery = servicesQuery.Where(s => !inactiveServices.Contains(s.Name));

                var existingServiceIds = partnerServices.Select(ps => ps.serviceId).ToList();
                var availableServices = await servicesQuery
                    .Where(s => !existingServiceIds.Contains(s.Id))
                    .OrderBy(s => s.DisplayOrder)
                    .Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        description = s.Description
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    coworkingName = coworking.Name,
                    partnerServices,
                    availableServices
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao carregar serviços: {ex.Message}" });
            }
        }

        /// <summary>
        /// Salva ou atualiza a percentagem de um serviço via AJAX
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePartnerServiceJson([FromBody] SavePartnerServiceRequest request)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Dados inválidos." });

                // Validar se o coworking existe
                var coworking = await _context.Coworkings.FindAsync(request.CoworkingId);
                if (coworking == null)
                    return Json(new { success = false, message = "Coworking não encontrado." });

                // Validar se o serviço existe
                var service = await _context.Services.FindAsync(request.ServiceId);
                if (service == null)
                    return Json(new { success = false, message = "Serviço não encontrado." });

                // Verificar se os menus de coworking estão habilitados
                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();
                
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                    return Json(new { success = false, message = "Este serviço não está disponível quando os menus de coworking estão desabilitados." });

                // Validar percentagem
                if (request.Percentage < 0 || request.Percentage > 100)
                    return Json(new { success = false, message = "A percentagem deve estar entre 0 e 100." });

                if (request.Id > 0)
                {
                    // Editar existente
                    var existing = await _context.PartnerServices.FindAsync(request.Id);
                    if (existing == null || existing.CoworkingId != request.CoworkingId)
                        return Json(new { success = false, message = "Serviço do parceiro não encontrado." });

                    existing.ServiceId = request.ServiceId;
                    existing.Percentage = request.Percentage;
                    existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                }
                else
                {
                    // Verificar se já existe este serviço para este parceiro
                    var existing = await _context.PartnerServices
                        .FirstOrDefaultAsync(ps => ps.CoworkingId == request.CoworkingId && ps.ServiceId == request.ServiceId);

                    if (existing != null)
                        return Json(new { success = false, message = "Este serviço já está cadastrado para este parceiro." });

                    // Criar novo
                    var partnerService = new PartnerService
                    {
                        CoworkingId = request.CoworkingId,
                        ServiceId = request.ServiceId,
                        Percentage = request.Percentage,
                        CreatedAt = DateTimeHelper.GetBrazilianTime()
                    };

                    _context.PartnerServices.Add(partnerService);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Serviço salvo com sucesso!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao salvar serviço: {ex.Message}" });
            }
        }

        /// <summary>
        /// Remove um serviço do parceiro via AJAX
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartnerServiceJson([FromBody] DeletePartnerServiceRequest request)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Dados inválidos." });

                var partnerService = await _context.PartnerServices.FindAsync(request.Id);
                if (partnerService == null)
                    return Json(new { success = false, message = "Serviço do parceiro não encontrado." });

                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Serviço removido com sucesso!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao remover serviço: {ex.Message}" });
            }
        }

        public class SavePartnerServiceRequest
        {
            public int Id { get; set; }
            public int CoworkingId { get; set; }
            public int ServiceId { get; set; }
            public decimal Percentage { get; set; }
        }

        public class DeletePartnerServiceRequest
        {
            public int Id { get; set; }
        }

        #endregion

        #region Invoice Validation (Validação de Notas Fiscais)

        /// <summary>
        /// Lista de fechamentos para validação de NF
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> InvoiceValidation()
        {
            var settlements = await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Include(s => s.PartnerInvoices)
                .OrderByDescending(s => s.ReferenceYear)
                .ThenByDescending(s => s.ReferenceMonth)
                .ToListAsync();

            var summary = await _monthlySettlementService.GetDashboardSummaryAsync();
            ViewBag.Summary = summary;

            return View(settlements);
        }

        /// <summary>
        /// Aprovar nota fiscal
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveInvoice(int invoiceId, string? notes)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _monthlySettlementService.ApproveInvoiceAsync(invoiceId, userId, notes);

                TempData["Success"] = "Nota fiscal aprovada com sucesso! A transferência será agendada automaticamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar nota fiscal: {ex.Message}";
            }

            return RedirectToAction("InvoiceValidation");
        }

        /// <summary>
        /// Rejeitar nota fiscal
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectInvoice(int invoiceId, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "Por favor, informe o motivo da rejeição.";
                    return RedirectToAction("InvoiceValidation");
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _monthlySettlementService.RejectInvoiceAsync(invoiceId, userId, rejectionReason);

                TempData["Success"] = "Nota fiscal rejeitada. O parceiro será notificado para enviar uma nova NF.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao rejeitar nota fiscal: {ex.Message}";
            }

            return RedirectToAction("InvoiceValidation");
        }

        /// <summary>
        /// Executar transferência manualmente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteTransfer(int settlementId)
        {
            try
            {
                await _monthlySettlementService.ExecuteTransferAsync(settlementId);
                TempData["Success"] = "Transferência executada com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao executar transferência: {ex.Message}";
            }

            return RedirectToAction("InvoiceValidation");
        }

        /// <summary>
        /// Forçar fechamento de mês manualmente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceCloseMonth(int coworkingId, int month, int year)
        {
            try
            {
                await _monthlySettlementService.CloseMonthAsync(coworkingId, month, year);
                TempData["Success"] = $"Mês {month}/{year} fechado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao fechar mês: {ex.Message}";
            }

            return RedirectToAction("InvoiceValidation");
        }

        /// <summary>
        /// Gerar NF automática para um fechamento
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAutomaticInvoice(int settlementId)
        {
            try
            {
                await _monthlySettlementService.GenerateAutomaticInvoiceAsync(settlementId);
                TempData["Success"] = "Nota fiscal gerada automaticamente!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao gerar NF automática: {ex.Message}";
            }

            return RedirectToAction("InvoiceValidation");
        }

        #endregion
    }
}
