using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using static KlyonHubPlatform.Domain.Core.Constants.D4SignConfigKeys;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using static KlyonHubPlatform.Domain.Core.Constants.StorageConfigKeys;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using AutoMapper;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Api.Dtos.Admin;
using KlyonHubPlatform.Api.Extensions;
using KlyonHubPlatform.Api.Requests;
using KlyonHubPlatform.Application.Commands;
namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = Administrator)]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IReportService _reportService;
        private readonly IAdminDashboardService _adminDashboardService;
        private readonly ILocationService _locationService;
        private readonly IAdminService _adminService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IPaymentService _paymentService;
        private readonly IAsaasService _asaasService;
        private readonly IAuditService _auditService;
        private readonly ISefazService _sefazService;
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ICommissionService _commissionService;
        private readonly IMonthlySettlementService _monthlySettlementService;
        private readonly IAppBaseUrlProvider _appBaseUrlProvider;
        private readonly IMapper _mapper;
        private readonly IQuickClientRegistrationService _quickClientRegistrationService;
        private readonly IAdminApprovalService _adminApprovalService;
        private readonly IAdminCoworkingService _adminCoworkingService;
        private readonly IAdminDataService _adminDataService;
        private readonly IAdminDbService _adminDbService;
        private readonly StorageServiceFactory _storageFactory;
        public AdminController(
            IUserService userService,
            IReportService reportService,
            IAdminDashboardService adminDashboardService,
            ILocationService locationService,
            IAdminService adminService,
            ISystemConfigurationService systemConfigurationService,
            IPaymentService paymentService,
            IAsaasService asaasService,
            IAuditService auditService,
            ISefazService sefazService,
            IContractService contractService,
            ICoworkingService coworkingService,
            IAuthService authService,
            IEmailService emailService,
            ICommissionService commissionService,
            IMonthlySettlementService monthlySettlementService,
            IAppBaseUrlProvider appBaseUrlProvider,
            IMapper mapper,
            IQuickClientRegistrationService quickClientRegistrationService,
            IAdminApprovalService adminApprovalService,
            IAdminCoworkingService adminCoworkingService,
            IAdminDataService adminDataService,
            IAdminDbService adminDbService,
            StorageServiceFactory storageFactory)
        {
            _userService = userService;
            _reportService = reportService;
            _adminDashboardService = adminDashboardService;
            _locationService = locationService;
            _adminService = adminService;
            _systemConfigurationService = systemConfigurationService;
            _paymentService = paymentService;
            _asaasService = asaasService;
            _auditService = auditService;
            _sefazService = sefazService;
            _contractService = contractService;
            _coworkingService = coworkingService;
            _authService = authService;
            _emailService = emailService;
            _commissionService = commissionService;
            _monthlySettlementService = monthlySettlementService;
            _storageFactory = storageFactory;
            _appBaseUrlProvider = appBaseUrlProvider;
            _mapper = mapper;
            _quickClientRegistrationService = quickClientRegistrationService;
            _adminApprovalService = adminApprovalService;
            _adminCoworkingService = adminCoworkingService;
            _adminDataService = adminDataService;
            _adminDbService = adminDbService;
            _storageFactory = storageFactory;
        }
        private IActionResult ApplyAdminResult(AdminCommandResult result, bool isAjax = false, string? exceptionMessage = null)
        {
            if (exceptionMessage != null)
            {
                if (isAjax) return Json(new { success = false, message = exceptionMessage });
                TempData[ErrorMessages.TempDataKey] = exceptionMessage;
                return RedirectToAction(ActionNames.Dashboard);
            }
            if (isAjax)
                return Json(new { success = result.Success, message = result.Success ? result.SuccessMessage : result.ErrorMessage });
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                return result.RedirectCoworkingId.HasValue
                    ? RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId })
                    : RedirectToAction(ActionNames.Dashboard);
            }
            TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            return result.RedirectCoworkingId.HasValue
                ? RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId })
                : RedirectToAction(ActionNames.Dashboard);
        }
        public IActionResult Index()
        {
            return RedirectToAction(ActionNames.Dashboard);
        }
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken = default)
        {
            var model = await _adminDashboardService.GetDashboardViewModelAsync(cancellationToken);
            this.SetDashboardViewBag(model);
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetDashboardList(string type, string? filter = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _adminDashboardService.GetDashboardListAsync(type, filter, page, pageSize, cancellationToken);
                if (result == null)
                    return Json(new { success = false, message = JsonMessage.TipoInvalido });
                var viewAllUrl = GetDashboardViewAllUrl(type, filter);
                return Json(new { success = true, data = result.Data, viewAllUrl, totalCount = result.TotalCount, page = type.ToLower() == "users" ? page : (int?)null, pageSize = type.ToLower() == "users" ? pageSize : (int?)null });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao carregar dados: {ex.Message}" });
            }
        }

        private string GetDashboardViewAllUrl(string type, string? filter)
        {
            return type.ToLower() switch
            {
                "users" => filter == "inactive" ? Url.Action(ActionNames.Users, ControllerNames.Admin, new { status = "inactive" })! :
                           filter == "unverified" ? Url.Action(ActionNames.Users, ControllerNames.Admin, new { emailVerified = StringBooleanValue.False })! :
                           Url.Action(ActionNames.Users, ControllerNames.Admin)!,
                "coworkings" => Url.Action(ActionNames.Coworkings, ControllerNames.Admin)!,
                "contracts" => filter == "Active" ? Url.Action(ActionNames.Contracts, ControllerNames.Admin, new { status = "Active" })! : Url.Action(ActionNames.Contracts, ControllerNames.Admin)!,
                "locations" => Url.Action(ActionNames.Locations, ControllerNames.Admin)!,
                "payments" => filter == "Pending" ? Url.Action(ActionNames.Payments, ControllerNames.Admin, new { status = "Pending" })! : Url.Action(ActionNames.Payments, ControllerNames.Admin)!,
                "reservations" => Url.Action(ActionNames.ReportCoworkingRevenue, ControllerNames.Admin)!,
                _ => "#"
            };
        }
        [HttpGet]
        public async Task<IActionResult> GetPendingCardPriceRequests(CancellationToken cancellationToken = default)
        {
            try
            {
                var list = await _adminService.GetPendingCardPriceRequestsAsync(cancellationToken);
                var data = list.Select(r => new
                {
                    id = r.Id,
                    coworkingName = r.CoworkingName,
                    coworkingId = r.CoworkingId,
                    addressLabel = r.AddressLabel,
                    requestedCardPrice1x = r.RequestedCardPrice1x,
                    requestedCardPriceMonthly = r.RequestedCardPriceMonthly,
                    requestedCardPrice12x = r.RequestedCardPrice12x,
                    previousCardPrice1x = r.PreviousCardPrice1x,
                    previousCardPriceMonthly = r.PreviousCardPriceMonthly,
                    previousCardPrice12x = r.PreviousCardPrice12x,
                    createdAt = r.CreatedAtFormatted
                }).ToList();
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetCardPriceRequestDetail(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var r = await _adminService.GetCardPriceRequestDetailAsync(id, cancellationToken);
                if (r == null)
                    return Json(new { success = false, message = JsonMessage.SolicitacaoNaoEncontrada });
                if (r.Status != CapacityApprovalStatus.Pending)
                    return Json(new { success = false, message = JsonMessage.EstaSolicitacaoJaFoiProcessada });
                var detail = new
                {
                    id = r.Id,
                    coworkingName = r.CoworkingName ?? DefaultDisplayStrings.EmptyPlaceholder,
                    coworkingId = r.CoworkingId,
                    addressLabel = r.AddressLabel,
                    requestedCardPrice1x = r.RequestedCardPrice1x,
                    requestedCardPriceMonthly = r.RequestedCardPriceMonthly,
                    requestedCardPrice12x = r.RequestedCardPrice12x,
                    previousCardPrice1x = r.PreviousCardPrice1x,
                    previousCardPriceMonthly = r.PreviousCardPriceMonthly,
                    previousCardPrice12x = r.PreviousCardPrice12x,
                    createdAt = r.CreatedAtFormatted
                };
                return Json(new { success = true, data = detail });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        private const int FixedPageSize = 10;
        [HttpGet]
        public async Task<IActionResult> Users(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            pageSize = FixedPageSize;
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
        public async Task<IActionResult> ViewUser(int id, CancellationToken cancellationToken = default)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }
        [HttpGet]
        public async Task<IActionResult> EditUser(int id, CancellationToken cancellationToken = default)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            var allRoles = await _userService.GetAllRolesAsync(cancellationToken);
            var userRoles = user.UserRoles.Select(ur => ur.RoleId).ToList();
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;
            return View(user);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, string fullName, string email, string? phone, string isActive, int[]? roleIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }
                var oldFullName = user.FullName;
                var oldEmail = user.Email;
                var oldPhone = user.Phone;
                var oldIsActive = user.IsActive;
                var oldRoles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
                _mapper.Map(new EditUserFormDto { FullName = fullName, Email = email, Phone = phone, IsActive = StringBooleanValue.IsTrue(isActive) }, user);
                user.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _userService.UpdateAsync(user);
                var newRoleNames = new List<string>();
                if (roleIds != null)
                {
                    await _userService.SetUserRolesAsync(id, roleIds, cancellationToken);
                    var allRolesList = await _userService.GetAllRolesAsync(cancellationToken);
                    newRoleNames = allRolesList.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToList();
                }
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.UserUpdated;
                return RedirectToAction(ActionNames.Users);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar usuário", ex.Message);
                return RedirectToAction(ActionNames.EditUser, new { id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUserEmail(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _adminService.VerifyUserEmailAsync(id);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.EmailVerified;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("verificar e-mail", ex.Message);
            }
            return RedirectToAction(ActionNames.ViewUser, new { id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(int userId, string newPassword, string confirmNewPassword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmNewPassword)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PasswordsDoNotMatchOrEmpty;
                return RedirectToAction(ActionNames.ViewUser, new { id = userId });
            }
            try
            {
                await _adminService.ResetUserPasswordAsync(userId, newPassword, confirmNewPassword);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PasswordReset;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("resetar senha", ex.Message);
            }
            return RedirectToAction(ActionNames.ViewUser, new { id = userId });
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> FixPasswordHash(string email, string newPassword = "password", CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _authService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    return Content($"<h1>Usuário não encontrado</h1><p>Email: {email}</p>", "text/html");
                }
                await _adminService.FixUserPasswordByEmailAsync(email, newPassword);
                var savedUser = await _authService.GetUserByEmailAsync(email);
                var hashValid = savedUser?.PasswordHash?.StartsWith("$2a$") ?? false;
                var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
                var loginUrl = $"{baseUrl.TrimEnd('/')}/Auth/Login";
                return Content($"<h1>Senha Resetada!</h1>" +
                    $"<p><strong>Email:</strong> {user.Email}</p>" +
                    $"<p><strong>Nova Senha:</strong> {newPassword}</p>" +
                    $"<p><strong>Hash válido:</strong> {(hashValid ? "SIM ✓" : "NÃO ✗")}</p>" +
                    $"<p><strong>Hash (primeiros 30 chars):</strong> {(savedUser?.PasswordHash != null ? savedUser.PasswordHash.Substring(0, Math.Min(30, savedUser.PasswordHash.Length)) : "N/A")}</p>" +
                    $"<hr><p><a href='{loginUrl}'>Fazer Login</a></p>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<h1>Erro</h1><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>", "text/html");
            }
        }
        [HttpGet]
        public async Task<IActionResult> Coworkings(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, string? capacityAlert = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            var coworkings = await _adminService.GetCoworkingsFilteredAsync(search, status, isPartner, approvalStatus, page, pageSize);
            var totalItems = await _adminService.GetCoworkingsFilteredCountAsync(search, status, isPartner, approvalStatus);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            Dictionary<int, int> servicesCount = new Dictionary<int, int>();
            Dictionary<int, bool> hasPendingPercentages = new Dictionary<int, bool>();
            Dictionary<int, bool> hasMaxCapacity = new Dictionary<int, bool>();
            if (coworkings != null && coworkings.Any())
            {
                var coworkingIds = coworkings.Select(c => c.Id).ToList();
                var stats = await _adminService.GetCoworkingListStatsAsync(coworkingIds, capacityAlert == StringBooleanValue.True, cancellationToken);
                servicesCount = stats.ServicesCount;
                hasPendingPercentages = stats.HasPendingPercentages;
                hasMaxCapacity = stats.HasMaxCapacity;
            }
            ViewBag.ServicesCount = servicesCount;
            ViewBag.HasPendingPercentages = hasPendingPercentages;
            ViewBag.HasMaxCapacity = hasMaxCapacity;
            ViewBag.CapacityAlert = capacityAlert == StringBooleanValue.True;
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
        public async Task<IActionResult> ViewCoworking(int id, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            var coworking = await _adminService.GetCoworkingWithDetailsForViewAsync(id, cancellationToken);
            if (coworking == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotFound;
                return RedirectToAction(ActionNames.Coworkings);
            }
            var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
            var services = await _adminService.GetServicesForAdminCoworkingViewAsync(showCoworkingMenus, cancellationToken);
            ViewBag.Services = services;
            var partnerServices = await _adminService.GetPartnerServicesForCoworkingAsync(id, showCoworkingMenus, cancellationToken);
            ViewBag.PartnerServices = partnerServices;
            ViewBag.ShowCoworkingMenus = showCoworkingMenus;
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
        public async Task<IActionResult> ApproveCoworking(int id, string? search, string? status, string? isPartner, string? approvalStatus, int? page, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _adminCoworkingService.ApproveCoworkingAsync(id, userId, cancellationToken);
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                return result.RedirectToDashboard ? RedirectToAction(ActionNames.Dashboard) : RedirectToAction(ActionNames.Coworkings);
            }
            TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            if (page.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(isPartner) || !string.IsNullOrEmpty(approvalStatus))
                return RedirectToAction(ActionNames.Coworkings, new { search, status, isPartner, approvalStatus, page });
            return RedirectToAction(ActionNames.ViewCoworking, new { id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCoworking(int id, string rejectionReason, string? search, string? status, string? isPartner, string? approvalStatus, int? page, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _adminCoworkingService.RejectCoworkingAsync(id, rejectionReason, userId, cancellationToken);
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                if (result.RedirectCoworkingId.HasValue && result.RedirectCoworkingId != id)
                    return RedirectToAction(ActionNames.Coworkings);
                return RedirectToAction(ActionNames.ViewCoworking, new { id });
            }
            TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            if (page.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(isPartner) || !string.IsNullOrEmpty(approvalStatus))
                return RedirectToAction(ActionNames.Coworkings, new { search, status, isPartner, approvalStatus, page });
            return RedirectToAction(ActionNames.ViewCoworking, new { id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveDocument(int documentId, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _adminCoworkingService.ApproveDocumentAsync(documentId, userId, cancellationToken);
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                return result.RedirectToDashboard ? RedirectToAction(ActionNames.Coworkings) : RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId });
            }
            TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            if (page.HasValue)
                return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId, page = page.Value, pageSize = pageSize ?? 4 });
            return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectDocument(int documentId, string rejectionReason, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _adminCoworkingService.RejectDocumentAsync(documentId, rejectionReason, userId, cancellationToken);
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId ?? 0, page = page ?? 1, pageSize = pageSize ?? 4 });
            }
            TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            if (page.HasValue)
                return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId, page = page.Value, pageSize = pageSize ?? 4 });
            return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCapacityChange(int requestId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.ApproveCapacityChangeAsync(requestId, userId, cancellationToken);
                return ApplyAdminResult(result);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), exceptionMessage: ErrorMessages.ErrorWithException("aprovar alteração de capacidade", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCapacityChange(int requestId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InformReprovalReason;
                return RedirectToAction(ActionNames.Dashboard);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.RejectCapacityChangeAsync(requestId, rejectionReason, userId, cancellationToken);
                return ApplyAdminResult(result);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), exceptionMessage: ErrorMessages.ErrorWithException("reprovar alteração de capacidade", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveMaxContractsChange(int requestId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.ApproveMaxContractsChangeAsync(requestId, userId, cancellationToken);
                return ApplyAdminResult(result);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), exceptionMessage: ErrorMessages.ErrorWithException("aprovar alteração de quantidade máxima", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectMaxContractsChange(int requestId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InformReprovalReason;
                return RedirectToAction(ActionNames.Dashboard);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.RejectMaxContractsChangeAsync(requestId, rejectionReason, userId, cancellationToken);
                return ApplyAdminResult(result);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), exceptionMessage: ErrorMessages.ErrorWithException("reprovar alteração de quantidade máxima", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCardPriceChange(int requestId, CancellationToken cancellationToken = default)
        {
            var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.ApproveCardPriceChangeAsync(requestId, userId, cancellationToken);
                return ApplyAdminResult(result, isAjax);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), isAjax, exceptionMessage: ErrorMessages.ErrorWithException("aprovar preços no cartão", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCardPriceChange(int requestId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                if (isAjax) return Json(new { success = false, message = JsonMessage.InformeMotivoReprovacao });
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InformReprovalReason;
                return RedirectToAction(ActionNames.Dashboard);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                var result = await _adminApprovalService.RejectCardPriceChangeAsync(requestId, rejectionReason, userId, cancellationToken);
                return ApplyAdminResult(result, isAjax);
            }
            catch (Exception ex)
            {
                return ApplyAdminResult(new AdminCommandResult(), isAjax, exceptionMessage: ErrorMessages.ErrorWithException("reprovar preços no cartão", ex.Message));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAddressStatus(int addressId, int coworkingId, string status, CancellationToken cancellationToken = default)
        {
            var address = await _adminDataService.GetAddressByIdAndCoworkingAsync(addressId, coworkingId, cancellationToken);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.ViewCoworking, new { id = coworkingId });
            }
            if (!Enum.TryParse<AddressStatus>(status, ignoreCase: true, out var newStatus))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidStatusWithValue(status);
                return RedirectToAction(ActionNames.ViewCoworking, new { id = coworkingId });
            }
            var oldStatus = address.AddressStatus;
            await _adminDataService.SetAddressStatusAsync(addressId, coworkingId, newStatus, cancellationToken);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _auditService.LogActionAsync(userId, "AddressStatusChanged", "CoworkingAddress", addressId,
                $"{{\"AddressStatus\":\"{oldStatus}\"}}", $"{{\"AddressStatus\":\"{newStatus}\"}}");
            var label = newStatus == AddressStatus.PendingApproval ? "Pendente aprovação"
                : newStatus == AddressStatus.Active ? "Ativo"
                : newStatus == AddressStatus.Suspended ? "Suspenso"
                : "Removido";
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.AddressStatusChanged(label);
            return RedirectToAction(ActionNames.ViewCoworking, new { id = coworkingId });
        }
        [HttpGet]
        public async Task<IActionResult> CoworkingHistory(int id, CancellationToken cancellationToken = default)
        {
            var coworking = await _adminDataService.GetCoworkingWithUserAsync(id, cancellationToken);
            if (coworking == null) { TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotFound; return RedirectToAction(ActionNames.Coworkings); }
            var logs = await _auditService.GetLogsByEntityAsync("Coworking", id);
            ViewBag.Coworking = coworking;
            return View(logs);
        }
        [HttpGet]
        public async Task<IActionResult> GetAuditLogDetails(int id, CancellationToken cancellationToken = default)
        {
            var log = await _adminDataService.GetAuditLogWithUserAsync(id, cancellationToken);
            if (log == null)
            {
                return Json(new { success = false, message = JsonMessage.LogNaoEncontrado });
            }
            var actionLabel = CoworkingHistoryActionLabels.GetDisplayLabel(log.Action ?? string.Empty);
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
        public async Task<IActionResult> EditCoworking(int id, CancellationToken cancellationToken = default)
        {
            var coworking = await _adminDataService.GetCoworkingForEditAsync(id, cancellationToken);
            if (coworking == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotFound;
                return RedirectToAction(ActionNames.Coworkings);
            }
            return View(coworking);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCoworking(int Id, UpdateCoworkingDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var coworking = await _adminDataService.FindCoworkingAsync(Id, cancellationToken);
                if (coworking == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotFound;
                    return RedirectToAction(ActionNames.Coworkings);
                }
                if (!string.IsNullOrEmpty(dto.CNPJ) && dto.CNPJ != coworking.CNPJ)
                {
                    var cnpjClean = dto.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjInvalidFourteenDigits;
                        return RedirectToAction(ActionNames.EditCoworking, new { id = Id });
                    }
                    var existingCNPJ = await _adminDataService.CoworkingExistsCnpjAsync(cnpjClean, Id, cancellationToken);
                    if (existingCNPJ)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjAlreadyRegisteredForCoworking(dto.CNPJ);
                        return RedirectToAction(ActionNames.EditCoworking, new { id = Id });
                    }
                    dto.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                }
                var command = new KlyonHubPlatform.Application.Commands.UpdateCoworkingCommand
                {
                    Name = dto.Name, CNPJ = dto.CNPJ, TradeName = dto.TradeName, ContactEmail = dto.ContactEmail,
                    ContactPhone = dto.ContactPhone, SecondaryEmail = dto.SecondaryEmail, SecondaryPhone = dto.SecondaryPhone,
                    BankName = dto.BankName, BankAgency = dto.BankAgency, BankAccount = dto.BankAccount, BankAccountType = dto.BankAccountType,
                    BankPixKey = dto.BankPixKey, Description = dto.Description, Website = dto.Website, Instagram = dto.Instagram,
                    Facebook = dto.Facebook, LinkedIn = dto.LinkedIn, Address = dto.Address, AddressNumber = dto.AddressNumber,
                    AddressComplement = dto.AddressComplement, AddressNeighborhood = dto.AddressNeighborhood, AddressCity = dto.AddressCity,
                    AddressState = dto.AddressState, AddressZipCode = dto.AddressZipCode, IsActive = dto.IsActive, IsPartner = dto.IsPartner,
                    ApprovalStatus = dto.ApprovalStatus, RejectionReason = dto.RejectionReason
                };
                var result = await _adminCoworkingService.UpdateCoworkingAsync(Id, command, cancellationToken);
                if (!result.Success)
                {
                    TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
                    return RedirectToAction(ActionNames.EditCoworking, new { id = Id });
                }
                TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
                return RedirectToAction(ActionNames.ViewCoworking, new { id = result.RedirectCoworkingId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar coworking", ex.Message);
                return RedirectToAction(ActionNames.EditCoworking, new { id = Id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCoworkingStatus(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var (success, errorMessage) = await _adminDataService.ToggleCoworkingStatusAsync(id, cancellationToken);
                if (!success)
                {
                    TempData[ErrorMessages.TempDataKey] = errorMessage ?? ErrorMessages.CoworkingNotFound;
                    return RedirectToAction(ActionNames.Coworkings, new
                    {
                        search = Request.Query["search"],
                        status = Request.Query[RouteValueKeys.Status],
                        isPartner = Request.Query["isPartner"],
                        approvalStatus = Request.Query["approvalStatus"],
                        page = Request.Query["page"]
                    });
                }
                var coworking = await _adminDataService.FindCoworkingAsync(id, cancellationToken);
                var statusText = coworking?.IsActive == true ? "ativado" : "inativado";
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CoworkingStatusChanged(coworking?.Name ?? "", statusText);
                return RedirectToAction(ActionNames.Coworkings, new
                {
                    search = Request.Query["search"],
                    status = Request.Query[RouteValueKeys.Status],
                    isPartner = Request.Query["isPartner"],
                    approvalStatus = Request.Query["approvalStatus"],
                    page = Request.Query["page"]
                });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("alterar status do coworking", ex.Message);
                return RedirectToAction(ActionNames.Coworkings);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Contracts(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
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
        public async Task<IActionResult> Locations(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            ActiveFilter? statusFilter = !string.IsNullOrEmpty(status) && Enum.TryParse<ActiveFilter>(status, ignoreCase: true, out var parsed) ? parsed : null;
            var locations = await _locationService.GetFilteredAsync(search, state, region, statusFilter, page, pageSize);
            var totalItems = await _locationService.GetFilteredCountAsync(search, state, region, statusFilter);
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
        public async Task<IActionResult> UpdateLocationCommission(int locationId, decimal commissionPercentage, CancellationToken cancellationToken = default)
        {
            try
            {
                await _locationService.UpdateCommissionAsync(locationId, commissionPercentage);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CommissionUpdated(commissionPercentage);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar comissão", ex.Message);
            }
            return RedirectToAction(ActionNames.Locations);
        }
        [HttpGet]
        public IActionResult CreateLocation()
        {
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(string city, string state, string? region, string? isActive, decimal commissionPercentage = 15.0m, CancellationToken cancellationToken = default)
        {
            try
            {
                var isActiveBool = StringBooleanValue.IsTrue(isActive);
                await _locationService.CreateAsync(city, state, region, isActiveBool, commissionPercentage);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.LocationCreated(city, state);
                return RedirectToAction(ActionNames.Locations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("criar localidade", ex.Message);
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
        public async Task<IActionResult> EditLocation(int id, CancellationToken cancellationToken = default)
        {
            var location = await _locationService.GetByIdAsync(id);
            if (location == null)
            {
                return NotFound();
            }
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
            return View(location);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(int id, string city, string state, string? region, string? isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                var isActiveBool = StringBooleanValue.IsTrue(isActive);
                await _locationService.UpdateAsync(id, city, state, region, isActiveBool);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.LocationUpdated(city, state);
                return RedirectToAction(ActionNames.Locations);
            }
            catch (KeyNotFoundException)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.LocationNotFound;
                return RedirectToAction(ActionNames.Locations);
            }
            catch (InvalidOperationException ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.EditLocation, new { id });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar localidade", ex.Message);
                return RedirectToAction(ActionNames.EditLocation, new { id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLocation(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var deleted = await _locationService.DeleteAsync(id);
                if (!deleted)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.LocationNotFound;
                    return RedirectToAction(ActionNames.Locations);
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.LocationDeleted;
                return RedirectToAction(ActionNames.Locations);
            }
            catch (InvalidOperationException ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.Locations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("excluir localidade", ex.Message);
                return RedirectToAction(ActionNames.Locations);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string? product, string? assistedSale, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            var start = startDate?.Date ?? DateTimeHelper.GetBrazilianTime().AddMonths(-1).Date;
            var end = endDate?.Date ?? DateTimeHelper.GetBrazilianTime().Date;
            if (end < start) end = start;
            var report = await _reportService.GetFinancialReportAsync(start, end, product, assistedSale);
            var totalItems = report.Rows.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            if (page < 1) page = 1;
            var paginatedRows = report.Rows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
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
        public async Task<IActionResult> ReportCredits(CancellationToken cancellationToken = default)
        {
            var list = await _reportService.GetClientCreditsReportAsync();
            return View(list);
        }
        [HttpGet]
        public async Task<IActionResult> ReportClientCreditStatement(int clientId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        {
            var dto = await _reportService.GetClientCreditStatementAsync(clientId, dateFrom, dateTo);
            return View(dto);
        }
        [HttpGet]
        public async Task<IActionResult> GetStatementMovementDetails(int? paymentId, int? reservationId, CancellationToken cancellationToken = default)
        {
            if (paymentId.HasValue)
            {
                var payment = await _adminDbService.GetPaymentForStatementAsync(paymentId.Value, cancellationToken);
                if (payment == null)
                    return Json(new { success = false, message = JsonMessage.PagamentoNaoEncontrado });
                string statusText = payment.Status.ToDisplayString();
                var paymentData = new
                {
                    type = "Pagamento",
                    id = payment.Id,
                    amount = payment.Amount,
                    status = statusText,
                    method = payment.Method.ToString(),
                    dueDate = payment.DueDate.ToString(DateFormatConstants.DisplayDateOnly),
                    paidAt = payment.PaidAt?.ToString(DateFormatConstants.DisplayDateAndTime),
                    createdAt = payment.CreatedAt.ToString(DateFormatConstants.DisplayDateAndTime),
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
                        reservationDate = payment.Reservation.ReservationDate.ToString(DateFormatConstants.DisplayDateOnly),
                        roomType = payment.Reservation.RoomType,
                        totalValue = payment.Reservation.TotalValue
                    } : null
                };
                return Json(new { success = true, data = paymentData });
            }
            else if (reservationId.HasValue)
            {
                var reservation = await _adminDbService.GetReservationForStatementAsync(reservationId.Value, cancellationToken);
                if (reservation == null)
                    return Json(new { success = false, message = JsonMessage.ReservaNaoEncontrada });
                var reservationData = new
                {
                    type = "Reserva",
                    id = reservation.Id,
                    totalValue = reservation.TotalValue,
                    status = reservation.Status,
                    reservationDate = reservation.ReservationDate.ToString(DateFormatConstants.DisplayDateOnly),
                    startTime = reservation.StartTime?.ToString(@"hh\:mm"),
                    endTime = reservation.EndTime?.ToString(@"hh\:mm"),
                    roomType = reservation.RoomType,
                    roomTypeDisplay = reservation.RoomType == "Shared" ? "Sala compartilhada"
                        : reservation.RoomType == "Private" ? "Sala privativa"
                        : reservation.RoomType == "Auditorium" ? "Auditório"
                        : reservation.RoomType,
                    clientConfirmedAt = reservation.ClientConfirmedAt?.ToString(DateFormatConstants.DisplayDateAndTime),
                    coworkingConfirmedAt = reservation.CoworkingConfirmedAt?.ToString(DateFormatConstants.DisplayDateAndTime),
                    coworkingName = reservation.CoworkingAddress?.Coworking?.Name,
                    coworkingAddress = reservation.CoworkingAddress != null
                        ? $"{reservation.CoworkingAddress.Address}, {reservation.CoworkingAddress.Number}"
                        : null,
                    roomName = reservation.CoworkingRoom?.Name,
                    createdAt = reservation.CreatedAt.ToString(DateFormatConstants.DisplayDateAndTime)
                };
                return Json(new { success = true, data = reservationData });
            }
            return Json(new { success = false, message = JsonMessage.IdPagamentoOuReservaNaoFornecido });
        }
        [HttpGet]
        public async Task<IActionResult> ReportCoworkingRevenue(CancellationToken cancellationToken = default)
        {
            var list = await _reportService.GetCoworkingRevenueReportAsync();
            return View(list);
        }
        [HttpGet]
        public async Task<IActionResult> ReportCoworkingRevenueDetail(int id, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        {
            var dto = await _reportService.GetCoworkingRevenueDetailAsync(id, dateFrom, dateTo);
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            return View(dto);
        }
        [HttpGet]
        public async Task<IActionResult> ReportCoworkingProductDetail(int id, string productType, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        {
            var dto = await _reportService.GetCoworkingProductDetailAsync(id, productType, dateFrom, dateTo);
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            return View(dto);
        }
        [HttpGet]
        public async Task<IActionResult> ReservationFinancialDetail(int id, CancellationToken cancellationToken = default)
        {
            var (reservation, commission, transfer, auditLogs) = await _adminDbService.GetReservationFinancialDetailAsync(id, cancellationToken);
            if (reservation == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationNotFoundAdmin;
                return RedirectToAction(ActionNames.ReportCoworkingRevenue);
            }
            ViewBag.Reservation = reservation;
            ViewBag.Commission = commission;
            ViewBag.Transfer = transfer;
            ViewBag.AuditLogs = auditLogs;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> SystemConfigurations(CancellationToken cancellationToken = default)
        {
            var coworkingMenusConfig = await _systemConfigurationService.GetByKeyAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            if (coworkingMenusConfig == null)
            {
                await _systemConfigurationService.CreateOrUpdateAsync(
                    key: FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(),
                    value: StringBooleanValue.False,
                    description: "Habilita ou desabilita os menus de coworking (Reservar Sala, Reservas, etc.) para clientes e parceiros. Valores: true ou false",
                    category: "Features",
                    isEncrypted: false,
                    isSensitive: false,
                    updatedBy: User.Identity?.Name
                );
            }
            var creditCardFeeConfig = await _systemConfigurationService.GetByKeyAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (creditCardFeeConfig == null)
            {
                await _systemConfigurationService.CreateOrUpdateAsync(
                    key: PaymentConfigKey.CreditCardFeePercentage.ToConfigKey(),
                    value: "0",
                    description: "Percentual adicional a ser cobrado quando o pagamento for realizado com cartão de crédito. Exemplo: 2.5 para 2,5%",
                    category: "Payment",
                    isEncrypted: false,
                    isSensitive: false,
                    updatedBy: User.Identity?.Name
                );
            }
            var oracleNamespaceKey = OracleStorageConfigKey.Namespace.ToConfigKey();
            if (await _systemConfigurationService.GetByKeyAsync(oracleNamespaceKey) == null)
            {
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.Namespace.ToConfigKey(), "", OracleStorageConfigKey.Namespace.GetDescription(), "Storage", false, false, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.Region.ToConfigKey(), OracleStorageDefaults.Region, OracleStorageConfigKey.Region.GetDescription(), "Storage", false, false, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.BucketName.ToConfigKey(), "", OracleStorageConfigKey.BucketName.GetDescription(), "Storage", false, false, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.AccessKeyId.ToConfigKey(), "", OracleStorageConfigKey.AccessKeyId.GetDescription(), "Storage", false, true, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.SecretAccessKey.ToConfigKey(), "", OracleStorageConfigKey.SecretAccessKey.GetDescription(), "Storage", false, true, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.ContractsFolder.ToConfigKey(), OracleStorageDefaults.ContractsFolder, OracleStorageConfigKey.ContractsFolder.GetDescription(), "Storage", false, false, User.Identity?.Name);
                await _systemConfigurationService.CreateOrUpdateAsync(OracleStorageConfigKey.PhotosFolder.ToConfigKey(), OracleStorageDefaults.PhotosFolder, OracleStorageConfigKey.PhotosFolder.GetDescription(), "Storage", false, false, User.Identity?.Name);
            }
            else
            {
                var oracleAccessKey = await _systemConfigurationService.GetByKeyAsync(OracleStorageConfigKey.AccessKeyId.ToConfigKey());
                var oracleSecretKey = await _systemConfigurationService.GetByKeyAsync(OracleStorageConfigKey.SecretAccessKey.ToConfigKey());
                if (oracleAccessKey != null && !oracleAccessKey.IsSensitive)
                {
                    oracleAccessKey.IsSensitive = true;
                    await _systemConfigurationService.UpdateAsync(oracleAccessKey, User.Identity?.Name);
                }
                if (oracleSecretKey != null && !oracleSecretKey.IsSensitive)
                {
                    oracleSecretKey.IsSensitive = true;
                    await _systemConfigurationService.UpdateAsync(oracleSecretKey, User.Identity?.Name);
                }
            }
            var configurations = await _systemConfigurationService.GetAllAsync();
            var groupedByCategory = configurations.GroupBy(c => c.Category).ToList();
            var currentProvider = await _systemConfigurationService.GetValueAsync(Provider) ?? "AWS";
            var d4Token = await _systemConfigurationService.GetValueAsync(TokenAPI);
            var d4Safe = await _systemConfigurationService.GetValueAsync(SafeUuid);
            ViewBag.D4SignConfigured = !string.IsNullOrWhiteSpace(d4Token) && !string.IsNullOrWhiteSpace(d4Safe);
            ViewBag.GroupedConfigurations = groupedByCategory;
            ViewBag.Categories = groupedByCategory.Select(g => g.Key).ToList();
            ViewBag.CurrentStorageProvider = currentProvider;
            return View(configurations);
        }
        [HttpGet]
        public async Task<IActionResult> EditSystemConfiguration(int id, CancellationToken cancellationToken = default)
        {
            var configuration = await _systemConfigurationService.GetByIdAsync(id);
            if (configuration == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ConfigNotFound;
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            return View(configuration);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSystemConfiguration(int id, string value, string? description, CancellationToken cancellationToken = default)
        {
            try
            {
                var configuration = await _systemConfigurationService.GetByIdAsync(id);
                if (configuration == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ConfigNotFound;
                    return RedirectToAction(ActionNames.SystemConfigurations);
                }
                configuration.Value = value;
                configuration.Description = description ?? configuration.Description;
                configuration.UpdatedBy = User.Identity?.Name;
                await _systemConfigurationService.UpdateAsync(configuration, User.Identity?.Name);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ConfigurationUpdated(configuration.Key);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar configuração", ex.Message);
                return RedirectToAction(ActionNames.EditSystemConfiguration, new { id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStorageToFTP(CancellationToken cancellationToken = default)
        {
            try
            {
                await _adminDbService.UpdateStorageToFtpAsync(cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.FtpSettingsUpdated;
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar configurações", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStorageToOracle(CancellationToken cancellationToken = default)
        {
            try
            {
                await _adminDbService.UpdateStorageToOracleAsync(cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.OracleSettingsUpdated;
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar configurações", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStorageToAws(CancellationToken cancellationToken = default)
        {
            try
            {
                await _adminDbService.UpdateStorageToAwsAsync(cancellationToken);
                TempData[SuccessMessage.TempDataKey] = "Provedor de armazenamento alterado para AWS S3.";
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar configurações", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAddressDeclarationsTable(CancellationToken cancellationToken = default)
        {
            try
            {
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
                    tableExists = false;
                }
                if (!tableExists)
                {
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
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.TableAddressDeclarationsCreated;
                }
                else
                {
                    TempData[TempDataKeys.Info] = InfoMessages.TableAddressDeclarationsAlreadyExists;
                }
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData[ErrorMessages.TempDataKey] = errorMessage;
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigureAsaasSandbox(CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKeyKey = AsaasConfigKey.ApiKey.ToConfigKey();
                var useSandboxKey = AsaasConfigKey.UseSandbox.ToConfigKey();
                var configs = new[]
                {
                    (apiKeyKey, "", "API Key do Asaas (obtida no painel Asaas > Integrações)", true),
                    (useSandboxKey, StringBooleanValue.True, "Usar ambiente sandbox do Asaas (true/false)", false)
                };
                await _adminDbService.UpdateOrAddSystemConfigurationsAsync(configs, "Payment", User.Identity?.Name, cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.AsaasSandboxPrepared(apiKeyKey);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("configurar Asaas", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestAsaasConnection(CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = await _systemConfigurationService.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
                var useSandbox = await _systemConfigurationService.GetValueAsync(AsaasConfigKey.UseSandbox.ToConfigKey());
                if (string.IsNullOrEmpty(apiKey))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ApiKeyAsaasNotConfigured(AsaasConfigKey.ApiKey.ToConfigKey());
                    return RedirectToAction(ActionNames.SystemConfigurations);
                }
                var isSandbox = StringBooleanValue.IsTrue(useSandbox);
                var environment = isSandbox ? "Sandbox (Testes)" : "Produção";
                var (success, statusCode, message) = await _asaasService.TestCreatePaymentAsync();
                if (success)
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.AsaasConnectionValidated(environment, statusCode);
                }
                else
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorValidatingAsaasConnection(message);
                }
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("API Key") || ex.Message.Contains("não configurada"))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ApiKeyNotConfiguredConfigureFirst(ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (errorMessage.Contains("Unauthorized") || errorMessage.Contains("401"))
                {
                    errorMessage = "API Key inválida ou expirada. Verifique no painel Asaas (sandbox.asaas.com ou asaas.com).";
                }
                else if (errorMessage.Contains("Forbidden") || errorMessage.Contains("403"))
                {
                    errorMessage = "API Key sem permissões suficientes. Verifique as permissões no painel do Asaas.";
                }
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorValidatingAsaasConnection(errorMessage);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestAsaasCreatePayment(CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = await _systemConfigurationService.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
                if (string.IsNullOrEmpty(apiKey))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ApiKeyAsaasNotConfiguredConfigureIn(AsaasConfigKey.ApiKey.ToConfigKey());
                    return RedirectToAction(ActionNames.SystemConfigurations);
                }
                var (success, statusCode, message) = await _asaasService.TestCreatePaymentAsync();
                if (success)
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.AsaasReturnOk(statusCode, message);
                }
                else
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.AsaasReturnStatus(statusCode.ToString(), message);
                }
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("testar criação de cobrança", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestAsaasCreateCreditCardPayment(CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = await _systemConfigurationService.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
                if (string.IsNullOrEmpty(apiKey))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ApiKeyAsaasNotConfiguredConfigureIn(AsaasConfigKey.ApiKey.ToConfigKey());
                    return RedirectToAction(ActionNames.SystemConfigurations);
                }
                var (success, statusCode, message) = await _asaasService.TestCreateCreditCardPaymentAsync();
                if (success)
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.AsaasReturnOk(statusCode, message);
                }
                else
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.AsaasReturnStatus(statusCode.ToString(), message);
                }
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("testar cobrança com cartão", ex.Message);
                return RedirectToAction(ActionNames.SystemConfigurations);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Payments(string? search = null, string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            var (payments, totalItems) = await _adminDbService.GetPaymentsPagedAsync(search, status, page, pageSize, cancellationToken);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
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
        public async Task<IActionResult> ApprovePayment(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _adminDbService.GetPaymentForApproveAsync(paymentId, cancellationToken);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.Payments);
                }
                await _paymentService.ConfirmPaymentAsync(paymentId, Guid.NewGuid().ToString());
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentApproved;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("aprovar pagamento", ex.Message);
            }
            return RedirectToAction(ActionNames.Payments);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.RejectionReasonRequired;
                return RedirectToAction(ActionNames.Payments);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            try
            {
                await _adminDbService.RejectPaymentAsync(paymentId, userId, rejectionReason, cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentRejected;
            }
            catch (InvalidOperationException ex) when (ex.Message == "Pagamento não encontrado.")
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("reprovar pagamento", ex.Message);
            }
            return RedirectToAction(ActionNames.Payments);
        }
        [HttpGet]
        public async Task<IActionResult> GetPaymentHistory(int paymentId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            var (history, totalItems) = await _adminDbService.GetPaymentHistoryPagedAsync(paymentId, page, pageSize, cancellationToken);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            return Json(new
            {
                items = _mapper.Map<List<PaymentHistoryItemDto>>(history),
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
        public async Task<IActionResult> ViewReceipt(int paymentId, CancellationToken cancellationToken = default)
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
            if (fileExtension != ".pdf")
            {
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                return File(fileBytes, contentType);
            }
            else
            {
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                return File(fileBytes, contentType);
            }
        }
        [HttpGet]
        public async Task<IActionResult> DatabaseManager(CancellationToken cancellationToken = default)
        {
            try
            {
                var pending = await _adminDbService.GetPendingMigrationsAsync(cancellationToken);
                ViewBag.PendingMigrations = pending.ToList();
                ViewBag.AppliedMigrations = await _adminDbService.GetAppliedMigrationsAsync(cancellationToken);
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
        public async Task<IActionResult> RunMigrations(CancellationToken cancellationToken = default)
        {
            try
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT 1 FROM CreditPlans LIMIT 1");
                    var pendingCheck = await _context.Database.GetPendingMigrationsAsync();
                    if (pendingCheck.Contains("20260124000542_AddCoworkingSefazConfiguration"))
                    {
                        await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')");
                    }
                }
                catch
                {
                }
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                var list = pendingMigrations.ToList();
                if (list.Count == 0)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.NoPendingMigrations;
                    return RedirectToAction(ActionNames.DatabaseManager);
                }
                await _context.Database.MigrateAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.MigrationsExecuted(string.Join(", ", list));
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                if (msg.Contains("already exists") || msg.Contains("Table") && msg.Contains("exists"))
                {
                    var pending = await _context.Database.GetPendingMigrationsAsync();
                    var pendingList = pending.ToList();
                    if (msg.Contains("creditplans", StringComparison.OrdinalIgnoreCase) || msg.Contains("CreditPlans"))
                    {
                        try
                        {
                            await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')");
                            TempData[TempDataKeys.Info] = InfoMessages.MigrationMarkedAsApplied;
                            return RedirectToAction(ActionNames.DatabaseManager);
                        }
                        catch
                        {
                        }
                    }
                }
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorExecutingMigrations(msg);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCoworkingOperatingHours(CancellationToken cancellationToken = default)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();
                var sb = new System.Text.StringBuilder();
                try
                {
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
                            TempData[TempDataKeys.VerifyOperatingHoursResult] = sb.ToString();
                            return RedirectToAction(ActionNames.DatabaseManager);
                        }
                    }
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM CoworkingOperatingHours";
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        sb.AppendLine($"Total de registros: {count}");
                        sb.AppendLine();
                    }
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
                TempData[TempDataKeys.VerifyOperatingHoursResult] = sb.ToString();
            }
            catch (Exception ex)
            {
                TempData[TempDataKeys.VerifyOperatingHoursResult] = $"[ERRO] {ex.Message}";
                if (ex.InnerException != null)
                    TempData[TempDataKeys.VerifyOperatingHoursResult] += "\r\n" + ex.InnerException.Message;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingOperatingHoursTable(CancellationToken cancellationToken = default)
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
                    TempData[TempDataKeys.Info] = InfoMessages.TableCoworkingOperatingHoursAlreadyExists;
                    return RedirectToAction(ActionNames.DatabaseManager);
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TableCoworkingOperatingHoursCreated;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorCreatingCoworkingOperatingHours(msg);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddD4SignDocumentUuidColumn(CancellationToken cancellationToken = default)
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
                    TempData[TempDataKeys.Info] = InfoMessages.ColumnD4SignDocumentUuidAlreadyExists;
                    return RedirectToAction(ActionNames.DatabaseManager);
                }
                const string alterSql = @"ALTER TABLE Documents ADD COLUMN D4SignDocumentUuid VARCHAR(64) NULL CHARACTER SET utf8mb4;";
                await _context.Database.ExecuteSqlRawAsync(alterSql);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ColumnD4SignDocumentUuidCreated;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorAddingColumnD4SignDocumentUuid(msg);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearTestData(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
                var tablesToClear = new[]
                {
                    "PaymentHistories",
                    "ReservationCommissions",
                    "Commissions",
                    "Transfers",
                    "Payments",
                    "Documents",
                    "AddressDeclarations",
                    "Subscriptions",
                    "Contracts"
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
                            using var checkCmd = connection.CreateCommand();
                            checkCmd.CommandText = $@"
                                SELECT COUNT(*) 
                                FROM information_schema.tables 
                                WHERE table_schema = DATABASE() 
                                AND table_name = '{tableName}'";
                            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                            if (exists)
                            {
                                using var countCmd = connection.CreateCommand();
                                countCmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}`;";
                                var countBefore = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
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
                    await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                    if (!wasOpen) await connection.CloseAsync();
                }
                if (errors.Any())
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorsOccurredCleaningData(string.Join("; ", errors));
                }
                if (clearedTables.Any())
                {
                    var message = $"Dados de teste limpos com sucesso!<br><br><strong>Tabelas limpas:</strong><ul>";
                    foreach (var table in clearedTables)
                    {
                        message += $"<li>{table}</li>";
                    }
                    message += "</ul>";
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.Generic(message);
                }
                else
                {
                    TempData[TempDataKeys.Info] = InfoMessages.NoTableCleaned;
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorCleaningTestData(msg);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMustChangePasswordColumn(CancellationToken cancellationToken = default)
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
                    TempData[TempDataKeys.Info] = InfoMessages.ColumnMustChangePasswordAlreadyExists;
                    return RedirectToAction(ActionNames.DatabaseManager);
                }
                const string alterSql = @"ALTER TABLE Users ADD COLUMN MustChangePassword tinyint(1) NOT NULL DEFAULT 0;";
                await _context.Database.ExecuteSqlRawAsync(alterSql);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ColumnMustChangePasswordCreated;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorAddingColumnMustChangePassword(msg);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyIndexes(CancellationToken cancellationToken = default)
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
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorCreatingIndex(name, ex.Message);
                    return RedirectToAction(ActionNames.DatabaseManager);
                }
            }
            if (created.Count > 0)
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.IndexesCreated(created.Count, string.Join(", ", created));
            else
                TempData[TempDataKeys.Info] = InfoMessages.AllIndexesAlreadyExist;
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixReservationAndTransferColumns(CancellationToken cancellationToken = default)
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
            try { await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IX_Transfers_ReservationId ON Transfers (ReservationId)"); added.Add("IX_Transfers_ReservationId"); } catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            try { await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Transfers ADD CONSTRAINT FK_Transfers_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL"); added.Add("FK_Transfers_Reservations_ReservationId"); } catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            if (added.Count > 0)
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ColumnsIndexFkAdded(string.Join(", ", added));
            else
                TempData[TempDataKeys.Info] = InfoMessages.ColumnsAlreadyExist;
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixCreditPurchaseColumns(CancellationToken cancellationToken = default)
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CreditPurchasesColumnsAdded(string.Join(", ", added));
            else
                TempData[TempDataKeys.Info] = InfoMessages.CreditPurchasesColumnsAlreadyExist;
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInterestsTable(CancellationToken cancellationToken = default)
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
                        AND table_name = 'Interests'";
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    tableExists = false;
                }
                if (tableExists)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.TableInterestsAlreadyExists;
                    await connection.CloseAsync();
                    return RedirectToAction(ActionNames.DatabaseManager);
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TableInterestsCreated;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela Interests: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData[ErrorMessages.TempDataKey] = errorMessage;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingProductsTable(CancellationToken cancellationToken = default)
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
                        AND table_name = 'CoworkingProducts'";
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    tableExists = false;
                }
                if (tableExists)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.TableCoworkingProductsAlreadyExists;
                    await connection.CloseAsync();
                    return RedirectToAction(ActionNames.DatabaseManager);
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TableCoworkingProductsCreated;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingProducts: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData[ErrorMessages.TempDataKey] = errorMessage;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearDatabase(CancellationToken cancellationToken = default)
        {
            try
            {
                HttpContext.Items["SkipAudit"] = true;
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
                var tablesToClear = new[]
                {
                    "AuditLogs",
                    "PaymentHistories",
                    "FiscalNoteScriptExecutions",
                    "FiscalNotes",
                    "ReservationCommissions",
                    "Commissions",
                    "Transfers",
                    "Reservations",
                    "Payments",
                    "Subscriptions",
                    "Contracts",
                    "AddressDeclarations",
                    "CapacityChangeRequests",
                    "CoworkingRoomPhotos",
                    "CoworkingRooms",
                    "CoworkingPhotos",
                    "CoworkingDocuments",
                    "CoworkingOperatingHours",
                    "BlockedDates",
                    "CoworkingPartners",
                    "CoworkingSefazConfigurations",
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
                    "Translations",
                    "Services",
                };
                var clearedTables = new List<string>();
                var errors = new List<string>();
                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@klyon.com");
                var adminUserRolePairs = new List<(int UserId, int RoleId)>();
                if (adminUser != null)
                {
                    var adminRoles = await _context.UserRoles
                        .Where(ur => ur.UserId == adminUser.Id)
                        .ToListAsync();
                    adminUserRolePairs = adminRoles.Select(ur => (ur.UserId, ur.RoleId)).ToList();
                }
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                foreach (var tableName in tablesToClear)
                {
                    try
                    {
                        using var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = DATABASE() 
                            AND table_name = '{tableName}'";
                        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                        if (exists)
                        {
                            using var deleteCmd = connection.CreateCommand();
                            deleteCmd.CommandText = $"DELETE FROM `{tableName}`;";
                            var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync();
                            try
                            {
                                using var resetCmd = connection.CreateCommand();
                                resetCmd.CommandText = $"ALTER TABLE `{tableName}` AUTO_INCREMENT = 1;";
                                await resetCmd.ExecuteNonQueryAsync();
                            }
                            catch (Exception resetEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Aviso: Não foi possível resetar AUTO_INCREMENT da tabela {tableName}: {resetEx.Message}");
                            }
                            clearedTables.Add($"{tableName} ({rowsDeleted} registros)");
                        }
                    }
                    catch (Exception)
                    {
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
                if (adminUser != null && adminUserRolePairs.Any())
                {
                    var roleConditions = string.Join(" OR ", adminUserRolePairs.Select(pair => $"(UserId = {pair.UserId} AND RoleId = {pair.RoleId})"));
                    try
                    {
#pragma warning disable EF1002
                        await _context.Database.ExecuteSqlRawAsync(
                            $"DELETE FROM UserRoles WHERE NOT ({roleConditions});");
#pragma warning restore EF1002
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"UserRoles: {ex.Message}");
                    }
                    try
                    {
#pragma warning disable EF1002
                        await _context.Database.ExecuteSqlRawAsync(
                            $"DELETE FROM Users WHERE Id != {adminUser.Id};");
#pragma warning restore EF1002
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Users: {ex.Message}");
                    }
                }
                else
                {
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
                var tablesToResetAutoIncrement = new[] { "AuditLogs", "Users", "Roles", "Clients", "Coworkings", "Contracts", "Payments", "Subscriptions", "Locations", "Services", "SystemConfigurations", "Languages", "LocationCommissions", "UserRoles" };
                var wasConnectionOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasConnectionOpen) await connection.OpenAsync();
                try
                {
                    foreach (var tableName in tablesToResetAutoIncrement)
                    {
                        try
                        {
                            using var checkCmd = connection.CreateCommand();
                            var escapedTableName = tableName.Replace("`", "``");
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
                            System.Diagnostics.Debug.WriteLine($"Aviso: Não foi possível resetar AUTO_INCREMENT da tabela {tableName}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    if (!wasConnectionOpen) await connection.CloseAsync();
                }
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                var seedService = new Application.Services.SeedService(_context);
                await seedService.SeedInitialDataAsync();
                var successMessage = $"Base de dados limpa com sucesso! {clearedTables.Count} tabela(s) foram limpas. " +
                    $"Dados essenciais preservados: Users (admin), UserRoles (admin), SefazConfigurations, __EFMigrationsHistory. " +
                    $"Tabelas com seed recriadas automaticamente: Roles, Languages, Locations, Services, SystemConfigurations.";
                if (errors.Any())
                {
                    successMessage += $" Avisos: {string.Join("; ", errors.Take(3))}";
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.Generic(successMessage);
            }
            catch (Exception ex)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                }
                catch { }
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorClearingDatabase(ex.Message);
                if (ex.InnerException != null)
                {
                    TempData[ErrorMessages.TempDataKey] += ErrorMessages.DetailInnerException(ex.InnerException.Message);
                }
            }
            finally
            {
                HttpContext.Items.Remove("SkipAudit");
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteSql(string sqlQuery, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.SqlQueryCannotBeEmpty;
                return RedirectToAction(ActionNames.DatabaseManager);
            }
            var upperQuery = sqlQuery.ToUpper().Trim();
            var blockedKeywords = new[] { "DROP DATABASE", "DROP TABLE", "TRUNCATE", "DELETE FROM", "UPDATE", "ALTER DATABASE" };
            if (blockedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.DangerousOperationsNotAllowed;
                return RedirectToAction(ActionNames.DatabaseManager);
            }
            var allowedKeywords = new[] { "ALTER TABLE", "CREATE TABLE", "CREATE INDEX", "ADD COLUMN", "ADD", "SELECT", "SHOW", "DESCRIBE", "DESC" };
            if (!allowedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyAllowedSqlOperations;
                return RedirectToAction(ActionNames.DatabaseManager);
            }
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sqlQuery;
                command.CommandTimeout = 300;
                var result = new System.Text.StringBuilder();
                var rowsAffected = 0;
                if (upperQuery.TrimStart().StartsWith("SELECT") ||
                    upperQuery.TrimStart().StartsWith("SHOW") ||
                    upperQuery.TrimStart().StartsWith("DESCRIBE") ||
                    upperQuery.TrimStart().StartsWith("DESC"))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    if (reader.HasRows)
                    {
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }
                        result.AppendLine(string.Join(" | ", columnNames));
                        result.AppendLine(new string('-', Math.Min(200, columnNames.Sum(c => c.Length) + (columnNames.Count * 3))));
                        int rowCount = 0;
                        while (await reader.ReadAsync() && rowCount < 1000)
                        {
                            var row = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL";
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
                    rowsAffected = await command.ExecuteNonQueryAsync();
                    result.AppendLine($"Comando executado com sucesso!");
                    result.AppendLine($"Linhas afetadas: {rowsAffected}");
                }
                await connection.CloseAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.SqlQueryExecuted;
                TempData[TempDataKeys.QueryResult] = result.ToString();
                TempData[TempDataKeys.ExecutedQuery] = sqlQuery;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("executar query", ex.Message);
                TempData[TempDataKeys.ExecutedQuery] = sqlQuery;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingRatingsTable(CancellationToken cancellationToken = default)
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
                    TempData[TempDataKeys.Info] = InfoMessages.TableCoworkingRatingsAlreadyExists;
                    await connection.CloseAsync();
                    return RedirectToAction(ActionNames.DatabaseManager);
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TableCoworkingRatingsCreated;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingRatings: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData[ErrorMessages.TempDataKey] = errorMessage;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCreditsTables(CancellationToken cancellationToken = default)
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CreditTablesCreated;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabelas de créditos: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData[ErrorMessages.TempDataKey] = errorMessage;
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeCreditPlans()
        {
            try
            {
                var existingPlans = await _context.CreditPlans.AnyAsync();
                if (existingPlans)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.CreditPlansAlreadyInitialized;
                    return RedirectToAction(ActionNames.DatabaseManager);
                }
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CreditPlansInitialized;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("inicializar planos", ex.Message);
            }
            return RedirectToAction(ActionNames.DatabaseManager);
        }
        [HttpPost]
        public async Task<IActionResult> AddRoomConfigurationColumns(CancellationToken cancellationToken = default)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
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
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }
                await connection.CloseAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.RoomConfigColumnsAdded;
                return RedirectToAction(ActionNames.DatabaseManager);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("adicionar colunas", ex.Message);
                return RedirectToAction(ActionNames.DatabaseManager);
            }
        }
        [HttpPost]
        public async Task<IActionResult> AddReservationColumns(CancellationToken cancellationToken = default)
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
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }
                await connection.CloseAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.RoomTypeTotalValueColumnsAdded;
                return RedirectToAction(ActionNames.DatabaseManager);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("adicionar colunas", ex.Message);
                return RedirectToAction(ActionNames.DatabaseManager);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePartnerService(int Id, int CoworkingId, int ServiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var percentageStr = (Request.Form["Percentage"].ToString() ?? "").Trim().Replace(",", ".");
                if (!decimal.TryParse(percentageStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var percentage))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidPercentageFormat;
                    return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                }
                var coworking = await _context.Coworkings.FindAsync(CoworkingId);
                if (coworking == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotFound;
                    return RedirectToAction(ActionNames.Coworkings);
                }
                var service = await _context.Services.FindAsync(ServiceId);
                if (service == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ServiceNotFound;
                    return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                }
                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ServiceNotAvailableWhenMenusDisabled;
                    return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                }
                if (percentage < 0 || percentage > 100)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PercentageMustBeBetween0And100;
                    return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                }
                if (Id > 0)
                {
                    var existing = await _context.PartnerServices.FindAsync(Id);
                    if (existing == null || existing.CoworkingId != CoworkingId)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.PartnerServiceNotFound;
                        return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                    }
                    existing.ServiceId = ServiceId;
                    existing.Percentage = percentage;
                    existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                }
                else
                {
                    var existing = await _context.PartnerServices
                        .FirstOrDefaultAsync(ps => ps.CoworkingId == CoworkingId && ps.ServiceId == ServiceId);
                    if (existing != null)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.ServiceAlreadyRegisteredForPartner;
                        return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
                    }
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceSaved;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("salvar serviço", ex.Message);
            }
            return RedirectToAction(ActionNames.ViewCoworking, new { id = CoworkingId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartnerService(int id, int coworkingId, CancellationToken cancellationToken = default)
        {
            try
            {
                var partnerService = await _context.PartnerServices.FindAsync(id);
                if (partnerService == null || partnerService.CoworkingId != coworkingId)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PartnerServiceNotFound;
                    return RedirectToAction(ActionNames.ViewCoworking, new { id = coworkingId });
                }
                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceRemoved;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("remover serviço", ex.Message);
            }
            return RedirectToAction(ActionNames.ViewCoworking, new { id = coworkingId });
        }
        [HttpGet]
        public async Task<IActionResult> ServiceRemovalRequests(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
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
        public async Task<IActionResult> ApproveServiceRemoval(int partnerServiceId, CancellationToken cancellationToken = default)
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
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.RequestNotFound;
                    return RedirectToAction(ActionNames.ServiceRemovalRequests);
                }
                if (partnerService.RemovalRequestStatus != RemovalRequestStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.RequestAlreadyProcessed;
                    return RedirectToAction(ActionNames.ServiceRemovalRequests);
                }
                var serviceName = partnerService.Service.Name;
                var coworkingName = partnerService.Coworking.Name;
                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();
                await _auditService.LogActionAsync(userId, "ServiceRemovalApproved", "PartnerService", partnerServiceId,
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}",
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Approved\",\"Removed\":true}}");
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceRemovalApproved(serviceName, coworkingName);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("aprovar remoção", ex.Message);
            }
            return RedirectToAction(ActionNames.ServiceRemovalRequests);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectServiceRemoval(int partnerServiceId, string rejectionReason, CancellationToken cancellationToken = default)
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
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.RequestNotFound;
                    return RedirectToAction(ActionNames.ServiceRemovalRequests);
                }
                if (partnerService.RemovalRequestStatus != RemovalRequestStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.RequestAlreadyProcessed;
                    return RedirectToAction(ActionNames.ServiceRemovalRequests);
                }
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.RejectionReasonRequiredForRemoval;
                    return RedirectToAction(ActionNames.ServiceRemovalRequests);
                }
                var serviceName = partnerService.Service.Name;
                var coworkingName = partnerService.Coworking.Name;
                partnerService.RemovalRequestStatus = RemovalRequestStatus.Rejected;
                partnerService.RemovalReviewedAt = DateTimeHelper.GetBrazilianTime();
                partnerService.RemovalReviewedBy = userId;
                partnerService.RemovalRejectionReason = rejectionReason.Trim();
                partnerService.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
                await _auditService.LogActionAsync(userId, "ServiceRemovalRejected", "PartnerService", partnerServiceId,
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}",
                    $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Rejected\",\"Reason\":\"{rejectionReason}\"}}");
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceRemovalRejected(serviceName, coworkingName);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("rejeitar remoção", ex.Message);
            }
            return RedirectToAction(ActionNames.ServiceRemovalRequests);
        }
        [HttpGet]
        public async Task<IActionResult> SefazConfiguration(string? state = null, CancellationToken cancellationToken = default)
        {
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };
            ViewBag.SupportedStates = new List<string> { "ES" };
            var selectedState = state ?? "ES";
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
        public async Task<IActionResult> SaveSefazConfiguration(SefazConfiguration config, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(config.State))
                {
                    config.State = config.State.Trim().ToUpper();
                }
                else
                {
                    config.State = "ES";
                }
                if (config.Id > 0)
                {
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
                    config.CreatedAt = DateTimeHelper.GetBrazilianTime();
                    _context.SefazConfigurations.Add(config);
                }
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.SefazEsConfigSaved;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("salvar configuração", ex.Message);
            }
            return RedirectToAction(ActionNames.SefazConfiguration);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteFiscalNoteScript(string noteType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
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
                        var reservations = await _context.Reservations
                            .Include(r => r.Client)
                            .Include(r => r.CoworkingAddress)
                                .ThenInclude(a => a.Coworking)
                            .Where(r => r.Status == ReservationStatus.Completed &&
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
                                    Status = FiscalNoteStatus.Pendente,
                                    CreatedAt = executedAt
                                };
                                _context.FiscalNotes.Add(fiscalNote);
                                notesCreated++;
                            }
                            catch (DbUpdateException ex) when (IsDuplicateIdempotencyKeyException(ex))
                            {
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
                                    Status = FiscalNoteStatus.Pendente,
                                    CreatedAt = executedAt
                                };
                                _context.FiscalNotes.Add(fiscalNote);
                                notesCreated++;
                            }
                            catch (DbUpdateException ex) when (IsDuplicateIdempotencyKeyException(ex))
                            {
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
                        TempData[TempDataKeys.Info] = InfoMessages.ServiceInvoicesEmittedByPartnerAtSefaz;
                        await transaction.RollbackAsync();
                        return RedirectToAction(ActionNames.SefazConfiguration);
                    }
                    await _context.SaveChangesAsync();
                    var pendingNotes = await _context.FiscalNotes
                        .Include(fn => fn.Coworking)
                            .ThenInclude(c => c.Addresses)
                        .Where(fn => fn.Status == FiscalNoteStatus.Pendente && fn.Type == "Intermediacao")
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
                            note.Status = FiscalNoteStatus.Erro;
                            note.ErrorMessage = ex.Message;
                            note.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                            _context.FiscalNotes.Update(note);
                        }
                        catch (Exception ex)
                        {
                            emissionErrors++;
                            errorMessages.Add($"NF #{note.Id}: {ex.Message}");
                            note.Status = FiscalNoteStatus.Erro;
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
                            TempData[TempDataKeys.UnsupportedStates] = string.Join("<br>", unsupportedStateMessages.Take(10));
                    }
                    if (errors + emissionErrors > 0)
                    {
                        message += $" {errors + emissionErrors} erro(s) encontrado(s).";
                        if (errorMessages.Count > 0)
                            TempData[TempDataKeys.ErrorDetails] = string.Join("<br>", errorMessages.Take(10));
                    }
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.Generic(message);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("executar script", ex.Message);
            }
            return RedirectToAction(ActionNames.SefazConfiguration);
        }
        [HttpGet]
        public async Task<IActionResult> FiscalNotes(int page = 1, int pageSize = 10, string? status = null, string? type = null, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
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
            if (string.IsNullOrEmpty(status))
            {
                query = query.Where(fn => fn.Status != FiscalNoteStatus.Rejeitada && fn.Status != FiscalNoteStatus.Importada);
            }
            else if (Enum.TryParse<FiscalNoteStatus>(status, true, out var fiscalStatus))
            {
                query = query.Where(fn => fn.Status == fiscalStatus);
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
        [HttpGet]
        public async Task<IActionResult> ImportedFiscalNotes(int page = 1, int pageSize = 10, int? coworkingId = null, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            try
            {
                var query = _context.FiscalNotes
                    .Include(fn => fn.Payment)
                    .Include(fn => fn.Client).ThenInclude(c => c.User)
                    .Include(fn => fn.Coworking)
                    .Where(fn => fn.Type == "Servico" && fn.Status == FiscalNoteStatus.Importada)
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
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("carregar notas fiscais importadas", ex.Message);
                ViewBag.Coworkings = await _context.Coworkings.OrderBy(c => c.Name).ToListAsync();
                return View(new List<KlyonHubPlatform.Domain.Entities.FiscalNote>());
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveImportedFiscalNote(int fiscalNoteId, CancellationToken cancellationToken = default)
        {
            var fn = await _context.FiscalNotes.FindAsync(fiscalNoteId);
            if (fn == null || fn.Type != "Servico" || fn.Status != FiscalNoteStatus.Importada)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ImportedInvoiceNotFoundOrProcessed;
                return RedirectToAction(ActionNames.ImportedFiscalNotes);
            }
            fn.Status = FiscalNoteStatus.Emitida;
            fn.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.ImportedInvoiceApproved;
            return RedirectToAction(ActionNames.ImportedFiscalNotes);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectImportedFiscalNote(int fiscalNoteId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InformRejectionReason;
                return RedirectToAction(ActionNames.ImportedFiscalNotes);
            }
            var fn = await _context.FiscalNotes.FindAsync(fiscalNoteId);
            if (fn == null || fn.Type != "Servico" || fn.Status != FiscalNoteStatus.Importada)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ImportedInvoiceNotFoundOrProcessed;
                return RedirectToAction(ActionNames.ImportedFiscalNotes);
            }
            fn.Status = FiscalNoteStatus.Rejeitada;
            fn.ErrorMessage = rejectionReason;
            fn.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.ImportedInvoiceRejected;
            return RedirectToAction(ActionNames.ImportedFiscalNotes);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadFiscalNotePdf(int fiscalNoteId, CancellationToken cancellationToken = default)
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
        public async Task<IActionResult> GetFiscalNoteDetails(int fiscalNoteId, CancellationToken cancellationToken = default)
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
                return Json(new { success = false, message = JsonMessage.NotaFiscalNaoEncontrada });
            }
            var commissions = new List<CommissionListItemDto>();
            var paymentCommissions = new List<Commission>();
            if (fn.PaymentId.HasValue)
            {
                paymentCommissions = await _context.Commissions
                    .Include(c => c.Coworking)
                    .Where(c => c.PaymentId == fn.PaymentId.Value)
                    .ToListAsync();
                commissions = _mapper.Map<List<CommissionListItemDto>>(paymentCommissions);
            }
            var transfers = new List<TransferListItemDto>();
            var coworkingTransfers = new List<Transfer>();
            if (fn.CoworkingId.HasValue)
            {
                coworkingTransfers = await _context.Transfers
                    .Where(t => t.CoworkingId == fn.CoworkingId.Value)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
                transfers = _mapper.Map<List<TransferListItemDto>>(coworkingTransfers);
            }
            var history = new List<object>();
            var auditLogs = await _context.AuditLogs
                .Where(a => a.EntityType == "FiscalNote" && a.EntityId == fiscalNoteId)
                .OrderByDescending(a => a.ActionDate)
                .ToListAsync();
            var userIds = auditLogs.Where(a => a.UserId.HasValue).Select(a => a.UserId.Value).Distinct().ToList();
            var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
            history = auditLogs.Select(a => new
            {
                action = a.Action,
                details = !string.IsNullOrEmpty(a.NewValues) ? a.NewValues : (a.OldValues ?? DefaultDisplayStrings.EmptyPlaceholder),
                createdAt = a.ActionDate,
                user = a.UserId.HasValue && users.ContainsKey(a.UserId.Value) ? users[a.UserId.Value] ?? DefaultDisplayStrings.EmptyPlaceholder : "Sistema"
            }).ToList<object>();
            string serviceType = DefaultDisplayStrings.EmptyPlaceholder;
            string serviceDetails = DefaultDisplayStrings.EmptyPlaceholder;
            if (fn.Payment?.Subscription?.Contract != null)
            {
                serviceType = "Endereço Fiscal";
                var contract = fn.Payment.Subscription.Contract;
                serviceDetails = $"Contrato #{contract.ContractNumber} - {(contract.CoworkingAddress?.Location != null ? $"{contract.CoworkingAddress.Location.City}/{contract.CoworkingAddress.Location.State}" : DefaultDisplayStrings.EmptyPlaceholder)}";
            }
            else if (fn.Payment?.Reservation != null)
            {
                serviceType = "Reserva de Sala";
                var reservation = fn.Payment.Reservation;
                serviceDetails = $"Reserva #{reservation.Id} - {(reservation.CoworkingAddress?.Location != null ? $"{reservation.CoworkingAddress.Location.City}/{reservation.CoworkingAddress.Location.State}" : DefaultDisplayStrings.EmptyPlaceholder)}";
            }
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
                    number = fn.Number ?? DefaultDisplayStrings.EmptyPlaceholder,
                    accessKey = fn.AccessKey ?? DefaultDisplayStrings.EmptyPlaceholder,
                    verificationCode = fn.VerificationCode ?? DefaultDisplayStrings.EmptyPlaceholder,
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
                    razaoSocial = fn.Client.RazaoSocial ?? DefaultDisplayStrings.EmptyPlaceholder,
                    nomeFantasia = fn.Client.NomeFantasia ?? DefaultDisplayStrings.EmptyPlaceholder,
                    cnpj = fn.Client.CNPJ ?? DefaultDisplayStrings.EmptyPlaceholder,
                    cpf = fn.Client.CPF ?? DefaultDisplayStrings.EmptyPlaceholder,
                    contactName = fn.Client.ContactName ?? DefaultDisplayStrings.EmptyPlaceholder,
                    contactEmail = fn.Client.ContactEmail ?? DefaultDisplayStrings.EmptyPlaceholder,
                    contactPhone = fn.Client.ContactPhone ?? DefaultDisplayStrings.EmptyPlaceholder,
                    userFullName = fn.Client.User?.FullName ?? DefaultDisplayStrings.EmptyPlaceholder
                } : null,
                coworking = fn.Coworking != null ? new
                {
                    id = fn.Coworking.Id,
                    name = fn.Coworking.Name ?? DefaultDisplayStrings.EmptyPlaceholder,
                    cnpj = fn.Coworking.CNPJ ?? DefaultDisplayStrings.EmptyPlaceholder
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
        public async Task<IActionResult> CancelFiscalNote(int fiscalNoteId, string cancellationReason, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cancellationReason))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.JustificativaCancelamentoObrigatoria;
                    return RedirectToAction(ActionNames.FiscalNotes);
                }
                var result = await _sefazService.CancelFiscalNoteAsync(fiscalNoteId, cancellationReason);
                if (result)
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceCancelled;
                }
                else
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorCancellingInvoice;
                }
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("cancelar nota fiscal", ex.Message);
            }
            return RedirectToAction(ActionNames.FiscalNotes);
        }
        [HttpGet]
        public async Task<IActionResult> SearchClients(string term = "", int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
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
            var results = _mapper.Map<List<ClientAutocompleteItemDto>>(clients);
            return Json(new
            {
                results,
                pagination = new
                {
                    more = (page * pageSize) < totalItems
                }
            });
        }
        [HttpGet]
        public async Task<IActionResult> CommissionFiscalNotes(int page = 1, int pageSize = 10, int? coworkingId = null, int? clientId = null, CancellationToken cancellationToken = default)
        {
            pageSize = FixedPageSize;
            if (page < 1) page = 1;
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
            if (clientId.HasValue)
            {
                var selectedClient = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == clientId.Value);
                ViewBag.SelectedClient = selectedClient;
            }
            return View(commissions);
        }
        [HttpGet]
        public async Task<IActionResult> CommissionFiscalNoteDetail(int id, CancellationToken cancellationToken = default)
        {
            var commission = await _context.Commissions
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(ct => ct.CoworkingAddress)
                                .ThenInclude(ca => ca.Location)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(ct => ct.Client)
                                .ThenInclude(cl => cl!.User)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Reservation)
                        .ThenInclude(r => r.CoworkingAddress)
                            .ThenInclude(ca => ca.Location)
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Reservation)
                        .ThenInclude(r => r.Client)
                            .ThenInclude(cl => cl!.User)
                .Include(c => c.Coworking)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (commission == null)
                return NotFound();
            var payment = commission.Payment;
            var client = payment?.Subscription?.Contract?.Client ?? payment?.Reservation?.Client;
            var contract = payment?.Subscription?.Contract;
            var reservation = payment?.Reservation;
            var coworking = commission.Coworking;
            var location = contract?.CoworkingAddress?.Location ?? reservation?.CoworkingAddress?.Location;
            var detail = new
            {
                Commission = new
                {
                    commission.Id,
                    commission.Amount,
                    commission.Percentage,
                    commission.TotalPaymentAmount,
                    commission.CreatedAt,
                    commission.IsTransferred,
                    commission.TransferredAt
                },
                Coworking = coworking == null ? null : new
                {
                    coworking.Id,
                    coworking.Name,
                    coworking.CNPJ,
                    coworking.ContactEmail,
                    coworking.ContactPhone
                },
                Payment = payment == null ? null : new
                {
                    payment.Id,
                    payment.Amount,
                    Status = payment.Status.ToString(),
                    Method = payment.Method.ToString(),
                    payment.DueDate,
                    payment.PaidAt,
                    payment.CreatedAt,
                    payment.TransactionId
                },
                Contract = contract == null ? null : new
                {
                    contract.Id,
                    contract.ContractNumber,
                    contract.StartDate,
                    contract.EndDate,
                    contract.MonthlyValue,
                    Status = contract.Status.ToString(),
                    Address = FormatAddress(contract.CoworkingAddress),
                    Location = location == null ? null : new { location.City, location.State }
                },
                Reservation = reservation == null ? null : new
                {
                    reservation.Id,
                    reservation.ReservationDate,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.RoomType,
                    reservation.TotalValue,
                    reservation.Status,
                    Address = FormatAddress(reservation.CoworkingAddress),
                    Location = location == null ? null : new { location!.City, location.State }
                },
                Client = client == null ? null : new
                {
                    client.Id,
                    Name = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName,
                    client.RazaoSocial,
                    client.NomeFantasia,
                    client.ContactName,
                    client.CNPJ,
                    client.CPF,
                    client.ContactEmail,
                    client.ContactPhone
                },
                ServiceType = contract != null ? "Endereço Fiscal" : "Reserva de Sala"
            };
            return Json(detail);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueCommissionFiscalNote(int commissionId, CancellationToken cancellationToken = default)
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
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CommissionNotFound;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn =>
                        (fn.PaymentId == commission.PaymentId && fn.Type == "Intermediacao") ||
                        (fn.CommissionAmount == commission.Amount && fn.CoworkingId == commission.CoworkingId && fn.Type == "Intermediacao" && fn.Status == FiscalNoteStatus.Emitida)
                    );
                if (existingNote != null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.NfCommissionAlreadyExists;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                string state = "ES";
                if (commission.Payment?.Subscription?.Contract?.CoworkingAddress?.Location != null)
                {
                    state = commission.Payment.Subscription.Contract.CoworkingAddress.Location.State;
                }
                else if (commission.Payment?.Reservation?.CoworkingAddress?.Location != null)
                {
                    state = commission.Payment.Reservation.CoworkingAddress.Location.State;
                }
                if (!_sefazService.IsStateSupported(state))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SefazNotImplementedForState(state);
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                var sefazConfig = await _sefazService.GetConfigurationByStateAsync(state, "Producao");
                if (sefazConfig == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SefazConfigNotFoundForState(state);
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
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
                    Status = FiscalNoteStatus.Pendente,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();
                try
                {
                    var emittedNote = await _sefazService.IssueIntermediationNoteAsync(fiscalNote, sefazConfig);
                    _context.FiscalNotes.Update(emittedNote);
                    await _context.SaveChangesAsync();
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.CommissionInvoiceEmitted(emittedNote.AccessKey);
                }
                catch (Exception ex)
                {
                    _context.FiscalNotes.Update(fiscalNote);
                    await _context.SaveChangesAsync();
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("emitir NF", ex.Message);
                }
                return RedirectToAction(ActionNames.CommissionFiscalNotes);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("processar emissão de NF", ex.Message);
                return RedirectToAction(ActionNames.CommissionFiscalNotes);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCommissionFiscalNote(int commissionId, IFormFile pdfFile, IFormFile? xmlFile, CancellationToken cancellationToken = default)
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
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CommissionNotFound;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn =>
                        (fn.PaymentId == commission.PaymentId && fn.Type == "Intermediacao") ||
                        (fn.CommissionAmount == commission.Amount && fn.CoworkingId == commission.CoworkingId && fn.Type == "Intermediacao")
                    );
                if (existingNote != null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.NfCommissionAlreadyExistsForCommission;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SelectPdfFile;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.FileMustBePdf;
                    return RedirectToAction(ActionNames.CommissionFiscalNotes);
                }
                var storage = await _storageFactory.GetStorageServiceAsync();
                var pdfFileName = $"imported_commission_{commissionId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                using var ms = new MemoryStream();
                await pdfFile.CopyToAsync(ms);
                var pdfPath = await storage.SaveFileAsync(ms.ToArray(), pdfFileName, $"invoices/imported/admin");
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
                    Status = FiscalNoteStatus.Emitida,
                    PdfUrl = pdfPath,
                    CreatedAt = DateTime.UtcNow
                };
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
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CommissionInvoiceImported;
                return RedirectToAction(ActionNames.CommissionFiscalNotes);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("importar NF", ex.Message);
                return RedirectToAction(ActionNames.CommissionFiscalNotes);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Transfers(
            int page = 1,
            int pageSize = 10,
            int? coworkingId = null,
            TransferStatus? status = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            pageSize = FixedPageSize;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTransfer(int transferId, CancellationToken cancellationToken = default)
        {
            try
            {
                var transfer = await _context.Transfers.FindAsync(transferId);
                if (transfer == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PayoutRequestNotFound;
                    return RedirectToAction(ActionNames.Transfers);
                }
                if (transfer.Status != TransferStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyPendingPayoutsCanBeApproved;
                    return RedirectToAction(ActionNames.Transfers);
                }
                await _commissionService.ApproveTransferAsync(transferId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TransferApproved(transfer.Amount);
                return RedirectToAction(ActionNames.Transfers);
            }
            catch (InvalidOperationException ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.Transfers);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("aprovar repasse", ex.Message);
                return RedirectToAction(ActionNames.Transfers);
            }
        }
        [HttpGet]
        public async Task<IActionResult> TransferDetails(int id, CancellationToken cancellationToken = default)
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
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.TransferNotFound;
                return RedirectToAction(ActionNames.Transfers);
            }
            var relatedCommissions = new List<Commission>();
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
        [HttpGet]
        public async Task<IActionResult> TransferSchedule(CancellationToken cancellationToken = default)
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
        [HttpGet]
        public async Task<IActionResult> TransferDetailPartial(int id, CancellationToken cancellationToken = default)
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
        public async Task<IActionResult> QuickClientRegistration(CancellationToken cancellationToken = default)
        {
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
            var input = BuildQuickClientRegistrationInput(contactEmail, contactName, razaoSocial, nomeFantasia, cnpj, cpf, contactPhone, address, city, state, zipCode, coworkingAddressId, monthlyValue, paymentMethod);
            var result = await ExecuteQuickClientRegistrationAsync(input);
            if (result == null) return RedirectToAction(ActionNames.QuickClientRegistration);
            return ApplyQuickClientRegistrationResult(result);
        }

        private static QuickClientRegistrationInput BuildQuickClientRegistrationInput(string contactEmail, string contactName, string? razaoSocial, string? nomeFantasia, string? cnpj, string? cpf, string? contactPhone, string? address, string? city, string? state, string? zipCode, int coworkingAddressId, decimal monthlyValue, string paymentMethod) => new()
        {
            ContactEmail = contactEmail,
            ContactName = contactName,
            RazaoSocial = razaoSocial,
            NomeFantasia = nomeFantasia,
            Cnpj = cnpj,
            Cpf = cpf,
            ContactPhone = contactPhone,
            Address = address,
            City = city,
            State = state,
            ZipCode = zipCode,
            CoworkingAddressId = coworkingAddressId,
            MonthlyValue = monthlyValue,
            PaymentMethod = paymentMethod
        };

        private async Task<QuickClientRegistrationResult?> ExecuteQuickClientRegistrationAsync(QuickClientRegistrationInput input)
        {
            var adminUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                return await _quickClientRegistrationService.RegisterAsync(input, adminUserId,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.FirstOrDefault(), HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("cadastrar cliente e criar contrato", ex.Message);
                return null;
            }
        }

        private IActionResult ApplyQuickClientRegistrationResult(QuickClientRegistrationResult result)
        {
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = GetQuickClientRegistrationErrorMessage(result.FailureKey, result.FailureArgument);
                return RedirectToAction(ActionNames.QuickClientRegistration);
            }
            if (!string.IsNullOrEmpty(result.WarningKey))
                TempData[TempDataKeys.Warning] = result.WarningKey == "PixNotGenerated"
                    ? WarningMessages.ContractCreatedPixNotGenerated(result.WarningArgument ?? "")
                    : WarningMessages.ClientRegisteredEmailSendFailed(result.WarningArgument ?? "");
            if (result.PixCode != null) TempData[TempDataKeys.PixCode] = result.PixCode;
            if (result.PaymentId != null) TempData[TempDataKeys.PaymentId] = result.PaymentId;
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.ClientRegisteredContractCreated(result.ContractNumber!);
            return result.RedirectToSuccessPage && result.PaymentId != null
                ? RedirectToAction(ActionNames.QuickClientRegistrationSuccess, ControllerNames.Admin, new { paymentId = result.PaymentId })
                : RedirectToAction(ActionNames.Contracts, ControllerNames.Admin);
        }

        private static string GetQuickClientRegistrationErrorMessage(string? failureKey, string? failureArgument)
        {
            if (string.IsNullOrEmpty(failureKey)) return ErrorMessages.FillAllRequiredFields;
            return failureKey switch
            {
                QuickClientRegistrationService.FailureKeyEmailAndContactNameRequired => ErrorMessages.EmailAndContactNameRequired,
                QuickClientRegistrationService.FailureKeyAnnualValueMustBeGreaterThanZero => ErrorMessages.AnnualValueMustBeGreaterThanZero,
                QuickClientRegistrationService.FailureKeyAddressNotFound => ErrorMessages.AddressNotFound,
                QuickClientRegistrationService.FailureKeyPartnerSuspendedNoNewContracts => ErrorMessages.PartnerSuspendedNoNewContracts,
                QuickClientRegistrationService.FailureKeyAddressNotActiveForSales => ErrorMessages.AddressNotActiveForSales,
                QuickClientRegistrationService.FailureKeyAddressNotAvailableForContractSelected => ErrorMessages.AddressNotAvailableForContractSelected,
                QuickClientRegistrationService.FailureKeyClientAlreadyExistsWithEmail => ErrorMessages.ClientAlreadyExistsWithEmail(failureArgument ?? ""),
                QuickClientRegistrationService.FailureKeyCnpjAlreadyRegistered => ErrorMessages.CnpjAlreadyRegistered(failureArgument ?? ""),
                QuickClientRegistrationService.FailureKeyCpfAlreadyRegistered => ErrorMessages.CpfAlreadyRegistered(failureArgument ?? ""),
                _ => ErrorMessages.FillAllRequiredFields
            };
        }
        [HttpGet]
        public async Task<IActionResult> QuickClientRegistrationSuccess(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                return RedirectToAction(ActionNames.Contracts, ControllerNames.Admin);
            }
            ViewBag.Payment = payment;
            ViewBag.PixCode = payment.PixCode;
            ViewBag.PixExpiresAt = payment.PixExpiresAt;
            return View();
        }
        private static string? FormatAddress(CoworkingAddress? addr)
        {
            if (addr == null) return null;
            var parts = new List<string> { addr.Address, addr.Number, addr.Complement, addr.Neighborhood, addr.City, addr.State, addr.ZipCode };
            return string.Join(", ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        private static bool IsDuplicateIdempotencyKeyException(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("IX_FiscalNotes_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
        }
        #region Endpoints AJAX para Modal de Percentagem
        [HttpGet]
        public async Task<IActionResult> GetCoworkingServicesJson(int coworkingId, CancellationToken cancellationToken = default)
        {
            try
            {
                var coworking = await _context.Coworkings.FindAsync(coworkingId);
                if (coworking == null)
                    return Json(new { success = false, message = JsonMessage.CoworkingNaoEncontrado });
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                var inactiveServices = _context.Services.Where(ps => !ps.IsActive).Select(x => x.Name).ToList();
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
                        createdAt = ps.CreatedAt.ToString(DateFormatConstants.DisplayDateOnly)
                    })
                    .ToListAsync();
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePartnerServiceJson([FromBody] SavePartnerServiceRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = JsonMessage.DadosInvalidos });
                var coworking = await _context.Coworkings.FindAsync(request.CoworkingId);
                if (coworking == null)
                    return Json(new { success = false, message = JsonMessage.CoworkingNaoEncontrado });
                var service = await _context.Services.FindAsync(request.ServiceId);
                if (service == null)
                    return Json(new { success = false, message = JsonMessage.ServicoNaoEncontrado });
                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                    return Json(new { success = false, message = JsonMessage.ServicoNaoDisponivelMenusDesabilitados });
                if (request.Percentage < 0 || request.Percentage > 100)
                    return Json(new { success = false, message = JsonMessage.PercentagemEntreZeroECem });
                if (request.Id > 0)
                {
                    var existing = await _context.PartnerServices.FindAsync(request.Id);
                    if (existing == null || existing.CoworkingId != request.CoworkingId)
                        return Json(new { success = false, message = JsonMessage.ServicoParceiroNaoEncontrado });
                    existing.ServiceId = request.ServiceId;
                    existing.Percentage = request.Percentage;
                    existing.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                }
                else
                {
                    var existing = await _context.PartnerServices
                        .FirstOrDefaultAsync(ps => ps.CoworkingId == request.CoworkingId && ps.ServiceId == request.ServiceId);
                    if (existing != null)
                        return Json(new { success = false, message = JsonMessage.ServicoJaCadastradoParaParceiro });
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
                return Json(new { success = true, message = JsonMessage.ServicoSalvoComSucesso });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao salvar serviço: {ex.Message}" });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartnerServiceJson([FromBody] DeletePartnerServiceRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = JsonMessage.DadosInvalidos });
                var partnerService = await _context.PartnerServices.FindAsync(request.Id);
                if (partnerService == null)
                    return Json(new { success = false, message = JsonMessage.ServicoParceiroNaoEncontrado });
                _context.PartnerServices.Remove(partnerService);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = JsonMessage.ServicoRemovidoComSucesso });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao remover serviço: {ex.Message}" });
            }
        }
        #endregion
        #region Invoice Validation (Validação de Notas Fiscais)
        [HttpGet]
        public async Task<IActionResult> InvoiceValidation(CancellationToken cancellationToken = default)
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveInvoice(int invoiceId, string? notes)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _monthlySettlementService.ApproveInvoiceAsync(invoiceId, userId, notes);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceApprovedTransferScheduled;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("aprovar nota fiscal", ex.Message);
            }
            return RedirectToAction(ActionNames.InvoiceValidation);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectInvoice(int invoiceId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PleaseInformRejectionReason;
                    return RedirectToAction(ActionNames.InvoiceValidation);
                }
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _monthlySettlementService.RejectInvoiceAsync(invoiceId, userId, rejectionReason);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceRejectedPartnerNotified;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("rejeitar nota fiscal", ex.Message);
            }
            return RedirectToAction(ActionNames.InvoiceValidation);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteTransfer(int settlementId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _monthlySettlementService.ExecuteTransferAsync(settlementId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.TransferExecuted;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("executar transferência", ex.Message);
            }
            return RedirectToAction(ActionNames.InvoiceValidation);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceCloseMonth(int coworkingId, int month, int year, CancellationToken cancellationToken = default)
        {
            try
            {
                await _monthlySettlementService.CloseMonthAsync(coworkingId, month, year);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.MonthClosed(month, year);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("fechar mês", ex.Message);
            }
            return RedirectToAction(ActionNames.InvoiceValidation);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAutomaticInvoice(int settlementId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _monthlySettlementService.GenerateAutomaticInvoiceAsync(settlementId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceGeneratedAutomatically;
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("gerar NF automática", ex.Message);
            }
            return RedirectToAction(ActionNames.InvoiceValidation);
        }
        #endregion
    }
}