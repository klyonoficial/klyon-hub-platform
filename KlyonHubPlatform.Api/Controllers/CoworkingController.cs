using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Api.Dtos;
using KlyonHubPlatform.Api.Dtos.Coworking;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = RoleNames.CoworkingPartner + "," + RoleNames.Administrator)]
    public class CoworkingController : Controller
    {
        private readonly ICoworkingService _coworkingService;
        private readonly ICoworkingAppService _coworkingAppService;
        private readonly ICommissionService _commissionService;
        private readonly IPaymentService _paymentService;
        private readonly StorageServiceFactory _storageFactory;
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly IAuditService _auditService;
        private readonly IReservationTransferService _reservationTransferService;
        private readonly ISystemConfigurationService _systemConfig;
        private readonly ISefazService _sefazService;
        private readonly IMonthlySettlementService _monthlySettlementService;
        private readonly IMapper _mapper;
        public CoworkingController(
            ICoworkingService coworkingService,
            ICoworkingAppService coworkingAppService,
            ICommissionService commissionService,
            IPaymentService paymentService,
            StorageServiceFactory storageFactory,
            IAddressDeclarationService addressDeclarationService,
            IAuditService auditService,
            IReservationTransferService reservationTransferService,
            ISystemConfigurationService systemConfig,
            ISefazService sefazService,
            IMonthlySettlementService monthlySettlementService,
            IMapper mapper)
        {
            _coworkingService = coworkingService;
            _coworkingAppService = coworkingAppService;
            _commissionService = commissionService;
            _paymentService = paymentService;
            _storageFactory = storageFactory;
            _addressDeclarationService = addressDeclarationService;
            _auditService = auditService;
            _reservationTransferService = reservationTransferService;
            _systemConfig = systemConfig;
            _sefazService = sefazService;
            _monthlySettlementService = monthlySettlementService;
            _mapper = mapper;
        }
        private async Task RefreshUserPrincipalAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName)
            };
            var tokenClaim = User.FindFirst(AuthConstants.TokenClaimType);
            if (tokenClaim != null)
            {
                claims.Add(new Claim(AuthConstants.TokenClaimType, tokenClaim.Value));
            }
            foreach (var roleClaim in User.FindAll(ClaimTypes.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            }
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal);
            HttpContext.Session.SetString(SessionKeys.UserName, user.FullName);
        }
        private async Task<long> GetMaxPhotoSizeBytesAsync()
        {
            var mb = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mb, out var n) && n > 0) return n * 1024L * 1024L;
            return 5 * 1024L * 1024L;
        }
        private async Task ReturnToReviewIfApprovedAsync(Domain.Entities.Coworking c, int userId, string reason)
        {
            if (c.ApprovalStatus != CoworkingApprovalStatus.Approved) return;
            c.ApprovalStatus = CoworkingApprovalStatus.UnderReview;
            c.SubmittedAt = DateTime.UtcNow;
            var reasonEsc = System.Text.Json.JsonSerializer.Serialize(reason);
            await _auditService.LogActionAsync(userId, "ReturnedToReview", "Coworking", c.Id, "{\"ApprovalStatus\":\"Approved\"}", "{\"ApprovalStatus\":\"UnderReview\",\"Reason\":" + reasonEsc + "}");
        }
        private async Task<List<object>> BuildAdministrativeAlertsAsync(Coworking coworkingEntity)
        {
            var alerts = new List<object>();
            if (coworkingEntity.ApprovalStatus == CoworkingApprovalStatus.AwaitingAdjustment)
            {
                alerts.Add(new { Type = BootstrapBadgeCss.Danger, Icon = "bi-exclamation-triangle-fill", Title = "Cadastro Aguardando Ajuste", Message = !string.IsNullOrEmpty(coworkingEntity.RejectionReason) ? coworkingEntity.RejectionReason : "Seu cadastro foi reprovado e requer ajustes. Verifique os itens indicados e reenvie para análise.", ActionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking), ActionText = "Ver Perfil" });
            }
            var rejectedDocs = await _coworkingAppService.GetRejectedCoworkingDocumentsAsync(coworkingEntity.Id);
            foreach (var doc in rejectedDocs)
            {
                var docTypeName = GetDocumentTypeDisplayName(doc.Type);
                alerts.Add(new { Type = BootstrapBadgeCss.Danger, Icon = "bi-file-earmark-x-fill", Title = $"Documento Rejeitado: {docTypeName}", Message = !string.IsNullOrEmpty(doc.RejectionReason) ? doc.RejectionReason : "Este documento foi rejeitado e requer correção.", ActionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking), ActionText = "Ver Documentos" });
            }
            var pendingDocs = await _coworkingAppService.GetPendingCoworkingDocumentsCountAsync(coworkingEntity.Id);
            if (pendingDocs > 0) alerts.Add(new { Type = BootstrapBadgeCss.Info, Icon = "bi-file-earmark-check", Title = "Documentos Em Análise", Message = $"{pendingDocs} documento(s) aguardando análise administrativa.", ActionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking), ActionText = "Ver Documentos" });
            var pendingNf = 0;
            try
            {
                pendingNf = await _coworkingAppService.GetPendingPaymentsWithoutFiscalNoteCountAsync(coworkingEntity.Id);
            }
            catch { }
            if (pendingNf > 0) alerts.Add(new { Type = BootstrapBadgeCss.Warning, Icon = "bi-receipt", Title = "NF de Serviço Não Emitidas", Message = $"{pendingNf} pagamento(s) aguardando emissão de NF. Você só recebe o repasse após emitir a nota fiscal.", ActionUrl = Url.Action(ActionNames.ServiceFiscalNotes, ControllerNames.Coworking), ActionText = "Emitir NF" });
            var rejectedNf = 0;
            try
            {
                rejectedNf = await _coworkingAppService.GetRejectedFiscalNotesCountAsync(coworkingEntity.Id);
            }
            catch { }
            if (rejectedNf > 0) alerts.Add(new { Type = BootstrapBadgeCss.Danger, Icon = "bi-x-circle-fill", Title = "NF Importadas Rejeitadas", Message = $"{rejectedNf} NF(s) importada(s) foram rejeitadas. Verifique o motivo e reenvie pela tela Emitir NF de Serviço.", ActionUrl = Url.Action(ActionNames.ServiceFiscalNotes, ControllerNames.Coworking), ActionText = "Emitir / Importar NF" });
            var addrs = await _coworkingAppService.GetCoworkingAddressesWithCapacityPendingOrRejectedAsync(coworkingEntity.Id);
            foreach (var a in addrs)
            {
                if (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending) alerts.Add(new { Type = BootstrapBadgeCss.Warning, Icon = "bi-clock-history", Title = "Capacidade Fiscal Aguardando Aprovação", Message = $"A alteração de capacidade do endereço {a.Address}, {a.City} está aguardando aprovação administrativa.", ActionUrl = Url.Action(ActionNames.MyAddresses, ControllerNames.Coworking), ActionText = "Ver Endereços" });
                else alerts.Add(new { Type = BootstrapBadgeCss.Danger, Icon = "bi-x-circle-fill", Title = "Capacidade Fiscal Rejeitada", Message = $"A alteração de capacidade do endereço {a.Address}, {a.City} foi rejeitada.", ActionUrl = Url.Action(ActionNames.MyAddresses, ControllerNames.Coworking), ActionText = "Ver Endereços" });
            }
            return alerts;
        }
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coworking = await _coworkingService.GetByUserIdAsync(userId, cancellationToken);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var coworkingWithAddresses = await _coworkingAppService.GetCoworkingWithAddressesAsync(coworkingEntity.Id);
            var contracts = await _coworkingAppService.GetContractsForDashboardAsync(coworkingEntity.Id);
            var commissions = await _commissionService.GetPendingCommissionsAsync(coworkingEntity.Id);
            var totalPending = commissions.Sum(c => c.TotalPaymentAmount - c.Amount);
            var pendingPaymentsCount = await _coworkingAppService.GetPendingPaymentsCountForCoworkingAsync(coworkingEntity.Id);
            var pendingReservationsCount = 0;
            try
            {
                var pendingInterests = 0;
                try
                {
                    pendingInterests = await _coworkingAppService.GetPendingInterestsCountAsync(coworkingEntity.Id);
                }
                catch
                {
                }
                var confirmedReservations = await _coworkingAppService.GetConfirmedReservationsCountAsync(coworkingEntity.Id);
                pendingReservationsCount = pendingInterests + confirmedReservations;
            }
            catch
            {
                pendingReservationsCount = 0;
            }
            var administrativeAlerts = await BuildAdministrativeAlertsAsync(coworkingEntity);
            var pendenciasTotal = administrativeAlerts.Count;
            var pendingNfCount = 0;
            try
            {
                pendingNfCount = await _coworkingAppService.GetPendingPaymentsWithoutFiscalNoteCountAsync(coworkingEntity.Id);
            }
            catch { }
            var coworkingForNotification = coworkingWithAddresses ?? coworkingEntity;
            if (coworkingForNotification.HasPendingStatusNotification)
            {
                ViewBag.HasStatusNotification = true;
                ViewBag.StatusNotificationType = coworkingForNotification.LastNotifiedStatus;
            }
            else
            {
                ViewBag.HasStatusNotification = false;
            }
            ViewBag.Coworking = coworkingWithAddresses ?? coworkingEntity;
            ViewBag.ActiveContracts = contracts.Count(c => c.Status == ContractStatus.Active);
            ViewBag.PendingCommissions = totalPending;
            ViewBag.Commissions = commissions;
            ViewBag.TotalAddresses = coworkingWithAddresses?.Addresses?.Count ?? 0;
            ViewBag.PendingPaymentsCount = pendingPaymentsCount;
            ViewBag.PendingReservationsCount = pendingReservationsCount;
            ViewBag.AdministrativeAlerts = administrativeAlerts;
            ViewBag.PendingFiscalNotesCount = pendingNfCount;
            ViewBag.PendenciasTotal = pendenciasTotal;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissStatusNotification(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var coworking = await _coworkingService.GetByUserIdAsync(userId, cancellationToken);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity != null)
            {
                await _coworkingAppService.DismissStatusNotificationAsync(coworkingEntity.Id);
            }
            return RedirectToAction(ActionNames.Dashboard);
        }
        [HttpGet]
        public async Task<IActionResult> GetPendencyDetails(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
                return Json(new { pendencies = Array.Empty<object>() });
            var pendencies = new List<object>();
            if (coworkingEntity.ApprovalStatus == CoworkingApprovalStatus.AwaitingAdjustment)
            {
                pendencies.Add(new
                {
                    type = BootstrapBadgeCss.Danger,
                    icon = "bi-exclamation-triangle-fill",
                    title = "Cadastro Aguardando Ajuste",
                    message = !string.IsNullOrEmpty(coworkingEntity.RejectionReason) ? coworkingEntity.RejectionReason : "Seu cadastro foi reprovado e requer ajustes. Verifique os itens indicados e reenvie para análise.",
                    actionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking),
                    actionText = "Ver Perfil",
                    details = (string[]?)null
                });
            }
            var rejectedDocs = await _coworkingAppService.GetRejectedCoworkingDocumentsAsync(coworkingEntity.Id);
            foreach (var doc in rejectedDocs)
            {
                var docTypeName = GetDocumentTypeDisplayName(doc.Type);
                pendencies.Add(new
                {
                    type = BootstrapBadgeCss.Danger,
                    icon = "bi-file-earmark-x-fill",
                    title = $"Documento Rejeitado: {docTypeName}",
                    message = !string.IsNullOrEmpty(doc.RejectionReason) ? doc.RejectionReason : "Este documento foi rejeitado e requer correção.",
                    actionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking) + "#documents",
                    actionText = "Ver Documentos",
                    details = (string[]?)null
                });
            }
            var pendingDocsList = await _coworkingAppService.GetPendingCoworkingDocumentsAsync(coworkingEntity.Id);
            if (pendingDocsList.Count > 0)
            {
                var docTypeNames = pendingDocsList.Select(d => GetDocumentTypeDisplayName(d.Type)).Distinct().OrderBy(x => x).ToArray();
                pendencies.Add(new
                {
                    type = BootstrapBadgeCss.Info,
                    icon = "bi-file-earmark-check",
                    title = "Documentos Em Análise",
                    message = $"{pendingDocsList.Count} documento(s) aguardando análise administrativa.",
                    actionUrl = Url.Action(ActionNames.Profile, ControllerNames.Coworking) + "#documents",
                    actionText = "Ver Documentos",
                    details = docTypeNames
                });
            }
            int pendingNf = 0;
            try
            {
                pendingNf = await _coworkingAppService.GetPendingPaymentsWithoutFiscalNoteCountAsync(coworkingEntity.Id);
            }
            catch { }
            if (pendingNf > 0)
            {
                pendencies.Add(new
                {
                    type = BootstrapBadgeCss.Warning,
                    icon = "bi-receipt",
                    title = "NF de Serviço Não Emitidas",
                    message = $"{pendingNf} pagamento(s) aguardando emissão de NF. Você só recebe o repasse após emitir a nota fiscal.",
                    actionUrl = Url.Action(ActionNames.ServiceFiscalNotes, ControllerNames.Coworking),
                    actionText = "Emitir NF",
                    details = (string[]?)null
                });
            }
            int rejectedNf = 0;
            try
            {
                rejectedNf = await _coworkingAppService.GetRejectedFiscalNotesCountAsync(coworkingEntity.Id);
            }
            catch { }
            if (rejectedNf > 0)
            {
                pendencies.Add(new
                {
                    type = BootstrapBadgeCss.Danger,
                    icon = "bi-x-circle-fill",
                    title = "NF Importadas Rejeitadas",
                    message = $"{rejectedNf} NF(s) importada(s) foram rejeitadas. Verifique o motivo e reenvie pela tela Emitir NF de Serviço.",
                    actionUrl = Url.Action(ActionNames.ServiceFiscalNotes, ControllerNames.Coworking),
                    actionText = "Emitir / Importar NF",
                    details = (string[]?)null
                });
            }
            var addrs = await _coworkingAppService.GetCoworkingAddressesWithCapacityPendingOrRejectedAsync(coworkingEntity.Id);
            foreach (var a in addrs)
            {
                if (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending)
                    pendencies.Add(new
                    {
                        type = BootstrapBadgeCss.Warning,
                        icon = "bi-clock-history",
                        title = "Capacidade Fiscal Aguardando Aprovação",
                        message = $"A alteração de capacidade do endereço {a.Address}, {a.City} está aguardando aprovação administrativa.",
                        actionUrl = Url.Action(ActionNames.MyAddresses, ControllerNames.Coworking),
                        actionText = "Ver Endereços",
                        details = (string[]?)null
                    });
                else
                    pendencies.Add(new
                    {
                        type = BootstrapBadgeCss.Danger,
                        icon = "bi-x-circle-fill",
                        title = "Capacidade Fiscal Rejeitada",
                        message = $"A alteração de capacidade do endereço {a.Address}, {a.City} foi rejeitada.",
                        actionUrl = Url.Action(ActionNames.MyAddresses, ControllerNames.Coworking),
                        actionText = "Ver Endereços",
                        details = (string[]?)null
                    });
            }
            return Json(new { pendencies });
        }
        private static string GetDocumentTypeDisplayName(CoworkingDocumentType type) => type.ToDisplayString();
        [HttpGet]
        public async Task<IActionResult> Register(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var existingCoworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = existingCoworking.FirstOrDefault();
            if (coworkingEntity != null)
            {
                return RedirectToAction(ActionNames.Profile);
            }
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
            var user = await _coworkingAppService.FindUserAsync(userId);
            ViewBag.UserFullName = user?.FullName ?? string.Empty;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Register(Coworking coworking,
            List<IFormFile>? Photos,
            IFormFile? UtilityBill,
            IFormFile? AddressAuthorization,
            IFormFile? CNPJCard)
        {
            try
            {
                if (coworking == null)
                {
                    coworking = new Coworking
                    {
                        Name = Request.Form["Name"].ToString(),
                        CNPJ = Request.Form["CNPJ"].ToString(),
                        TradeName = Request.Form["TradeName"].ToString(),
                        ContactEmail = Request.Form["ContactEmail"].ToString(),
                        ContactPhone = Request.Form["ContactPhone"].ToString(),
                        BankName = Request.Form["BankName"].ToString(),
                        BankAgency = Request.Form["BankAgency"].ToString(),
                        BankAccount = Request.Form["BankAccount"].ToString(),
                        BankPixKey = Request.Form["BankPixKey"].ToString()
                    };
                }
                if (string.IsNullOrWhiteSpace(coworking.Name) || string.IsNullOrWhiteSpace(coworking.CNPJ))
                {
                    TempData[ErrorMessages.TempDataKey] = "Nome e CNPJ são obrigatórios.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _coworkingAppService.FindUserAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }
                var cnpjClean = coworking.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                {
                    TempData[ErrorMessages.TempDataKey] = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _coworkingAppService.FindUserAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }
                var existingCNPJ = await _coworkingAppService.GetCoworkingByCnpjAsync(cnpjClean);
                if (existingCNPJ != null)
                {
                    TempData[ErrorMessages.TempDataKey] = $"O CNPJ {coworking.CNPJ} já está cadastrado no sistema.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _coworkingAppService.FindUserAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }
                coworking.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                coworking.UserId = userId;
                coworking.IsActive = true;
                coworking.IsPartner = true;
                var createdCoworking = await _coworkingService.CreateAsync(coworking);
                createdCoworking.ApprovalStatus = CoworkingApprovalStatus.UnderReview;
                createdCoworking.SubmittedAt = DateTime.UtcNow;
                await _auditService.LogActionAsync(userId, "Submitted", "Coworking", createdCoworking.Id, null, "{\"ApprovalStatus\":\"UnderReview\"}");
                var maxPhotoBytes = await GetMaxPhotoSizeBytesAsync();
                var storageService = await _storageFactory.GetStorageServiceAsync();
                if (Photos != null && Photos.Count > 0)
                {
                    var photoCount = 0;
                    foreach (var photo in Photos.Take(2))
                    {
                        if (photo.Length > 0 && photo.Length <= maxPhotoBytes)
                        {
                            using var stream = new MemoryStream();
                            await photo.CopyToAsync(stream);
                            var fileBytes = stream.ToArray();
                            var fileName = $"photo_{createdCoworking.Id}_{photoCount}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(photo.FileName)}";
                            var filePath = await storageService.SaveFileAsync(fileBytes, fileName, $"coworkings/{createdCoworking.Id}/photos");
                            var coworkingPhoto = new CoworkingPhoto
                            {
                                CoworkingId = createdCoworking.Id,
                                FilePath = filePath,
                                Description = photo.FileName,
                                IsMain = photoCount == 0,
                                DisplayOrder = photoCount
                            };
                            _coworkingAppService.AddCoworkingPhoto(coworkingPhoto);
                            photoCount++;
                        }
                    }
                }
                var documentsToProcess = new[]
                {
                    (UtilityBill, CoworkingDocumentType.UtilityBill, "utility_bill"),
                    (AddressAuthorization, CoworkingDocumentType.AddressAuthorization, "address_authorization"),
                    (CNPJCard, CoworkingDocumentType.CNPJCard, "cnpj_card")
                };
                foreach (var (file, docType, folderName) in documentsToProcess)
                {
                    if (file != null && file.Length > 0)
                    {
                        using var stream = new MemoryStream();
                        await file.CopyToAsync(stream);
                        var fileBytes = stream.ToArray();
                        var fileName = $"{folderName}_{createdCoworking.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
                        var filePath = await storageService.SaveFileAsync(fileBytes, fileName, $"coworkings/{createdCoworking.Id}/documents");
                        var coworkingDocument = new CoworkingDocument
                        {
                            CoworkingId = createdCoworking.Id,
                            Type = docType,
                            FileName = file.FileName,
                            FilePath = filePath,
                            IsValid = true,
                            ExpiresAt = docType == CoworkingDocumentType.UtilityBill
                                ? DateTime.UtcNow.AddDays(90)
                                : null
                        };
                        _coworkingAppService.AddCoworkingDocument(coworkingDocument);
                    }
                }
                var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                if (showCoworkingMenus)
                {
                    var operatingHours = await _coworkingAppService.GetCoworkingOperatingHoursCountAsync(createdCoworking.Id);
                    if (operatingHours == 0)
                    {
                        TempData[TempDataKeys.Warning] = WarningMessages.CoworkingRegisteredConfigureOperatingHours;
                    }
                }
                await _coworkingAppService.SaveChangesAsync();
                var userToUpdate = await _coworkingAppService.FindUserAsync(userId);
                if (userToUpdate != null && !string.IsNullOrWhiteSpace(coworking.Name))
                {
                    userToUpdate.FullName = coworking.Name;
                    userToUpdate.UpdatedAt = DateTime.UtcNow;
                    _coworkingAppService.UpdateUser(userToUpdate);
                    await _coworkingAppService.SaveChangesAsync();
                    await RefreshUserPrincipalAsync(userToUpdate);
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CoworkingRegistered;
                return RedirectToAction(ActionNames.Dashboard);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao cadastrar coworking: {ex.Message}";
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                ViewBag.UserEmail = userEmail;
                return View(coworking ?? new Coworking());
            }
        }
        [HttpGet]
        public async Task<IActionResult> MyAddresses(string? search = null, string? status = null, int? locationId = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var (addresses, totalItems) = await _coworkingAppService.GetCoworkingAddressesPagedAsync(coworkingEntity.Id, search, status, locationId, page, pageSize);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.LocationId = locationId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            var locations = await _coworkingAppService.GetActiveLocationsAsync();
            ViewBag.Locations = locations;
            var addressIds = addresses.Select(a => a.Id).ToList();
            var activeContractsByAddress = await _coworkingAppService.GetActiveContractsCountByAddressAsync(addressIds);
            ViewBag.ActiveContractsByAddress = activeContractsByAddress;
            var hasFiscalAddress = await _coworkingAppService.HasFiscalAddressAsync(coworkingEntity.Id);
            ViewBag.CanAddAddress = !hasFiscalAddress;
            return View(addresses);
        }
        [HttpGet]
        public async Task<IActionResult> AddAddress(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity != null)
            {
                var hasFiscalAddress = await _coworkingAppService.HasFiscalAddressAsync(coworkingEntity.Id);
                if (hasFiscalAddress)
                {
                    TempData[ErrorMessages.TempDataKey] = "Não é possível adicionar outro endereço. Você já possui um endereço fiscal cadastrado.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
            }
            bool hasRoomRental = false;
            if (coworkingEntity != null)
            {
                try
                {
                    hasRoomRental = await _coworkingAppService.HasRoomRentalAsync(coworkingEntity.Id);
                }
                catch
                {
                    hasRoomRental = false;
                }
            }
            var locations = await _coworkingAppService.GetActiveLocationsAsync();
            ViewBag.Locations = locations;
            ViewBag.HasRoomRental = hasRoomRental;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> AddAddress(int locationId, string address, string number, string? complement,
            string neighborhood, string city, string state, string zipCode, decimal basePrice, int maxContracts)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction(ActionNames.Register);
                }
                var hasFiscalAddress = await _coworkingAppService.HasFiscalAddressAsync(coworkingEntity.Id);
                if (hasFiscalAddress)
                {
                    TempData[ErrorMessages.TempDataKey] = "Não é possível adicionar outro endereço. Você já possui um endereço fiscal cadastrado.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var location = await _coworkingAppService.FindLocationAsync(locationId);
                if (location == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
                    var locations = await _coworkingAppService.GetActiveLocationsAsync();
                    bool hasRoomRental = await _coworkingAppService.HasRoomRentalAsync(coworkingEntity.Id);
                    ViewBag.Locations = locations;
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(locations);
                }
                if (basePrice < 0)
                {
                    TempData[ErrorMessages.TempDataKey] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
                    var locations = await _coworkingAppService.GetActiveLocationsAsync();
                    var hasRoomRental = await _coworkingAppService.HasRoomRentalAsync(coworkingEntity.Id);
                    ViewBag.Locations = locations;
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(locations);
                }
                var coworkingAddress = new CoworkingAddress
                {
                    CoworkingId = coworkingEntity.Id,
                    LocationId = locationId,
                    Address = address,
                    Number = number,
                    Complement = complement,
                    Neighborhood = neighborhood,
                    City = city,
                    State = state,
                    ZipCode = zipCode,
                    BasePrice = basePrice,
                    MaxContracts = maxContracts,
                    IsAvailable = false,
                    AddressStatus = AddressStatus.PendingApproval,
                    CurrentContracts = 0
                };
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Endereço adicionado");
                await _coworkingService.CreateAddressAsync(coworkingAddress);
                var fiscalAddressOfferService = await _coworkingAppService.GetServiceByCodeAsync(FiscalAddressOffer);
                if (fiscalAddressOfferService != null)
                {
                    var alreadyHasService = await _coworkingAppService.PartnerServiceExistsAsync(coworkingEntity.Id, fiscalAddressOfferService.Id);
                    if (!alreadyHasService)
                    {
                        _coworkingAppService.AddPartnerService(new PartnerService
                        {
                            CoworkingId = coworkingEntity.Id,
                            ServiceId = fiscalAddressOfferService.Id,
                            Percentage = 0,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _coworkingAppService.SaveChangesAsync();
                    }
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.AddressRegistered;
                return RedirectToAction(ActionNames.MyAddresses);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao cadastrar endereço: {ex.Message}";
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                bool hasRoomRental = false;
                if (coworkingEntity != null)
                {
                    try
                    {
                        hasRoomRental = await _context.CoworkingProducts
                            .AnyAsync(p => p.CoworkingId == coworkingEntity.Id &&
                                          p.ProductType == "RoomRental" &&
                                          p.IsActive);
                    }
                    catch
                    {
                        hasRoomRental = false;
                    }
                }
                var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                ViewBag.Locations = locations;
                ViewBag.HasRoomRental = hasRoomRental;
                return View(locations);
            }
        }
        [HttpGet]
        public async Task<IActionResult> EditAddress(int? id, CancellationToken cancellationToken = default)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = "ID do endereço inválido.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction(ActionNames.Register);
            }
            var address = await _coworkingAppService.GetAddressWithLocationAndCapacityRequestsAsync(id.Value, coworkingEntity.Id);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            var hasRoomRental = await _coworkingAppService.HasRoomRentalAsync(coworkingEntity.Id);
            var hasActiveContracts = await _coworkingAppService.HasActiveContractsForAddressAsync(address.Id);
            ViewBag.HasActiveContracts = hasActiveContracts;
            var locations = await _coworkingAppService.GetActiveLocationsAsync();
            ViewBag.Locations = locations;
            ViewBag.Address = address;
            ViewBag.HasRoomRental = hasRoomRental;
            return View(address);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAddress(int id, int locationId, string address, string number, string? complement,
            string neighborhood, string city, string state, string zipCode, int maxContracts, bool isAvailable)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction(ActionNames.Register);
                }
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.CapacityChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);
                if (existingAddress == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => c.CoworkingAddressId == existingAddress.Id && c.Status == ContractStatus.Active);
                if (hasActiveContracts)
                {
                    TempData[ErrorMessages.TempDataKey] = "Não é possível editar um endereço que possui contrato(s) ativo(s).";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var location = await _context.Locations.FindAsync(locationId);
                if (location == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    bool hasRoomRental = false;
                    try
                    {
                        hasRoomRental = await _context.CoworkingProducts
                            .AnyAsync(p => p.CoworkingId == coworkingEntity.Id &&
                                          p.ProductType == "RoomRental" &&
                                          p.IsActive);
                    }
                    catch
                    {
                        hasRoomRental = false;
                    }
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(existingAddress);
                }
                var basePriceStr = Request.Form["BasePrice"].ToString().Trim().Replace(",", ".");
                if (!decimal.TryParse(basePriceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var basePrice))
                    basePrice = existingAddress.BasePrice;
                if (basePrice < 0)
                {
                    TempData[ErrorMessages.TempDataKey] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    bool hasRoomRental = false;
                    try
                    {
                        hasRoomRental = await _context.CoworkingProducts
                            .AnyAsync(p => p.CoworkingId == coworkingEntity.Id &&
                                          p.ProductType == "RoomRental" &&
                                          p.IsActive);
                    }
                    catch
                    {
                        hasRoomRental = false;
                    }
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(existingAddress);
                }
                if (maxContracts < existingAddress.CurrentContracts)
                {
                    TempData[ErrorMessages.TempDataKey] = $"O máximo de contratos não pode ser menor que o número de contratos ativos ({existingAddress.CurrentContracts}).";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    bool hasRoomRental = false;
                    try
                    {
                        hasRoomRental = await _context.CoworkingProducts
                            .AnyAsync(p => p.CoworkingId == coworkingEntity.Id &&
                                          p.ProductType == "RoomRental" &&
                                          p.IsActive);
                    }
                    catch
                    {
                        hasRoomRental = false;
                    }
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(existingAddress);
                }
                existingAddress.LocationId = locationId;
                existingAddress.Address = address;
                existingAddress.Number = number;
                existingAddress.Complement = complement;
                existingAddress.Neighborhood = neighborhood;
                existingAddress.City = city;
                existingAddress.State = state;
                existingAddress.ZipCode = zipCode;
                existingAddress.BasePrice = basePrice;
                existingAddress.MaxContracts = maxContracts;
                existingAddress.IsAvailable = false;
                existingAddress.AddressStatus = AddressStatus.PendingApproval;
                existingAddress.UpdatedAt = DateTime.UtcNow;
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Endereço atualizado");
                await _coworkingService.UpdateAddressAsync(existingAddress);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.AddressUpdated;
                return RedirectToAction(ActionNames.MyAddresses);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao atualizar endereço: {ex.Message}";
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == id);
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                bool hasRoomRental = false;
                if (coworkingEntity != null)
                {
                    try
                    {
                        hasRoomRental = await _context.CoworkingProducts
                            .AnyAsync(p => p.CoworkingId == coworkingEntity.Id &&
                                          p.ProductType == "RoomRental" &&
                                          p.IsActive);
                    }
                    catch
                    {
                        hasRoomRental = false;
                    }
                }
                var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                ViewBag.Locations = locations;
                ViewBag.HasRoomRental = hasRoomRental;
                ViewBag.Address = existingAddress;
                return View(existingAddress);
            }
        }
        [HttpGet]
        public async Task<IActionResult> CardPrices(int? id, CancellationToken cancellationToken = default)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = "ID do endereço inválido.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction(ActionNames.Register);
            }
            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CardPriceChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == id.Value && a.CoworkingId == coworkingEntity.Id);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            return View(address);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CardPrices(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var address = await _context.CoworkingAddresses
                    .Include(a => a.CardPriceChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);
                if (address == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                if (address.CardPricesApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = "Já existe uma solicitação de alteração de preços no cartão pendente para este endereço. Aguarde a aprovação administrativa.";
                    return RedirectToAction(ActionNames.CardPrices, new { id });
                }
                decimal? cardPrice1x = decimal.TryParse(Request.Form["CardPrice1x"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1) ? v1 : (decimal?)null;
                decimal? cardPriceMonthly = decimal.TryParse(Request.Form["CardPriceMonthly"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2) ? v2 : (decimal?)null;
                decimal? cardPrice12x = decimal.TryParse(Request.Form["CardPrice12x"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v3) ? v3 : (decimal?)null;
                if (cardPrice1x.HasValue && cardPrice1x.Value < 0) cardPrice1x = null;
                if (cardPriceMonthly.HasValue && cardPriceMonthly.Value < 0) cardPriceMonthly = null;
                if (cardPrice12x.HasValue && cardPrice12x.Value < 0) cardPrice12x = null;
                var request = new CardPriceChangeRequest
                {
                    CoworkingAddressId = address.Id,
                    RequestedCardPrice1x = cardPrice1x,
                    RequestedCardPriceMonthly = cardPriceMonthly,
                    RequestedCardPrice12x = cardPrice12x,
                    PreviousCardPrice1x = address.CardPrice1x,
                    PreviousCardPriceMonthly = address.CardPriceMonthly,
                    PreviousCardPrice12x = address.CardPrice12x,
                    Status = CapacityApprovalStatus.Pending,
                    CreatedAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.CardPriceChangeRequests.Add(request);
                address.CardPricesApprovalStatus = CapacityApprovalStatus.Pending;
                address.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardPriceChangeRequestSent;
                return RedirectToAction(ActionNames.MyAddresses);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao salvar: {ex.Message}";
                var address = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .Include(a => a.CardPriceChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id);
                return address != null ? View(address) : RedirectToAction(ActionNames.MyAddresses);
            }
        }
        [HttpGet]
        public async Task<IActionResult> EditMaxContracts(int? id, CancellationToken cancellationToken = default)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = "ID do endereço inválido.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction(ActionNames.Register);
            }
            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.MaxContractsChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == id.Value && a.CoworkingId == coworkingEntity.Id);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            var hasActiveContracts = await _context.Contracts
                .AnyAsync(c => c.CoworkingAddressId == address.Id && c.Status == ContractStatus.Active);
            if (!hasActiveContracts)
            {
                TempData[ErrorMessages.TempDataKey] = "Esta funcionalidade está disponível apenas para endereços com contratos ativos. Use a edição normal do endereço.";
                return RedirectToAction(ActionNames.MyAddresses);
            }
            ViewBag.Address = address;
            return View(address);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMaxContracts(int id, int maxContracts, string? justification, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction(ActionNames.Register);
                }
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.MaxContractsChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);
                if (existingAddress == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => c.CoworkingAddressId == existingAddress.Id && c.Status == ContractStatus.Active);
                if (!hasActiveContracts)
                {
                    TempData[ErrorMessages.TempDataKey] = "Esta funcionalidade está disponível apenas para endereços com contratos ativos.";
                    return RedirectToAction(ActionNames.MyAddresses);
                }
                var minRequired = (int)Math.Ceiling(existingAddress.MaxContracts * 1.2m);
                if (maxContracts < minRequired)
                {
                    TempData[ErrorMessages.TempDataKey] = $"O novo valor de contratos máximos ({maxContracts}) deve ser pelo menos 20% maior que o valor atual ({existingAddress.MaxContracts}). O valor mínimo permitido é {minRequired}.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }
                if (maxContracts < existingAddress.CurrentContracts)
                {
                    TempData[ErrorMessages.TempDataKey] = $"O máximo de contratos não pode ser menor que o número de contratos ativos ({existingAddress.CurrentContracts}).";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }
                if (existingAddress.MaxContractsApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = "Já existe uma solicitação de alteração de quantidade máxima pendente para este endereço.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }
                if (string.IsNullOrWhiteSpace(justification))
                {
                    TempData[ErrorMessages.TempDataKey] = "A justificativa é obrigatória para solicitar alteração de quantidade máxima.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }
                var maxContractsRequest = new MaxContractsChangeRequest
                {
                    CoworkingAddressId = existingAddress.Id,
                    RequestedMaxContracts = maxContracts,
                    PreviousMaxContracts = existingAddress.MaxContracts,
                    Status = CapacityApprovalStatus.Pending,
                    Justification = justification.Trim(),
                    CreatedAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.MaxContractsChangeRequests.Add(maxContractsRequest);
                existingAddress.RequestedMaxContracts = maxContracts;
                existingAddress.MaxContractsApprovalStatus = CapacityApprovalStatus.Pending;
                existingAddress.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.MaxQuantityRequestSent;
                return RedirectToAction(ActionNames.MyAddresses);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao processar solicitação: {ex.Message}";
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == id);
                ViewBag.Address = existingAddress;
                return View(existingAddress);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestCapacityChange([FromBody] CapacityChangeRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null || request.AddressId <= 0 || request.RequestedCapacity <= 0)
                {
                    return Json(new { success = false, message = JsonMessage.DadosInvalidos });
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    return Json(new { success = false, message = JsonMessage.CoworkingNaoEncontrado });
                }
                var address = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.CoworkingId == coworkingEntity.Id);
                if (address == null)
                {
                    return Json(new { success = false, message = JsonMessage.EnderecoNaoEncontradoOuSemPermissao });
                }
                if (request.RequestedCapacity < address.CurrentContracts)
                {
                    return Json(new { success = false, message = string.Format(JsonMessage.CapacidadeSolicitadaMenorQueContratos, request.RequestedCapacity, address.CurrentContracts) });
                }
                if (address.CapacityApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    return Json(new { success = false, message = JsonMessage.JaExisteSolicitacaoAlteracaoCapacidadePendente });
                }
                var capacityRequest = new CapacityChangeRequest
                {
                    CoworkingAddressId = address.Id,
                    RequestedCapacity = request.RequestedCapacity,
                    PreviousCapacity = address.ApprovedFiscalCapacity,
                    Status = CapacityApprovalStatus.Pending,
                    Justification = request.Justification?.Trim(),
                    CreatedAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.CapacityChangeRequests.Add(capacityRequest);
                address.RequestedFiscalCapacity = request.RequestedCapacity;
                address.CapacityApprovalStatus = CapacityApprovalStatus.Pending;
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = JsonMessage.SolicitacaoAlteracaoCapacidadeEnviadaComSucesso });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao processar solicitação: {ex.Message}" });
            }
        }
        [HttpGet]
        public async Task<IActionResult> ActiveContracts(
            string? search = null,
            string? status = null,
            int? locationId = null,
            string? clientName = null,
            DateTime? startDateFrom = null,
            DateTime? startDateTo = null,
            int page = 1,
            int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var query = _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s.Payments)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, out var contractStatus))
            {
                query = query.Where(c => c.Status == contractStatus);
            }
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    (c.Client != null && (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(search) ||
                                         c.Client.ContactName != null && c.Client.ContactName.Contains(search) ||
                                         c.Client.CNPJ != null && c.Client.CNPJ.Contains(search) ||
                                         c.Client.CPF != null && c.Client.CPF.Contains(search))) ||
                    (c.CoworkingAddress != null && (c.CoworkingAddress.Address != null && c.CoworkingAddress.Address.Contains(search) ||
                                                    c.CoworkingAddress.City != null && c.CoworkingAddress.City.Contains(search) ||
                                                    c.CoworkingAddress.State != null && c.CoworkingAddress.State.Contains(search) ||
                                                    (c.CoworkingAddress.Location != null && (c.CoworkingAddress.Location.City != null && c.CoworkingAddress.Location.City.Contains(search) ||
                                                                                             c.CoworkingAddress.Location.State != null && c.CoworkingAddress.Location.State.Contains(search))))) ||
                    (c.ContractNumber != null && c.ContractNumber.Contains(search)));
            }
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(c => c.CoworkingAddress != null && c.CoworkingAddress.LocationId == locationId.Value);
            }
            if (!string.IsNullOrEmpty(clientName))
            {
                query = query.Where(c => c.Client != null &&
                    (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(clientName) ||
                     c.Client.ContactName != null && c.Client.ContactName.Contains(clientName)));
            }
            if (startDateFrom.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDateFrom.Value);
            }
            if (startDateTo.HasValue)
            {
                query = query.Where(c => c.StartDate <= startDateTo.Value);
            }
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var contracts = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.LocationId = locationId;
            ViewBag.ClientName = clientName;
            ViewBag.StartDateFrom = startDateFrom?.ToString("yyyy-MM-dd");
            ViewBag.StartDateTo = startDateTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            var locations = await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.City)
                .ThenBy(l => l.State)
                .ToListAsync();
            ViewBag.Locations = locations;
            return View(contracts);
        }
        [HttpGet]
        public async Task<IActionResult> ContractDetails(int id, int timelinePage = 1, int timelinePageSize = 5, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                return RedirectToAction(ActionNames.Dashboard);
            }
            var contract = await _context.Contracts
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(c => c.Subscription)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id && c.CoworkingAddress.CoworkingId == coworkingEntity.Id);
            if (contract == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Contrato não encontrado ou você não tem permissão para visualizá-lo.";
                return RedirectToAction(ActionNames.ActiveContracts);
            }
            var declarations = await _context.AddressDeclarations
                .Where(d => d.ContractId == contract.Id)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
            var payments = new List<Payment>();
            var historiesByPaymentId = new Dictionary<int, List<PaymentHistory>>();
            if (contract.Subscription != null)
            {
                payments = await _context.Payments
                    .Where(p => p.SubscriptionId == contract.Subscription.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
                var paymentIds = payments.Select(p => p.Id).ToList();
                if (paymentIds.Any())
                {
                    var paymentHistories = await _context.PaymentHistories
                        .Where(ph => paymentIds.Contains(ph.PaymentId))
                        .Include(ph => ph.ActionByUser)
                        .ToListAsync();
                    historiesByPaymentId = paymentHistories
                        .GroupBy(ph => ph.PaymentId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
            }
            var timeline = new List<object>();
            string ContractStatusPt(ContractStatus s) => s.ToDisplayString();
            string PaymentStatusPt(PaymentStatus s) => s.ToDisplayStringShort();
            string PaymentMethodPt(PaymentMethod m) => m.ToDisplayString();
            timeline.Add(new
            {
                Date = contract.CreatedAt,
                Type = "Criação",
                Title = "Contrato Criado",
                Description = $"Contrato {contract.ContractNumber} foi criado",
                Icon = "bi-file-earmark-plus",
                Color = "primary"
            });
            if (contract.UpdatedAt.HasValue && contract.UpdatedAt != contract.CreatedAt)
            {
                var statusPt = ContractStatusPt(contract.Status);
                timeline.Add(new
                {
                    Date = contract.UpdatedAt.Value,
                    Type = "Status",
                    Title = $"Status: {statusPt}",
                    Description = $"Status do contrato alterado para {statusPt}",
                    Icon = "bi-arrow-repeat",
                    Color = BootstrapBadgeCss.Info
                });
            }
            foreach (var payment in payments)
            {
                timeline.Add(new
                {
                    Date = payment.CreatedAt,
                    Type = "Pagamento",
                    Title = $"Pagamento de R$ {payment.Amount:N2}",
                    Description = $"Status: {PaymentStatusPt(payment.Status)} - Método: {PaymentMethodPt(payment.Method)}",
                    Icon = "bi-cash-coin",
                    Color = payment.Status == PaymentStatus.Paid ? BootstrapBadgeCss.Success : BootstrapBadgeCss.Warning,
                    Payment = payment
                });
                if (payment.PaidAt.HasValue)
                {
                    timeline.Add(new
                    {
                        Date = payment.PaidAt.Value,
                        Type = "Pagamento",
                        Title = "Pagamento Confirmado",
                        Description = $"Pagamento de R$ {payment.Amount:N2} foi confirmado",
                        Icon = "bi-check-circle",
                        Color = BootstrapBadgeCss.Success,
                        Payment = payment
                    });
                }
                if (historiesByPaymentId.ContainsKey(payment.Id))
                {
                    foreach (var history in historiesByPaymentId[payment.Id].OrderBy(h => h.ActionAt))
                    {
                        if (history.Action == PaymentHistoryActions.ContractCreated)
                            continue;
                        string title, description, icon, color;
                        if (history.Action == "Approved")
                        {
                            title = "Pagamento Aprovado";
                            description = $"Aprovado por {history.ActionByUser?.FullName ?? DefaultDisplayStrings.SystemAdminLabel}";
                            icon = "bi-check-circle";
                            color = BootstrapBadgeCss.Success;
                        }
                        else if (history.Action == "Cancelled")
                        {
                            title = "Pagamento Cancelado";
                            description = history.RejectionReason ?? "Cancelado (outro contrato ativado)";
                            icon = "bi-x-circle";
                            color = BootstrapBadgeCss.Secondary;
                        }
                        else if (history.Action == "ReceiptUploaded")
                        {
                            title = "Comprovante enviado";
                            description = "Cliente enviou o comprovante de pagamento. Aguardando validação.";
                            icon = "bi-file-earmark-arrow-up";
                            color = BootstrapBadgeCss.Info;
                        }
                        else if (history.Action == PaymentHistoryActions.Rejected)
                        {
                            title = "Pagamento Reprovado";
                            description = $"Reprovado por {history.ActionByUser?.FullName ?? DefaultDisplayStrings.SystemAdminLabel}: {history.RejectionReason}";
                            icon = "bi-x-circle";
                            color = BootstrapBadgeCss.Danger;
                        }
                        else
                            continue;
                        timeline.Add(new
                        {
                            Date = history.ActionAt,
                            Type = "Validação",
                            Title = title,
                            Description = description,
                            Icon = icon,
                            Color = color,
                            History = history
                        });
                    }
                }
            }
            foreach (var declaration in declarations)
            {
                timeline.Add(new
                {
                    Date = declaration.CreatedAt,
                    Type = "Documento",
                    Title = $"Declaração {declaration.DeclarationNumber}",
                    Description = $"Versão {declaration.Version} gerada",
                    Icon = "bi-file-earmark-pdf",
                    Color = BootstrapBadgeCss.Info,
                    Declaration = declaration
                });
            }
            timeline = timeline.OrderByDescending(t => ((dynamic)t).Date).ToList();
            var timelineTotal = timeline.Count;
            var timelineTotalPages = timelinePageSize > 0 ? (int)Math.Ceiling(timelineTotal / (double)timelinePageSize) : 1;
            if (timelinePage < 1) timelinePage = 1;
            if (timelinePage > timelineTotalPages) timelinePage = Math.Max(1, timelineTotalPages);
            var timelinePaged = timeline
                .Skip((timelinePage - 1) * timelinePageSize)
                .Take(timelinePageSize)
                .ToList();
            if (contract.Status == ContractStatus.PendingPayment && payments.Any())
            {
                var latestPaymentStatus = payments.OrderByDescending(p => p.CreatedAt).First().Status;
                ViewBag.ContractDisplayStatus = (latestPaymentStatus == PaymentStatus.AwaitingValidation || latestPaymentStatus == PaymentStatus.Processing) ? "Aguardando Validação" : "Aguardando Pagamento";
            }
            else
            {
                ViewBag.ContractDisplayStatus = ContractStatusPt(contract.Status);
            }
            ViewBag.Timeline = timelinePaged;
            ViewBag.TimelineTotalItems = timelineTotal;
            ViewBag.TimelineCurrentPage = timelinePage;
            ViewBag.TimelineTotalPages = timelineTotalPages;
            ViewBag.TimelinePageSize = timelinePageSize;
            ViewBag.Declarations = declarations;
            ViewBag.Payments = payments;
            ViewBag.PaymentHistories = historiesByPaymentId;
            return View(contract);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadDeclaration(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Dashboard);
            }
            var declaration = await _context.AddressDeclarations
                .Include(d => d.Contract)
                    .ThenInclude(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (declaration == null || declaration.Contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
            {
                return Forbid();
            }
            var pdfBytes = await _addressDeclarationService.GetDeclarationPdfAsync(id);
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return NotFound();
            }
            var fileName = $"{declaration.DeclarationNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadContractDocument(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Dashboard);
            }
            var document = await _context.Documents
                .Include(d => d.Contract)
                    .ThenInclude(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (document == null || document.Contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
            {
                return Forbid();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(document.FilePath);
            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound();
            }
            var fileName = !string.IsNullOrEmpty(document.FileName) ? document.FileName : Path.GetFileName(document.FilePath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"documento-{document.Id}.pdf";
            }
            var contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream";
            return File(fileBytes, contentType, fileName);
        }
        [HttpGet]
        public async Task<IActionResult> Reservations(
            string? search = null,
            string? status = null,
            int? addressId = null,
            DateTime? allocationDateFrom = null,
            DateTime? allocationDateTo = null,
            int page = 1,
            int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            if (!StringBooleanValue.IsTrue(enableCoworkingMenus))
                return RedirectToAction(ActionNames.Dashboard);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var addresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingEntity.Id)
                .OrderBy(a => a.City)
                .ThenBy(a => a.Neighborhood)
                .ToListAsync();
            ViewBag.Addresses = addresses;
            var reservationsQuery = _context.Reservations
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                reservationsQuery = reservationsQuery.Where(r =>
                    r.Client.User.FullName.Contains(search) ||
                    (r.Client.ContactEmail != null && r.Client.ContactEmail.Contains(search)) ||
                    (r.Client.ContactPhone != null && r.Client.ContactPhone.Contains(search)) ||
                    (r.Client.CNPJ != null && r.Client.CNPJ.Contains(search)) ||
                    (r.Client.CPF != null && r.Client.CPF.Contains(search)));
            }
            if (addressId.HasValue)
            {
                reservationsQuery = reservationsQuery.Where(r => r.CoworkingAddressId == addressId.Value);
            }
            if (allocationDateFrom.HasValue)
            {
                reservationsQuery = reservationsQuery.Where(r => r.ReservationDate >= allocationDateFrom.Value);
            }
            if (allocationDateTo.HasValue)
            {
                reservationsQuery = reservationsQuery.Where(r => r.ReservationDate <= allocationDateTo.Value);
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Confirmed" || status == "Completed" || status == "Cancelled")
                {
                    var resStatus = status == "Confirmed" ? ReservationStatus.Confirmed : status == "Completed" ? ReservationStatus.Completed : ReservationStatus.Cancelled;
                    reservationsQuery = reservationsQuery.Where(r => r.Status == resStatus);
                }
                else
                {
                    reservationsQuery = reservationsQuery.Where(r => false);
                }
            }
            var confirmedReservations = await reservationsQuery
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();
            List<Interest> interests = new List<Interest>();
            try
            {
                var interestsQuery = _context.Interests
                    .Include(i => i.CoworkingAddress)
                    .Include(i => i.ContactedByUser)
                    .Where(i => i.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                    .AsQueryable();
                if (!string.IsNullOrEmpty(search))
                {
                    interestsQuery = interestsQuery.Where(i =>
                        i.Name.Contains(search) ||
                        i.LastName.Contains(search) ||
                        i.Email.Contains(search) ||
                        i.Phone.Contains(search));
                }
                if (addressId.HasValue)
                {
                    interestsQuery = interestsQuery.Where(i => i.CoworkingAddressId == addressId.Value);
                }
                if (allocationDateFrom.HasValue)
                {
                    interestsQuery = interestsQuery.Where(i => i.AllocationDate.HasValue && i.AllocationDate >= allocationDateFrom.Value);
                }
                if (allocationDateTo.HasValue)
                {
                    interestsQuery = interestsQuery.Where(i => i.AllocationDate.HasValue && i.AllocationDate <= allocationDateTo.Value);
                }
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Pending" || status == "Contacted" || status == "Converted" || status == "Rejected")
                    {
                        var intStatus = status == "Pending" ? InterestStatus.Pending : status == "Contacted" ? InterestStatus.Contacted : status == "Converted" ? InterestStatus.Converted : InterestStatus.Rejected;
                        interestsQuery = interestsQuery.Where(i => i.Status == intStatus);
                    }
                    else
                    {
                        interestsQuery = interestsQuery.Where(i => false);
                    }
                }
                interests = await interestsQuery
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
            }
            var fromRes = _mapper.Map<List<ReservationOrInterestItemDto>>(confirmedReservations);
            var fromInt = _mapper.Map<List<ReservationOrInterestItemDto>>(interests);
            var allReservations = fromRes.Concat(fromInt)
                .OrderByDescending(x => x.ReservationDate)
                .ThenByDescending(x => x.CreatedAt)
                .ToList();
            var totalItems = allReservations.Count;
            var totalPages = totalItems > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            if (page > totalPages && totalPages > 0)
            {
                page = totalPages;
            }
            var pagedReservations = allReservations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.AddressId = addressId;
            ViewBag.AllocationDateFrom = allocationDateFrom?.ToString("yyyy-MM-dd");
            ViewBag.AllocationDateTo = allocationDateTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Reservations = pagedReservations;
            return View(interests);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReservationStatus(int id, string status, string? notes = null, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                return RedirectToAction(ActionNames.Reservations);
            }
            var interest = await _context.Interests
                .Include(i => i.CoworkingAddress)
                .FirstOrDefaultAsync(i => i.Id == id && i.CoworkingAddress.CoworkingId == coworkingEntity.Id);
            if (interest == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Reserva não encontrada.";
                return RedirectToAction(ActionNames.Reservations);
            }
            if (!Enum.TryParse<InterestStatus>(status, ignoreCase: true, out var statusEnum))
            {
                TempData[ErrorMessages.TempDataKey] = "Status inválido.";
                return RedirectToAction(ActionNames.Reservations);
            }
            interest.Status = statusEnum;
            interest.Notes = notes;
            interest.UpdatedAt = DateTime.UtcNow;
            if (statusEnum == InterestStatus.Contacted)
            {
                interest.ContactedByUserId = userId;
                interest.ContactedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.ReservationStatusUpdated;
            return RedirectToAction(ActionNames.Reservations);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservationAttended(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null) { TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado."; return RedirectToAction(ActionNames.Reservations); }
            var r = await _context.Reservations
                .Include(x => x.CoworkingAddress)
                .FirstOrDefaultAsync(x => x.Id == id && x.CoworkingAddress != null && x.CoworkingAddress.CoworkingId == coworkingEntity.Id);
            if (r == null) { TempData[ErrorMessages.TempDataKey] = "Reserva não encontrada."; return RedirectToAction(ActionNames.Reservations); }
            if (r.Status == ReservationStatus.Cancelled) { TempData[ErrorMessages.TempDataKey] = "Reserva cancelada."; return RedirectToAction(ActionNames.Reservations); }
            if (r.Status == ReservationStatus.Completed) { TempData[SuccessMessage.TempDataKey] = SuccessMessage.ReservationCompletedCreditsTransferred; return RedirectToAction(ActionNames.Reservations); }
            if (r.CoworkingConfirmedAt == null)
            {
                r.CoworkingConfirmedAt = DateTime.UtcNow;
                r.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            await _reservationTransferService.ExecuteTransferIfBothConfirmedAsync(r);
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.AttendanceConfirmed;
            return RedirectToAction(ActionNames.Reservations);
        }
        [HttpGet]
        public async Task<IActionResult> RoomRevenueReport(DateTime? dateFrom, DateTime? dateTo, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) return RedirectToAction(ActionNames.Register);
            var q = _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress != null && r.CoworkingAddress.CoworkingId == cow.Id && r.Status != ReservationStatus.Cancelled);
            if (dateFrom.HasValue)
                q = q.Where(r => r.ReservationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(r => r.ReservationDate <= dateTo.Value.Date.AddDays(1));
            var list = await q.ToListAsync();
            var listIds = list.Select(r => r.Id).ToList();
            var commissions = new List<ReservationCommission>();
            var transfers = new List<Transfer>();
            try
            {
                if (listIds.Any())
                {
                    commissions = await _context.ReservationCommissions
                        .Where(rc => listIds.Contains(rc.ReservationId))
                        .ToListAsync();
                    transfers = await _context.Transfers
                        .Where(t => t.ReservationId.HasValue && listIds.Contains(t.ReservationId.Value))
                        .ToListAsync();
                }
            }
            catch
            {
                commissions = new List<ReservationCommission>();
                transfers = new List<Transfer>();
            }
            string ClientDisplayName(Reservation r) => r.Client != null
                ? (r.Client.RazaoSocial ?? r.Client.NomeFantasia ?? r.Client.ContactName ?? DefaultDisplayStrings.EmptyPlaceholder)
                : "Cliente não identificado";
            var total = list.Sum(r => r.TotalValue);
            var byRoomType = new[]
            {
                ("Shared", "Sala compartilhada", list.Where(r => r.RoomType == "Shared").ToList()),
                ("Private", "Sala privativa", list.Where(r => r.RoomType == "Private").ToList()),
                ("Auditorium", "Auditório", list.Where(r => r.RoomType == "Auditorium").ToList())
            }.Select(x =>
            {
                var allDetails = x.Item3.Select(r => new Dictionary<string, object>
                {
                    ["DateFormatted"] = r.ReservationDate.ToString(DateFormatConstants.DisplayDateOnly),
                    ["ClientName"] = ClientDisplayName(r),
                    ["Value"] = r.TotalValue,
                }).ToList();
                var totalDetails = allDetails.Count;
                var totalPages = totalDetails > 0 ? (int)Math.Ceiling(totalDetails / (double)pageSize) : 1;
                if (page > totalPages && totalPages > 0) page = totalPages;
                var pagedDetails = allDetails
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                return new Dictionary<string, object>
                {
                    ["RoomTypeKey"] = x.Item1,
                    ["Label"] = x.Item2,
                    ["Revenue"] = (decimal)x.Item3.Sum(r => r.TotalValue),
                    ["Count"] = x.Item3.Count,
                    ["Details"] = pagedDetails,
                    ["TotalDetails"] = totalDetails,
                    ["CurrentPage"] = page,
                    ["TotalPages"] = totalPages
                };
            }).ToList();
            var byClient = list
                .GroupBy(r => r.ClientId)
                .Select(g =>
                {
                    var firstReservation = g.FirstOrDefault();
                    if (firstReservation == null) return null;
                    var allDetails = g.Select(r => new Dictionary<string, object>
                    {
                        ["DateFormatted"] = r.ReservationDate.ToString(DateFormatConstants.DisplayDateOnly),
                        ["RoomTypeLabel"] = RoomTypes.GetDisplayName(r.RoomType ?? "Shared"),
                        ["Value"] = r.TotalValue,
                    }).ToList();
                    var totalDetails = allDetails.Count;
                    var totalPages = totalDetails > 0 ? (int)Math.Ceiling(totalDetails / (double)pageSize) : 1;
                    var clientPage = page;
                    if (clientPage > totalPages && totalPages > 0) clientPage = totalPages;
                    var pagedDetails = allDetails
                        .Skip((clientPage - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                    return new Dictionary<string, object>
                    {
                        ["ClientId"] = g.Key,
                        ["ClientName"] = ClientDisplayName(firstReservation),
                        ["Count"] = g.Count(),
                        ["Revenue"] = (decimal)g.Sum(r => r.TotalValue),
                        ["Details"] = pagedDetails,
                        ["TotalDetails"] = totalDetails,
                        ["CurrentPage"] = clientPage,
                        ["TotalPages"] = totalPages
                    };
                })
                .Where(x => x != null)
                .Cast<Dictionary<string, object>>()
                .OrderByDescending(x => (decimal)x["Revenue"])
                .ToList();
            ViewBag.TotalRevenue = total;
            ViewBag.ByRoomType = byRoomType;
            ViewBag.ByClient = byClient;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.CoworkingName = cow.Name;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Commissions(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var commissions = await _context.Commissions
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                .Where(c => c.CoworkingId == coworkingEntity.Id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            var transfers = await _context.Transfers
                .Where(t => t.CoworkingId == coworkingEntity.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            var commissionTransferMap = new Dictionary<int, Transfer?>();
            foreach (var transfer in transfers)
            {
                if (!string.IsNullOrEmpty(transfer.Description) && transfer.Description.Contains("CommissionIds:"))
                {
                    var idsPart = transfer.Description.Split("CommissionIds:")[1];
                    if (!string.IsNullOrEmpty(idsPart))
                    {
                        var ids = idsPart.Split(',');
                        foreach (var idStr in ids)
                        {
                            if (int.TryParse(idStr.Trim(), out var commissionId))
                            {
                                if (commissions.Any(c => c.Id == commissionId))
                                {
                                    commissionTransferMap[commissionId] = transfer;
                                }
                            }
                        }
                    }
                }
            }
            var transferredCommissions = commissions
                .Where(c => c.IsTransferred && c.TransferredAt.HasValue && !commissionTransferMap.ContainsKey(c.Id))
                .GroupBy(c => new
                {
                    Date = c.TransferredAt.Value.Date,
                    Hour = c.TransferredAt.Value.Hour
                })
                .ToList();
            foreach (var group in transferredCommissions)
            {
                var groupCommissions = group.ToList();
                var totalCreditado = groupCommissions.Sum(c => c.TotalPaymentAmount - c.Amount);
                var matchingTransfer = transfers
                    .Where(t =>
                        !commissionTransferMap.Values.Contains(t) &&
                        t.CreatedAt.Date == group.Key.Date &&
                        Math.Abs(t.CreatedAt.Hour - group.Key.Hour) <= 1 &&
                        Math.Abs(t.Amount - totalCreditado) < 0.01m)
                    .OrderBy(t => Math.Abs((t.CreatedAt - groupCommissions.First().TransferredAt.Value).TotalMinutes))
                    .FirstOrDefault();
                if (matchingTransfer == null)
                {
                    matchingTransfer = transfers
                        .Where(t =>
                            !commissionTransferMap.Values.Contains(t) &&
                            t.CreatedAt.Date == group.Key.Date &&
                            Math.Abs(t.CreatedAt.Hour - group.Key.Hour) <= 1 &&
                            t.Amount >= totalCreditado * 0.95m &&
                            t.Amount <= totalCreditado * 1.05m)
                        .OrderBy(t => Math.Abs((t.CreatedAt - groupCommissions.First().TransferredAt.Value).TotalMinutes))
                        .FirstOrDefault();
                }
                foreach (var commission in groupCommissions)
                {
                    if (!commissionTransferMap.ContainsKey(commission.Id))
                    {
                        commissionTransferMap[commission.Id] = matchingTransfer;
                    }
                }
            }
            var paymentIds = commissions.Select(c => c.PaymentId).Distinct().ToList();
            var paymentIdsWithEmitidaNf = await _context.FiscalNotes
                .Where(fn => fn.PaymentId != null && paymentIds.Contains(fn.PaymentId.Value)
                    && fn.Type == FiscalNoteType.Servico && (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada))
                .Select(fn => fn.PaymentId!.Value)
                .Distinct()
                .ToListAsync();
            var paymentIdsWithEmitidaNfSet = new HashSet<int>(paymentIdsWithEmitidaNf);
            var transferIdsAllNfEmitida = new HashSet<int>();
            foreach (var transfer in transfers.Where(t => t.Status == TransferStatus.AwaitingInvoice))
            {
                var commissionIdsInTransfer = commissionTransferMap
                    .Where(kv => kv.Value?.Id == transfer.Id)
                    .Select(kv => kv.Key)
                    .ToList();
                var relatedPayments = commissions
                    .Where(c => commissionIdsInTransfer.Contains(c.Id))
                    .Select(c => c.PaymentId)
                    .ToList();
                if (relatedPayments.Count > 0 && relatedPayments.All(pid => paymentIdsWithEmitidaNfSet.Contains(pid)))
                    transferIdsAllNfEmitida.Add(transfer.Id);
            }
            ViewBag.CommissionTransferMap = commissionTransferMap;
            ViewBag.TransferIdsAllNfEmitida = transferIdsAllNfEmitida;
            return View(commissions);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTransfer(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var hasPixKey = !string.IsNullOrWhiteSpace(coworkingEntity.BankPixKey);
            var hasContaCorrente = !string.IsNullOrWhiteSpace(coworkingEntity.BankName) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAgency) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAccount) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAccountType);
            var hasBankData = hasPixKey || hasContaCorrente;
            if (!hasBankData)
            {
                TempData[ErrorMessages.TempDataKey] = "Para solicitar repasse, é necessário cadastrar os dados bancários: Chave PIX ou Conta Corrente (Banco, Agência, Conta e Tipo de conta). <a href=\"" + Url.Action(ActionNames.Profile, ControllerNames.Coworking) + "\" class=\"alert-link\">Clique aqui para cadastrar seus dados bancários</a>.";
                return RedirectToAction(ActionNames.Dashboard);
            }
            var pendingCommissions = await _commissionService.GetPendingCommissionsAsync(coworkingEntity.Id);
            if (pendingCommissions.Count == 0)
            {
                TempData[ErrorMessages.TempDataKey] = "Não há comissões pendentes para transferência";
                return RedirectToAction(ActionNames.Commissions);
            }
            try
            {
                var commissionIds = pendingCommissions.Select(c => c.Id).ToList();
                await _commissionService.CreateTransferAsync(coworkingEntity.Id, commissionIds);
                var totalCreditado = pendingCommissions.Sum(c => c.TotalPaymentAmount - c.Amount);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PayoutRequested(totalCreditado);
                return RedirectToAction(ActionNames.Commissions);
            }
            catch (InvalidOperationException ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.Dashboard);
            }
            catch (Exception)
            {
                TempData[ErrorMessages.TempDataKey] = "Ocorreu um erro ao processar a solicitação de repasse. Por favor, tente novamente.";
                return RedirectToAction(ActionNames.Dashboard);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Profile(int partnersPage = 1, int partnersPageSize = 10, int photosPage = 1, int photosPageSize = 6, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var maxPhotoMb = 5;
            var mbStr = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mbStr, out var n) && n > 0) maxPhotoMb = n;
            ViewBag.MaxPhotoSizeMB = maxPhotoMb;
            var coworkingWithDetails = await _context.Coworkings
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == coworkingEntity.Id);
            var user = await _context.Users.FindAsync(userId);
            ViewBag.UserFullName = user?.FullName ?? coworkingEntity.Name;
            if (coworkingWithDetails != null)
            {
                try
                {
                    coworkingWithDetails.Products = await _context.CoworkingProducts
                        .Where(p => p.CoworkingId == coworkingWithDetails.Id)
                        .ToListAsync();
                }
                catch
                {
                    coworkingWithDetails.Products = new List<Domain.Entities.CoworkingProduct>();
                }
                var partnersQuery = _context.CoworkingPartners
                    .Where(p => p.CoworkingId == coworkingWithDetails.Id)
                    .OrderByDescending(p => p.IsPrincipal)
                    .ThenBy(p => p.Name);
                var totalPartners = await partnersQuery.CountAsync();
                var totalPartnersPages = totalPartners > 0 ? (int)Math.Ceiling(totalPartners / (double)partnersPageSize) : 1;
                if (partnersPage > totalPartnersPages && totalPartnersPages > 0)
                {
                    partnersPage = totalPartnersPages;
                }
                var pagedPartners = await partnersQuery
                    .Skip((partnersPage - 1) * partnersPageSize)
                    .Take(partnersPageSize)
                    .ToListAsync();
                coworkingWithDetails.Partners = pagedPartners;
                ViewBag.PartnersTotalItems = totalPartners;
                ViewBag.PartnersCurrentPage = partnersPage;
                ViewBag.PartnersTotalPages = totalPartnersPages;
                ViewBag.PartnersPageSize = partnersPageSize;
                var photosQuery = _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingWithDetails.Id)
                    .OrderByDescending(p => p.IsMain)
                    .ThenBy(p => p.DisplayOrder)
                    .ThenBy(p => p.CreatedAt);
                var totalPhotos = await photosQuery.CountAsync();
                var totalPhotosPages = totalPhotos > 0 ? (int)Math.Ceiling(totalPhotos / (double)photosPageSize) : 1;
                if (photosPage > totalPhotosPages && totalPhotosPages > 0)
                {
                    photosPage = totalPhotosPages;
                }
                var pagedPhotos = await photosQuery
                    .Skip((photosPage - 1) * photosPageSize)
                    .Take(photosPageSize)
                    .ToListAsync();
                coworkingWithDetails.Photos = pagedPhotos;
                ViewBag.PhotosTotalItems = totalPhotos;
                ViewBag.PhotosCurrentPage = photosPage;
                ViewBag.PhotosTotalPages = totalPhotosPages;
                ViewBag.PhotosPageSize = photosPageSize;
            }
            else
            {
                coworkingEntity.Partners = new List<Domain.Entities.CoworkingPartner>();
                ViewBag.PartnersTotalItems = 0;
                ViewBag.PartnersCurrentPage = 1;
                ViewBag.PartnersTotalPages = 1;
                ViewBag.PartnersPageSize = partnersPageSize;
                coworkingEntity.Photos = new List<Domain.Entities.CoworkingPhoto>();
                ViewBag.PhotosTotalItems = 0;
                ViewBag.PhotosCurrentPage = 1;
                ViewBag.PhotosTotalPages = 1;
                ViewBag.PhotosPageSize = photosPageSize;
            }
            var operatingHours = await _context.CoworkingOperatingHours
                .Where(h => h.CoworkingId == (coworkingWithDetails ?? coworkingEntity).Id)
                .OrderBy(h => h.DayOfWeek)
                .ToListAsync();
            ViewBag.OperatingHours = operatingHours;
            var blockedDates = await _context.BlockedDates
                .Where(b => b.CoworkingId == (coworkingWithDetails ?? coworkingEntity).Id)
                .OrderByDescending(b => b.BlockedDateValue)
                .ToListAsync();
            ViewBag.BlockedDates = blockedDates;
            ViewBag.UserEmail = user?.Email ?? User.FindFirstValue(ClaimTypes.Email);
            var alerts = await BuildAdministrativeAlertsAsync(coworkingWithDetails ?? coworkingEntity);
            ViewBag.AdministrativeAlerts = alerts;
            ViewBag.PendenciasTotal = alerts.Count;
            return View(coworkingWithDetails ?? coworkingEntity);
        }
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Coworking coworking, CancellationToken cancellationToken = default)
        {
            try
            {
                var isAjaxRequest = Request.Headers[HttpHeaderNames.XRequestedWith].ToString() == HttpHeaderNames.XRequestedWithValue;
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var existingCoworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = existingCoworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Register);
                }
                var activeTab = Request.Form["activeTab"].ToString();
                var oldName = coworkingEntity.Name;
                var oldCNPJ = coworkingEntity.CNPJ;
                var oldTradeName = coworkingEntity.TradeName;
                switch (activeTab)
                {
                    case "basic":
                        if (string.IsNullOrWhiteSpace(coworking.Name))
                        {
                            var errorMessage = "Nome do Coworking é obrigatório.";
                            TempData[ErrorMessages.TempDataKey] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction(ActionNames.Profile);
                        }
                        if (string.IsNullOrWhiteSpace(coworking.CNPJ))
                        {
                            var errorMessage = "CNPJ é obrigatório.";
                            TempData[ErrorMessages.TempDataKey] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction(ActionNames.Profile);
                        }
                        var cnpjClean = coworking.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                        if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                        {
                            var errorMessage = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
                            TempData[ErrorMessages.TempDataKey] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction(ActionNames.Profile);
                        }
                        var existingCNPJ = await _context.Coworkings
                            .Where(c => c.Id != coworkingEntity.Id &&
                                c.CNPJ != null &&
                                c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                            .FirstOrDefaultAsync();
                        if (existingCNPJ != null)
                        {
                            var errorMessage = $"O CNPJ {coworking.CNPJ} já está cadastrado para outro coworking.";
                            TempData[ErrorMessages.TempDataKey] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction(ActionNames.Profile);
                        }
                        coworking.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                        coworkingEntity.Name = coworking.Name;
                        coworkingEntity.CNPJ = coworking.CNPJ;
                        coworkingEntity.TradeName = coworking.TradeName;
                        coworkingEntity.Description = coworking.Description;
                        break;
                    case "contact":
                        coworkingEntity.ContactPhone = coworking.ContactPhone;
                        coworkingEntity.SecondaryPhone = coworking.SecondaryPhone;
                        coworkingEntity.SecondaryEmail = coworking.SecondaryEmail;
                        break;
                    case "address":
                        coworkingEntity.AddressZipCode = coworking.AddressZipCode;
                        coworkingEntity.Address = coworking.Address;
                        coworkingEntity.AddressNumber = coworking.AddressNumber;
                        coworkingEntity.AddressComplement = coworking.AddressComplement;
                        coworkingEntity.AddressNeighborhood = coworking.AddressNeighborhood;
                        coworkingEntity.AddressCity = coworking.AddressCity;
                        coworkingEntity.AddressState = coworking.AddressState;
                        break;
                    case "bank":
                        string? ToNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                        coworkingEntity.BankName = ToNull(coworking.BankName);
                        coworkingEntity.BankAgency = ToNull(coworking.BankAgency);
                        coworkingEntity.BankAccount = ToNull(coworking.BankAccount);
                        coworkingEntity.BankAccountType = ToNull(coworking.BankAccountType);
                        coworkingEntity.BankPixKey = ToNull(coworking.BankPixKey);
                        break;
                    case "social":
                        coworkingEntity.Website = coworking.Website;
                        coworkingEntity.Instagram = coworking.Instagram;
                        coworkingEntity.Facebook = coworking.Facebook;
                        coworkingEntity.LinkedIn = coworking.LinkedIn;
                        break;
                    default:
                        coworkingEntity.Name = coworking.Name;
                        coworkingEntity.CNPJ = coworking.CNPJ;
                        coworkingEntity.TradeName = coworking.TradeName;
                        coworkingEntity.Description = coworking.Description;
                        coworkingEntity.ContactEmail = coworking.ContactEmail;
                        coworkingEntity.ContactPhone = coworking.ContactPhone;
                        coworkingEntity.SecondaryPhone = coworking.SecondaryPhone;
                        coworkingEntity.SecondaryEmail = coworking.SecondaryEmail;
                        coworkingEntity.Website = coworking.Website;
                        coworkingEntity.Instagram = coworking.Instagram;
                        coworkingEntity.Facebook = coworking.Facebook;
                        coworkingEntity.LinkedIn = coworking.LinkedIn;
                        coworkingEntity.Address = coworking.Address;
                        coworkingEntity.AddressNumber = coworking.AddressNumber;
                        coworkingEntity.AddressComplement = coworking.AddressComplement;
                        coworkingEntity.AddressNeighborhood = coworking.AddressNeighborhood;
                        coworkingEntity.AddressCity = coworking.AddressCity;
                        coworkingEntity.AddressState = coworking.AddressState;
                        coworkingEntity.AddressZipCode = coworking.AddressZipCode;
                        coworkingEntity.BankName = coworking.BankName;
                        coworkingEntity.BankAgency = coworking.BankAgency;
                        coworkingEntity.BankAccount = coworking.BankAccount;
                        coworkingEntity.BankAccountType = coworking.BankAccountType;
                        coworkingEntity.BankPixKey = coworking.BankPixKey;
                        break;
                }
                coworkingEntity.UpdatedAt = DateTime.UtcNow;
                string changeReason = activeTab switch
                {
                    "basic" => "Dados básicos atualizados",
                    "contact" => "Dados de contato atualizados",
                    "address" => "Endereço atualizado",
                    "bank" => "Dados bancários atualizados",
                    "social" => "Redes sociais atualizadas",
                    _ => "Perfil atualizado"
                };
                bool shouldReturnToReview = activeTab switch
                {
                    "contact" => false,
                    "social" => false,
                    "basic" => coworkingEntity.Name != oldName || coworkingEntity.CNPJ != oldCNPJ || coworkingEntity.TradeName != oldTradeName,
                    _ => true
                };
                if (shouldReturnToReview)
                    await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, changeReason);
                await _coworkingService.UpdateAsync(coworkingEntity);
                if (activeTab == "basic" && !string.IsNullOrWhiteSpace(coworking.Name))
                {
                    var userToUpdate = await _context.Users.FindAsync(userId);
                    if (userToUpdate != null)
                    {
                        userToUpdate.FullName = coworking.Name;
                        userToUpdate.UpdatedAt = DateTime.UtcNow;
                        _context.Users.Update(userToUpdate);
                        await _context.SaveChangesAsync();
                        await RefreshUserPrincipalAsync(userToUpdate);
                    }
                }
                var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                if (showCoworkingMenus)
                {
                    var operatingHoursCount = await _context.CoworkingOperatingHours
                        .Where(h => h.CoworkingId == coworkingEntity.Id && h.IsActive)
                        .CountAsync();
                    if (operatingHoursCount == 0)
                    {
                        TempData[TempDataKeys.Warning] = WarningMessages.OperatingHoursRequiredConfigureTab;
                        TempData[TempDataKeys.ActiveTab] = "hours";
                    }
                }
                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData[TempDataKeys.ActiveTab] = activeTab;
                }
                if (isAjaxRequest)
                {
                    return Json(new { success = true, message = JsonMessage.PerfilAtualizadoComSucesso });
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ProfileUpdated;
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                var isAjaxRequestCatch = Request.Headers[HttpHeaderNames.XRequestedWith].ToString() == HttpHeaderNames.XRequestedWithValue;
                var errorMessage = $"Erro ao atualizar dados: {ex.Message}";
                TempData[ErrorMessages.TempDataKey] = errorMessage;
                var activeTab = Request.Form["activeTab"].ToString();
                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData[TempDataKeys.ActiveTab] = activeTab;
                }
                if (isAjaxRequestCatch)
                {
                    return Json(new { success = false, error = errorMessage });
                }
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProducts(List<string>? productTypes, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                    {
                        return Json(new { success = false, message = JsonMessage.CoworkingNaoEncontrado });
                    }
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var existingProducts = await _context.CoworkingProducts
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .ToListAsync();
                _context.CoworkingProducts.RemoveRange(existingProducts);
                if (productTypes != null && productTypes.Any())
                {
                    var newProducts = productTypes.Select(pt => new KlyonHubPlatform.Domain.Entities.CoworkingProduct
                    {
                        CoworkingId = coworkingEntity.Id,
                        ProductType = pt,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();
                    _context.CoworkingProducts.AddRange(newProducts);
                }
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Produtos atualizados");
                await _context.SaveChangesAsync();
                if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                {
                    var updatedProducts = await _context.CoworkingProducts
                        .Where(p => p.CoworkingId == coworkingEntity.Id && p.IsActive)
                        .Select(p => p.ProductType)
                        .ToListAsync();
                    return Json(new
                    {
                        success = true,
                        message = JsonMessage.ProdutosAtualizadosComSucesso,
                        hasFiscalAddress = updatedProducts.Contains("FiscalAddress"),
                        hasRoomRental = updatedProducts.Contains("RoomRental")
                    });
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ProductsUpdated;
                return RedirectToAction(ActionNames.Products);
            }
            catch (Exception ex)
            {
                if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                {
                    return Json(new { success = false, message = $"Erro ao atualizar produtos: {ex.Message}" });
                }
                TempData[ErrorMessages.TempDataKey] = $"Erro ao atualizar produtos: {ex.Message}";
                return RedirectToAction(ActionNames.Products);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Products(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var products = await _context.CoworkingProducts
                .Where(p => p.CoworkingId == cow.Id && p.IsActive)
                .Select(p => p.ProductType)
                .ToListAsync();
            var addresses = await _context.CoworkingAddresses
                .Include(a => a.Rooms)
                .Where(a => a.CoworkingId == cow.Id)
                .OrderBy(a => a.City).ThenBy(a => a.Address)
                .ToListAsync();
            ViewBag.CurrentProducts = products;
            ViewBag.Addresses = addresses;
            ViewBag.Coworking = cow;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> AddressRooms(int addressId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) return RedirectToAction(ActionNames.Register);
            var address = await _context.CoworkingAddresses
                .Include(a => a.Rooms)
                .ThenInclude(r => r.Photos)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado.";
                return RedirectToAction(ActionNames.Products);
            }
            ViewBag.Address = address;
            ViewBag.RoomTypes = RoomTypes.All;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoom(int addressId, string RoomType, string Name, int Capacity, int CreditsPerHour, IFormFile[]? RoomPhotos, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) { TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado."; return RedirectToAction(ActionNames.Products); }
            var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null) { TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado."; return RedirectToAction(ActionNames.Products); }
            if (!RoomTypes.All.Contains(RoomType)) { TempData[ErrorMessages.TempDataKey] = "Tipo de sala inválido."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            if (Capacity < 1 || CreditsPerHour < 0) { TempData[ErrorMessages.TempDataKey] = "Capacidade deve ser >= 1 e créditos >= 0."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            var room = new CoworkingRoom
            {
                CoworkingAddressId = addressId,
                RoomType = RoomType,
                Name = Name ?? "Sala",
                Capacity = Capacity,
                CreditsPerHour = CreditsPerHour,
                IsActive = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.CoworkingRooms.Add(room);
            await _context.SaveChangesAsync();
            var maxPhotoBytes = await GetMaxPhotoSizeBytesAsync();
            var storage = await _storageFactory.GetStorageServiceAsync();
            var folder = $"coworkings/{cow.Id}/rooms/{room.Id}";
            if (RoomPhotos != null && RoomPhotos.Length > 0)
            {
                int order = 0;
                foreach (var f in RoomPhotos.Take(10))
                {
                    if (f == null || f.Length == 0 || f.Length > maxPhotoBytes) continue;
                    var ext = Path.GetExtension(f.FileName); if (string.IsNullOrEmpty(ext) || !new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext.ToLower())) ext = ".jpg";
                    using var ms = new MemoryStream();
                    await f.CopyToAsync(ms);
                    var path = await storage.SaveFileAsync(ms.ToArray(), $"photo_{order}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}", folder);
                    _context.CoworkingRoomPhotos.Add(new CoworkingRoomPhoto { CoworkingRoomId = room.Id, FilePath = path, IsMain = order == 0, DisplayOrder = order, CreatedAt = DateTime.UtcNow });
                    order++;
                }
                await _context.SaveChangesAsync();
            }
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.RoomAdded;
            return RedirectToAction(ActionNames.AddressRooms, new { addressId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoom(int roomId, int addressId, string RoomType, string Name, int Capacity, int CreditsPerHour, bool IsActive, IFormFile[]? RoomPhotos, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null) { TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado."; return RedirectToAction(ActionNames.Products); }
            var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null) { TempData[ErrorMessages.TempDataKey] = "Endereço não encontrado."; return RedirectToAction(ActionNames.Products); }
            var room = await _context.CoworkingRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.CoworkingAddressId == addressId);
            if (room == null) { TempData[ErrorMessages.TempDataKey] = "Sala não encontrada."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            if (!RoomTypes.All.Contains(RoomType)) { TempData[ErrorMessages.TempDataKey] = "Tipo de sala inválido."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            if (Capacity < 1 || CreditsPerHour < 0) { TempData[ErrorMessages.TempDataKey] = "Capacidade deve ser >= 1 e créditos >= 0."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            room.RoomType = RoomType;
            room.Name = Name ?? "Sala";
            room.Capacity = Capacity;
            room.CreditsPerHour = CreditsPerHour;
            room.IsActive = IsActive;
            room.UpdatedAt = DateTime.UtcNow;
            if (RoomPhotos != null && RoomPhotos.Length > 0)
            {
                var maxPhotoBytes = await GetMaxPhotoSizeBytesAsync();
                var storage = await _storageFactory.GetStorageServiceAsync();
                var folder = $"coworkings/{cow.Id}/rooms/{room.Id}";
                var maxOrder = await _context.CoworkingRoomPhotos.Where(p => p.CoworkingRoomId == room.Id).MaxAsync(p => (int?)p.DisplayOrder) ?? -1;
                int order = maxOrder + 1;
                foreach (var f in RoomPhotos.Take(10))
                {
                    if (f == null || f.Length == 0 || f.Length > maxPhotoBytes) continue;
                    var ext = Path.GetExtension(f.FileName); if (string.IsNullOrEmpty(ext) || !new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext.ToLower())) ext = ".jpg";
                    using var ms = new MemoryStream();
                    await f.CopyToAsync(ms);
                    var path = await storage.SaveFileAsync(ms.ToArray(), $"photo_{order}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}", folder);
                    _context.CoworkingRoomPhotos.Add(new CoworkingRoomPhoto { CoworkingRoomId = room.Id, FilePath = path, IsMain = false, DisplayOrder = order, CreatedAt = DateTime.UtcNow });
                    order++;
                }
            }
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.RoomUpdated;
            return RedirectToAction(ActionNames.AddressRooms, new { addressId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int roomId, int addressId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null) { TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado."; return RedirectToAction(ActionNames.Products); }
            var room = await _context.CoworkingRooms.Include(r => r.Photos).Include(r => r.CoworkingAddress).FirstOrDefaultAsync(r => r.Id == roomId && r.CoworkingAddress.CoworkingId == cow.Id);
            if (room == null) { TempData[ErrorMessages.TempDataKey] = "Sala não encontrada."; return RedirectToAction(ActionNames.AddressRooms, new { addressId }); }
            var storage = await _storageFactory.GetStorageServiceAsync();
            foreach (var p in room.Photos)
            {
                try { await storage.DeleteFileAsync(p.FilePath); } catch { }
            }
            await ReturnToReviewIfApprovedAsync(cow, userId, "Sala excluída");
            _context.CoworkingRooms.Remove(room);
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.RoomDeleted;
            return RedirectToAction(ActionNames.AddressRooms, new { addressId });
        }
        [HttpGet]
        public async Task<IActionResult> GetRoomPhoto(int photoId, CancellationToken cancellationToken = default)
        {
            var photo = await _context.CoworkingRoomPhotos.Include(p => p.CoworkingRoom).ThenInclude(r => r.CoworkingAddress).FirstOrDefaultAsync(p => p.Id == photoId);
            if (photo == null) return NotFound();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null || photo.CoworkingRoom.CoworkingAddress.CoworkingId != cow.Id) { if (!User.IsInRole(RoleNames.Administrator)) return Forbid(); }
            var storage = await _storageFactory.GetStorageServiceAsync();
            var bytes = await storage.GetFileAsync(photo.FilePath);
            if (bytes == null) return NotFound();
            var lower = photo.FilePath.ToLower();
            var ct = lower.EndsWith(".png") ? "image/png" : lower.EndsWith(".gif") ? "image/gif" : lower.EndsWith(".webp") ? "image/webp" : "image/jpeg";
            return File(bytes, ct);
        }
        [HttpGet]
        public async Task<IActionResult> PendingPayments(string? search = null, string? method = null, DateTime? dateFrom = null, DateTime? dateTo = null, decimal? minValue = null, decimal? maxValue = null, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var query = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Where(p => p.Status == PaymentStatus.AwaitingValidation
                    && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Id.ToString().Contains(search) ||
                    (p.Subscription.Contract.Client.RazaoSocial != null && p.Subscription.Contract.Client.RazaoSocial.Contains(search)) ||
                    (p.Subscription.Contract.Client.ContactName != null && p.Subscription.Contract.Client.ContactName.Contains(search)) ||
                    (p.Subscription.Contract.Client.CNPJ != null && p.Subscription.Contract.Client.CNPJ.Contains(search)) ||
                    (p.Subscription.Contract.Client.CPF != null && p.Subscription.Contract.Client.CPF.Contains(search)));
            }
            if (!string.IsNullOrEmpty(method))
            {
                if (Enum.TryParse<PaymentMethod>(method, out var paymentMethod))
                {
                    query = query.Where(p => p.Method == paymentMethod);
                }
            }
            if (dateFrom.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= dateFrom.Value);
            }
            if (dateTo.HasValue)
            {
                query = query.Where(p => p.CreatedAt <= dateTo.Value.AddDays(1).AddTicks(-1));
            }
            if (minValue.HasValue)
            {
                query = query.Where(p => p.Amount >= minValue.Value);
            }
            if (maxValue.HasValue)
            {
                query = query.Where(p => p.Amount <= maxValue.Value);
            }
            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            ViewBag.Search = search;
            ViewBag.Method = method;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.MinValue = minValue;
            ViewBag.MaxValue = maxValue;
            return View(payments);
        }
        [HttpGet]
        public async Task<IActionResult> ValidatePayment(int id, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);
            if (payment == null)
            {
                TempData[ErrorMessages.TempDataKey] = "Pagamento não encontrado.";
                return RedirectToAction(ActionNames.PendingPayments);
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var contract = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
            if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
            {
                TempData[ErrorMessages.TempDataKey] = "Você não tem permissão para validar este pagamento.";
                return RedirectToAction(ActionNames.PendingPayments);
            }
            payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(p => p.Id == id);
            return View(payment);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Pagamento não encontrado.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    return RedirectToAction(ActionNames.Register);
                }
                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
                {
                    TempData[ErrorMessages.TempDataKey] = "Você não tem permissão para validar este pagamento.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                if (payment.Status != PaymentStatus.AwaitingValidation)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este pagamento já foi processado.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                payment.ValidatedBy = userId;
                payment.ValidatedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                _context.PaymentHistories.Add(new PaymentHistory
                {
                    PaymentId = paymentId,
                    Action = PaymentHistoryActions.Approved,
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                });
                await _context.SaveChangesAsync();
                var previousPaidPayments = await _context.Payments
                    .Where(p => p.SubscriptionId == payment.SubscriptionId &&
                               p.Id != paymentId &&
                               p.Status == PaymentStatus.Paid)
                    .CountAsync();
                var isFirstPayment = previousPaidPayments == 0;
                var originalStatus = contract.Status;
                if (contract.Status == ContractStatus.PendingPayment || contract.Status == ContractStatus.Suspended)
                {
                    contract.Status = ContractStatus.Active;
                    contract.UpdatedAt = DateTime.UtcNow;
                    if (isFirstPayment && contract.ExpiryDate == null)
                    {
                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                    }
                }
                await _context.SaveChangesAsync();
                if (contract.Status == ContractStatus.Active)
                {
                    var activeContractNumber = contract.ContractNumber ?? "N/A";
                    var cancellationReasonContract = "Cliente já possui um endereço fiscal ativo. Outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";
                    var rejectionReasonPayment = "Cancelado pois outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";
                    var others = await _context.Contracts
                        .Include(c => c.Subscription)
                        .Where(c => c.ClientId == contract.ClientId && c.Id != contract.Id && c.Status == ContractStatus.PendingPayment)
                        .ToListAsync();
                    var now = DateTime.UtcNow;
                    var nowBr = DateTimeHelper.GetBrazilianTime();
                    var subscriptionIds = new List<int>();
                    foreach (var c in others)
                    {
                        c.Status = ContractStatus.Cancelled;
                        c.CancelledAt = now;
                        c.CancellationReason = cancellationReasonContract;
                        c.UpdatedAt = now;
                        if (c.Subscription != null)
                            subscriptionIds.Add(c.Subscription.Id);
                    }
                    if (subscriptionIds.Count > 0)
                    {
                        var paymentsToCancel = await _context.Payments
                            .Where(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)
                                && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing || p.Status == PaymentStatus.AwaitingValidation))
                            .ToListAsync();
                        foreach (var p in paymentsToCancel)
                        {
                            p.Status = PaymentStatus.Cancelled;
                            p.UpdatedAt = now;
                            _context.PaymentHistories.Add(new PaymentHistory
                            {
                                PaymentId = p.Id,
                                Action = PaymentHistoryActions.Cancelled,
                                RejectionReason = rejectionReasonPayment,
                                ActionBy = userId,
                                ActionAt = nowBr
                            });
                        }
                    }
                    if (others.Count > 0)
                        await _context.SaveChangesAsync();
                }
                if (isFirstPayment && contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                {
                    contract.CoworkingAddress.CurrentContracts++;
                    if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                    {
                        contract.CoworkingAddress.IsAvailable = false;
                    }
                    await _coworkingService.UpdateAddressAsync(contract.CoworkingAddress);
                }
                await _commissionService.CreateCommissionAsync(payment.Id);
                if (isFirstPayment && contract.ExpiryDate.HasValue)
                {
                    try
                    {
                        await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                    }
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentApprovedContractActivated(isFirstPayment && contract.ExpiryDate.HasValue);
                return RedirectToAction(ActionNames.PendingPayments);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao aprovar pagamento: {ex.Message}";
                return RedirectToAction(ActionNames.PendingPayments);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string validationNotes, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Pagamento não encontrado.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    return RedirectToAction(ActionNames.Register);
                }
                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
                {
                    TempData[ErrorMessages.TempDataKey] = "Você não tem permissão para validar este pagamento.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                if (payment.Status != PaymentStatus.AwaitingValidation)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este pagamento já foi processado.";
                    return RedirectToAction(ActionNames.PendingPayments);
                }
                if (string.IsNullOrWhiteSpace(validationNotes))
                {
                    TempData[ErrorMessages.TempDataKey] = "Por favor, informe a justificativa da reprovação.";
                    return RedirectToAction(ActionNames.ValidatePayment, new { id = paymentId });
                }
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = validationNotes;
                payment.ValidatedBy = userId;
                payment.ValidatedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentRejectedClientNotified;
                return RedirectToAction(ActionNames.PendingPayments);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao reprovar pagamento: {ex.Message}";
                return RedirectToAction(ActionNames.PendingPayments);
            }
        }
        [HttpGet]
        public async Task<IActionResult> DownloadPaymentReceipt(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var contract = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
            if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
            {
                return Forbid();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(payment.ReceiptFilePath);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var fileName = Path.GetFileName(payment.ReceiptFilePath);
            var contentType = fileName.EndsWith(".pdf") ? "application/pdf" : "image/jpeg";
            return File(fileBytes, contentType, fileName);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhotos(List<IFormFile> Photos, CancellationToken cancellationToken = default)
        {
            var isAjax = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue;
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    if (isAjax)
                        return new JsonResult(new { success = false, error = "Coworking não encontrado." }) { StatusCode = 400 };
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Register);
                }
                if (Photos == null || Photos.Count == 0)
                {
                    if (isAjax)
                        return new JsonResult(new { success = false, error = "Por favor, selecione pelo menos uma foto." }) { StatusCode = 400 };
                    TempData[ErrorMessages.TempDataKey] = "Por favor, selecione pelo menos uma foto.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var existingPhotosCount = await _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .CountAsync();
                var maxPhotos = 2;
                var photosToAdd = Photos.Take(maxPhotos - existingPhotosCount);
                if (!photosToAdd.Any())
                {
                    var limitMessage = $"Você já atingiu o limite máximo de {maxPhotos} fotos. Remova uma foto antes de adicionar outra.";
                    if (isAjax)
                        return new JsonResult(new { success = false, error = limitMessage }) { StatusCode = 400 };
                    TempData[ErrorMessages.TempDataKey] = limitMessage;
                    TempData[TempDataKeys.ActiveTab] = "photos";
                    return RedirectToAction(ActionNames.Profile);
                }
                var maxBytes = await GetMaxPhotoSizeBytesAsync();
                var maxMb = (int)(maxBytes / (1024 * 1024));
                foreach (var p in photosToAdd)
                {
                    if (p.Length > 0 && p.Length > maxBytes)
                    {
                        var sizeMessage = $"O arquivo \"{p.FileName}\" é muito grande. Tamanho máximo: {maxMb}MB. Ajuste em Admin > Configurações > Upload.MaxPhotoSizeMB.";
                        if (isAjax)
                            return new JsonResult(new { success = false, error = sizeMessage }) { StatusCode = 400 };
                        TempData[ErrorMessages.TempDataKey] = sizeMessage;
                        TempData[TempDataKeys.ActiveTab] = "photos";
                        return RedirectToAction(ActionNames.Profile);
                    }
                }
                var storageService = await _storageFactory.GetStorageServiceAsync();
                var coworkingFolder = $"coworkings/{coworkingEntity.Id}/photos";
                var photoCount = existingPhotosCount;
                foreach (var photo in photosToAdd)
                {
                    if (photo.Length > 0 && photo.Length <= maxBytes)
                    {
                        using var stream = new MemoryStream();
                        await photo.CopyToAsync(stream);
                        var fileBytes = stream.ToArray();
                        var fileName = $"photo_{coworkingEntity.Id}_{photoCount}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(photo.FileName)}";
                        var filePath = await storageService.SaveFileAsync(fileBytes, fileName, coworkingFolder);
                        var coworkingPhoto = new CoworkingPhoto
                        {
                            CoworkingId = coworkingEntity.Id,
                            FilePath = filePath,
                            Description = photo.FileName,
                            IsMain = existingPhotosCount == 0 && photoCount == existingPhotosCount,
                            DisplayOrder = photoCount,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.CoworkingPhotos.Add(coworkingPhoto);
                        photoCount++;
                    }
                }
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Foto(s) adicionada(s)");
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PhotosAdded(photosToAdd.Count());
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                if (isAjax)
                {
                    var message = ex.InnerException?.Message ?? ex.Message;
                    if (message.Contains("InvalidRequest", StringComparison.OrdinalIgnoreCase) || message.Contains("AWSSDK", StringComparison.OrdinalIgnoreCase))
                        message = "Falha ao salvar as fotos no armazenamento (Oracle Cloud). Verifique em Admin > Configurações do Sistema > Armazenamento: namespace, região, bucket e credenciais Oracle.OS.";
                    return new JsonResult(new { success = false, error = $"Erro ao fazer upload das fotos: {message}" }) { StatusCode = 500 };
                }
                TempData[ErrorMessages.TempDataKey] = $"Erro ao fazer upload das fotos: {ex.Message}";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhoto(int photoId, CancellationToken cancellationToken = default)
        {
            var photo = await _context.CoworkingPhotos
                .Include(p => p.Coworking)
                .FirstOrDefaultAsync(p => p.Id == photoId);
            if (photo == null)
            {
                return NotFound();
            }
            var isPublic = photo.Coworking.IsActive && photo.Coworking.IsPartner;
            if (!isPublic && User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                var canAccess = User.IsInRole(RoleNames.Administrator) ||
                              (coworkingEntity != null && photo.CoworkingId == coworkingEntity.Id) ||
                              User.IsInRole(RoleNames.Client);
                if (!canAccess)
                {
                    return Forbid();
                }
            }
            else if (!isPublic)
            {
                return Forbid();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(photo.FilePath);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var contentType = "image/jpeg";
            var lowerPath = photo.FilePath.ToLower();
            if (lowerPath.EndsWith(".png"))
                contentType = "image/png";
            else if (lowerPath.EndsWith(".gif"))
                contentType = "image/gif";
            else if (lowerPath.EndsWith(".webp"))
                contentType = "image/webp";
            return File(fileBytes, contentType);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMainPhoto(int photoId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var photo = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.CoworkingId == coworkingEntity.Id);
                if (photo == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Foto não encontrada.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var allPhotos = await _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .ToListAsync();
                foreach (var p in allPhotos)
                {
                    p.IsMain = false;
                }
                photo.IsMain = true;
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Foto principal alterada");
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.MainPhotoUpdated;
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao atualizar foto principal: {ex.Message}";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int photoId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var photo = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.CoworkingId == coworkingEntity.Id);
                if (photo == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Foto não encontrada.";
                    return RedirectToAction(ActionNames.Profile);
                }
                try
                {
                    var storageService = await _storageFactory.GetStorageServiceAsync();
                    await storageService.DeleteFileAsync(photo.FilePath);
                }
                catch
                {
                }
                _context.CoworkingPhotos.Remove(photo);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PhotoDeleted;
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao excluir foto: {ex.Message}";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPartner(CoworkingPartner partner, IFormFile? partnerPhoto, bool IsPrincipal = false, bool IsContractSigner = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var maxBytes = await GetMaxPhotoSizeBytesAsync();
                var maxMb = (int)(maxBytes / (1024 * 1024));
                if (partnerPhoto != null && partnerPhoto.Length > 0 && partnerPhoto.Length > maxBytes)
                {
                    TempData[ErrorMessages.TempDataKey] = $"A foto do sócio é muito grande. Tamanho máximo: {maxMb}MB.";
                    TempData[TempDataKeys.ActiveTab] = "partners";
                    return RedirectToAction(ActionNames.Profile);
                }
                partner.CoworkingId = coworkingEntity.Id;
                partner.CreatedAt = DateTime.UtcNow;
                partner.IsPrincipal = IsPrincipal;
                partner.IsContractSigner = IsContractSigner;
                if (Request.Form.ContainsKey("SharePercentage") && !string.IsNullOrWhiteSpace(Request.Form["SharePercentage"]))
                {
                    var sharePercentageStr = Request.Form["SharePercentage"].ToString().Trim().Replace(",", ".");
                    if (decimal.TryParse(sharePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sharePercentage))
                    {
                        if (sharePercentage < 0) sharePercentage = 0;
                        if (sharePercentage > 100) sharePercentage = 100;
                        partner.SharePercentage = sharePercentage;
                    }
                }
                if (IsPrincipal)
                {
                    var others = await _context.CoworkingPartners.Where(p => p.CoworkingId == coworkingEntity.Id).ToListAsync();
                    foreach (var p in others) { p.IsPrincipal = false; }
                }
                _context.CoworkingPartners.Add(partner);
                await _context.SaveChangesAsync();
                if (partnerPhoto != null && partnerPhoto.Length > 0 && partnerPhoto.Length <= maxBytes)
                {
                    var storageService = await _storageFactory.GetStorageServiceAsync();
                    var folder = $"coworkings/{coworkingEntity.Id}/partners";
                    var ext = Path.GetExtension(partnerPhoto.FileName);
                    if (string.IsNullOrEmpty(ext) || !new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext.ToLower())) ext = ".jpg";
                    var fileName = $"partner_{partner.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                    using var stream = new MemoryStream();
                    await partnerPhoto.CopyToAsync(stream);
                    partner.PhotoPath = await storageService.SaveFileAsync(stream.ToArray(), fileName, folder);
                    await _context.SaveChangesAsync();
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PartnerAdded;
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao adicionar sócio: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePartner(CoworkingPartner partner, IFormFile? partnerPhoto, bool IsPrincipal = false, bool IsContractSigner = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var existingPartner = await _context.CoworkingPartners
                    .FirstOrDefaultAsync(p => p.Id == partner.Id && p.CoworkingId == coworkingEntity.Id);
                if (existingPartner == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Sócio não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                existingPartner.Name = partner.Name;
                existingPartner.CPF = partner.CPF;
                existingPartner.Email = partner.Email;
                existingPartner.Phone = partner.Phone;
                existingPartner.Role = partner.Role;
                existingPartner.IsPrincipal = IsPrincipal;
                existingPartner.IsContractSigner = IsContractSigner;
                existingPartner.UpdatedAt = DateTime.UtcNow;
                if (Request.Form.ContainsKey("SharePercentage") && !string.IsNullOrWhiteSpace(Request.Form["SharePercentage"]))
                {
                    var sharePercentageStr = Request.Form["SharePercentage"].ToString().Trim().Replace(",", ".");
                    if (decimal.TryParse(sharePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sharePercentage))
                    {
                        if (sharePercentage < 0) sharePercentage = 0;
                        if (sharePercentage > 100) sharePercentage = 100;
                        existingPartner.SharePercentage = sharePercentage;
                    }
                }
                else
                {
                    existingPartner.SharePercentage = partner.SharePercentage;
                }
                if (IsPrincipal)
                {
                    var others = await _context.CoworkingPartners
                        .Where(p => p.CoworkingId == coworkingEntity.Id && p.Id != existingPartner.Id)
                        .ToListAsync();
                    foreach (var p in others) p.IsPrincipal = false;
                }
                var maxBytesPartner = await GetMaxPhotoSizeBytesAsync();
                var maxMbPartner = (int)(maxBytesPartner / (1024 * 1024));
                if (partnerPhoto != null && partnerPhoto.Length > 0 && partnerPhoto.Length > maxBytesPartner)
                {
                    TempData[ErrorMessages.TempDataKey] = $"A foto do sócio é muito grande. Tamanho máximo: {maxMbPartner}MB.";
                    TempData[TempDataKeys.ActiveTab] = "partners";
                    return RedirectToAction(ActionNames.Profile);
                }
                if (partnerPhoto != null && partnerPhoto.Length > 0 && partnerPhoto.Length <= maxBytesPartner)
                {
                    var storageService = await _storageFactory.GetStorageServiceAsync();
                    if (!string.IsNullOrEmpty(existingPartner.PhotoPath))
                    {
                        try { await storageService.DeleteFileAsync(existingPartner.PhotoPath); } catch { }
                    }
                    var folder = $"coworkings/{coworkingEntity.Id}/partners";
                    var ext = Path.GetExtension(partnerPhoto.FileName);
                    if (string.IsNullOrEmpty(ext) || !new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext.ToLower())) ext = ".jpg";
                    var fileName = $"partner_{existingPartner.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                    using var stream = new MemoryStream();
                    await partnerPhoto.CopyToAsync(stream);
                    existingPartner.PhotoPath = await storageService.SaveFileAsync(stream.ToArray(), fileName, folder);
                }
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Sócio atualizado");
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PartnerUpdated;
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao atualizar sócio: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartner(int partnerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var partner = await _context.CoworkingPartners
                    .FirstOrDefaultAsync(p => p.Id == partnerId && p.CoworkingId == coworkingEntity.Id);
                if (partner == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Sócio não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                if (!string.IsNullOrEmpty(partner.PhotoPath))
                {
                    try
                    {
                        var storageService = await _storageFactory.GetStorageServiceAsync();
                        await storageService.DeleteFileAsync(partner.PhotoPath);
                    }
                    catch { }
                }
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Sócio excluído");
                _context.CoworkingPartners.Remove(partner);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PartnerDeleted;
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao excluir sócio: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "partners";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResubmitForApproval(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) { TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado."; return RedirectToAction(ActionNames.Dashboard); }
            if (cow.ApprovalStatus != CoworkingApprovalStatus.AwaitingAdjustment)
            {
                TempData[ErrorMessages.TempDataKey] = "Apenas cadastros com status Aguardando ajuste podem ser reenviados.";
                return RedirectToAction(ActionNames.Profile);
            }
            cow.ApprovalStatus = CoworkingApprovalStatus.UnderReview;
            cow.SubmittedAt = DateTime.UtcNow;
            await _coworkingService.UpdateAsync(cow);
            await _auditService.LogActionAsync(userId, "ResubmitForApproval", "Coworking", cow.Id, "{\"ApprovalStatus\":\"AwaitingAdjustment\"}", "{\"ApprovalStatus\":\"UnderReview\"}");
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.RegistrationResubmitted;
            return RedirectToAction(ActionNames.Profile);
        }
        [HttpGet]
        public async Task<IActionResult> GetPartnerPhoto(int partnerId, CancellationToken cancellationToken = default)
        {
            var partner = await _context.CoworkingPartners.Include(p => p.Coworking).FirstOrDefaultAsync(p => p.Id == partnerId);
            if (partner == null || string.IsNullOrEmpty(partner.PhotoPath)) return NotFound();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null || partner.CoworkingId != cow.Id)
            {
                if (!User.IsInRole(RoleNames.Administrator)) return Forbid();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(partner.PhotoPath);
            if (fileBytes == null) return NotFound();
            var lower = partner.PhotoPath.ToLower();
            var contentType = lower.EndsWith(".png") ? "image/png" : lower.EndsWith(".gif") ? "image/gif" : lower.EndsWith(".webp") ? "image/webp" : "image/jpeg";
            return File(fileBytes, contentType);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(int documentType, IFormFile documentFile, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Register);
                }
                if (documentFile == null || documentFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = "Por favor, selecione um arquivo.";
                    return RedirectToAction(ActionNames.Profile);
                }
                if (documentFile.Length > 10 * 1024 * 1024)
                {
                    TempData[ErrorMessages.TempDataKey] = "O arquivo excede o tamanho máximo de 10MB.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var docType = (CoworkingDocumentType)documentType;
                var storageService = await _storageFactory.GetStorageServiceAsync();
                using var stream = new MemoryStream();
                await documentFile.CopyToAsync(stream);
                var fileBytes = stream.ToArray();
                var folderName = docType switch
                {
                    CoworkingDocumentType.AddressCessionContract => "address_cession_contract",
                    CoworkingDocumentType.AddressProof => "address_proof",
                    CoworkingDocumentType.CNPJCard => "cnpj_card",
                    CoworkingDocumentType.AddressAuthorization => "address_authorization",
                    _ => "other"
                };
                var fileName = $"{folderName}_{coworkingEntity.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(documentFile.FileName)}";
                var filePath = await storageService.SaveFileAsync(fileBytes, fileName, $"coworkings/{coworkingEntity.Id}/documents");
                var existingDoc = await _context.CoworkingDocuments
                    .FirstOrDefaultAsync(d => d.CoworkingId == coworkingEntity.Id && d.Type == docType);
                if (existingDoc != null)
                {
                    try
                    {
                        await storageService.DeleteFileAsync(existingDoc.FilePath);
                    }
                    catch
                    {
                    }
                    existingDoc.FileName = documentFile.FileName;
                    existingDoc.FilePath = filePath;
                    existingDoc.UpdatedAt = DateTime.UtcNow;
                    existingDoc.IsValid = true;
                    existingDoc.Status = CoworkingDocumentStatus.Pending;
                    existingDoc.RejectionReason = null;
                    if (docType == CoworkingDocumentType.AddressProof)
                    {
                        existingDoc.ExpiresAt = DateTime.UtcNow.AddDays(90);
                    }
                }
                else
                {
                    var coworkingDocument = new CoworkingDocument
                    {
                        CoworkingId = coworkingEntity.Id,
                        Type = docType,
                        FileName = documentFile.FileName,
                        FilePath = filePath,
                        IsValid = true,
                        Status = CoworkingDocumentStatus.Pending,
                        ExpiresAt = docType == CoworkingDocumentType.AddressProof
                            ? DateTime.UtcNow.AddDays(90)
                            : null
                    };
                    _context.CoworkingDocuments.Add(coworkingDocument);
                }
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Documento enviado");
                await _context.SaveChangesAsync();
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao enviar documento: {ex.Message}";
                return RedirectToAction(ActionNames.Profile);
            }
        }
        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId, CancellationToken cancellationToken = default)
        {
            var document = await _context.CoworkingDocuments.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null || document.CoworkingId != coworkingEntity.Id)
            {
                if (!User.IsInRole(RoleNames.Administrator))
                {
                    return Forbid();
                }
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(document.FilePath);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var contentType = document.FileName.EndsWith(".pdf") ? "application/pdf" : "image/jpeg";
            return File(fileBytes, contentType, document.FileName);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOperatingHours(int CoworkingId, IFormCollection form, CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            if (!StringBooleanValue.IsTrue(enableCoworkingMenus))
                return RedirectToAction(ActionNames.Profile);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null || coworkingEntity.Id != CoworkingId)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var daysOfWeek = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };
                foreach (var day in daysOfWeek)
                {
                    var isActiveKey = $"IsActive_{day}";
                    var isActive = form.ContainsKey(isActiveKey) && form[isActiveKey].ToString() == StringBooleanValue.True;
                    var openingTimeStr = form.ContainsKey($"OpeningTime_{day}") ? form[$"OpeningTime_{day}"].ToString() : null;
                    var closingTimeStr = form.ContainsKey($"ClosingTime_{day}") ? form[$"ClosingTime_{day}"].ToString() : null;
                    TimeSpan? openingTime = null;
                    TimeSpan? closingTime = null;
                    if (!string.IsNullOrWhiteSpace(openingTimeStr) && TimeSpan.TryParse(openingTimeStr, out var opening))
                    {
                        openingTime = opening;
                    }
                    if (!string.IsNullOrWhiteSpace(closingTimeStr) && TimeSpan.TryParse(closingTimeStr, out var closing))
                    {
                        closingTime = closing;
                    }
                    var existing = await _context.CoworkingOperatingHours
                        .FirstOrDefaultAsync(h => h.CoworkingId == CoworkingId && h.DayOfWeek == day);
                    if (existing != null)
                    {
                        existing.IsActive = isActive;
                        existing.OpeningTime = isActive ? openingTime : null;
                        existing.ClosingTime = isActive ? closingTime : null;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (isActive)
                    {
                        var newHour = new CoworkingOperatingHours
                        {
                            CoworkingId = CoworkingId,
                            DayOfWeek = day,
                            IsActive = true,
                            OpeningTime = openingTime,
                            ClosingTime = closingTime,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.CoworkingOperatingHours.Add(newHour);
                    }
                }
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.OperatingHoursSaved;
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao salvar horários: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            return RedirectToAction(ActionNames.Profile);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBlockedDate(int CoworkingId, DateTime BlockedDateValue, string Reason, bool BlocksFullDay, TimeSpan? StartTime, TimeSpan? EndTime, CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            if (!StringBooleanValue.IsTrue(enableCoworkingMenus))
                return RedirectToAction(ActionNames.Profile);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null || coworkingEntity.Id != CoworkingId)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var existing = await _context.BlockedDates
                    .FirstOrDefaultAsync(b => b.CoworkingId == CoworkingId && b.BlockedDateValue.Date == BlockedDateValue.Date);
                if (existing != null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Já existe um bloqueio para esta data.";
                    TempData[TempDataKeys.ActiveTab] = "hours";
                    return RedirectToAction(ActionNames.Profile);
                }
                var blockedDate = new BlockedDate
                {
                    CoworkingId = CoworkingId,
                    BlockedDateValue = BlockedDateValue.Date,
                    Reason = Reason,
                    BlocksFullDay = BlocksFullDay,
                    StartTime = BlocksFullDay ? null : StartTime,
                    EndTime = BlocksFullDay ? null : EndTime,
                    CreatedAt = DateTime.UtcNow
                };
                _context.BlockedDates.Add(blockedDate);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.BlockedDateAdded;
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao adicionar data bloqueada: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            return RedirectToAction(ActionNames.Profile);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlockedDate(int id, CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            if (!StringBooleanValue.IsTrue(enableCoworkingMenus))
                return RedirectToAction(ActionNames.Profile);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Profile);
                }
                var blockedDate = await _context.BlockedDates
                    .FirstOrDefaultAsync(b => b.Id == id && b.CoworkingId == coworkingEntity.Id);
                if (blockedDate == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Data bloqueada não encontrada.";
                    TempData[TempDataKeys.ActiveTab] = "hours";
                    return RedirectToAction(ActionNames.Profile);
                }
                _context.BlockedDates.Remove(blockedDate);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.BlockedDateRemoved;
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao remover data bloqueada: {ex.Message}";
                TempData[TempDataKeys.ActiveTab] = "hours";
            }
            return RedirectToAction(ActionNames.Profile);
        }
        [HttpGet]
        public async Task<IActionResult> MyContract(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var partnerServices = await _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == coworkingEntity.Id)
                .ToListAsync();
            partnerServices = partnerServices
                .Where(ps => ps.Service != null)
                .OrderBy(ps => ps.Service!.Name)
                .ToList();
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
            if (!showCoworkingMenus)
            {
                partnerServices = partnerServices
                    .Where(ps => ps.Service != null && ps.Service.Code == FiscalAddressOffer)
                    .ToList();
            }
            return View(partnerServices);
        }
        [HttpGet]
        public async Task<IActionResult> Services(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
            var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();
            var allServicesQuery = _context.Services
                .Where(s => s.IsActive && s.Code != FiscalAddressContracting);
            if (!showCoworkingMenus)
            {
                allServicesQuery = allServicesQuery.Where(s => !coworkingServiceNames.Contains(s.Name));
            }
            var allServices = await allServicesQuery
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
            var partnerServicesQuery = _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == coworkingEntity.Id);
            if (!showCoworkingMenus)
            {
                partnerServicesQuery = partnerServicesQuery.Where(ps => !coworkingServiceNames.Contains(ps.Service.Name));
            }
            var partnerServices = await partnerServicesQuery.ToListAsync();
            partnerServices = partnerServices.Where(ps => ps.Service?.Code != FiscalAddressContracting).ToList();
            var partnerServiceIds = partnerServices.Select(ps => ps.ServiceId).ToHashSet();
            var coworkingAddresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingEntity.Id)
                .Select(a => a.Id)
                .ToListAsync();
            var hasActiveContracts = await _context.Contracts
                .AnyAsync(c => coworkingAddresses.Contains(c.CoworkingAddressId)
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended));
            var contractIdsFromCoworking = await _context.Contracts
                .Where(c => coworkingAddresses.Contains(c.CoworkingAddressId))
                .Select(c => c.Id)
                .ToListAsync();
            var subscriptionIds = await _context.Subscriptions
                .Where(s => contractIdsFromCoworking.Contains(s.ContractId))
                .Select(s => s.Id)
                .ToListAsync();
            var hasClientAwaitingValidation = await _context.Payments
                .AnyAsync(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)
                    && p.Status == PaymentStatus.AwaitingValidation);
            ViewBag.PartnerServices = partnerServices;
            ViewBag.PartnerServiceIds = partnerServiceIds;
            ViewBag.CoworkingId = coworkingEntity.Id;
            ViewBag.ShowCoworkingMenus = showCoworkingMenus;
            ViewBag.HasActiveContracts = hasActiveContracts;
            ViewBag.HasClientAwaitingValidation = hasClientAwaitingValidation;
            return View(allServices);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddService(int serviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Services);
                }
                var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive);
                if (service == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Serviço não encontrado ou inativo.";
                    return RedirectToAction(ActionNames.Services);
                }
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                {
                    TempData[ErrorMessages.TempDataKey] = "Este serviço não está disponível no momento.";
                    return RedirectToAction(ActionNames.Services);
                }
                var existing = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingEntity.Id && ps.ServiceId == serviceId);
                if (existing != null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este serviço já está cadastrado.";
                    return RedirectToAction(ActionNames.Services);
                }
                var partnerService = new PartnerService
                {
                    CoworkingId = coworkingEntity.Id,
                    ServiceId = serviceId,
                    Percentage = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PartnerServices.Add(partnerService);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceAdded(service.Name);
                return RedirectToAction(ActionNames.Services);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao adicionar serviço: {ex.Message}";
                return RedirectToAction(ActionNames.Services);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveService(int partnerServiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Coworking não encontrado.";
                    return RedirectToAction(ActionNames.Services);
                }
                var partnerService = await _context.PartnerServices
                    .Include(ps => ps.Service)
                    .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId && ps.CoworkingId == coworkingEntity.Id);
                if (partnerService == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Serviço não encontrado ou não pertence a este parceiro.";
                    return RedirectToAction(ActionNames.Services);
                }
                if (partnerService.RemovalRequestStatus == Domain.Core.Enums.RemovalRequestStatus.Pending)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.ServiceRemovalRequestAlreadyPending;
                    return RedirectToAction(ActionNames.Services);
                }
                var coworkingAddresses = await _context.CoworkingAddresses
                    .Where(a => a.CoworkingId == coworkingEntity.Id)
                    .Select(a => a.Id)
                    .ToListAsync();
                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => coworkingAddresses.Contains(c.CoworkingAddressId)
                        && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended));
                if (hasActiveContracts)
                {
                    TempData[ErrorMessages.TempDataKey] = "Não é possível solicitar a remoção deste serviço enquanto houver contratos ativos. A solicitação só pode ser feita após o fim de todos os contratos.";
                    return RedirectToAction(ActionNames.Services);
                }
                var contractIdsFromCoworking = await _context.Contracts
                    .Where(c => coworkingAddresses.Contains(c.CoworkingAddressId))
                    .Select(c => c.Id)
                    .ToListAsync();
                var subscriptionIds = await _context.Subscriptions
                    .Where(s => contractIdsFromCoworking.Contains(s.ContractId))
                    .Select(s => s.Id)
                    .ToListAsync();
                var hasClientAwaitingValidation = await _context.Payments
                    .AnyAsync(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)
                        && p.Status == PaymentStatus.AwaitingValidation);
                if (hasClientAwaitingValidation)
                {
                    TempData[ErrorMessages.TempDataKey] = "Não é possível solicitar a remoção deste serviço enquanto houver cliente(s) com pagamento aguardando validação.";
                    return RedirectToAction(ActionNames.Services);
                }
                var serviceName = partnerService.Service.Name;
                partnerService.RemovalRequestStatus = Domain.Core.Enums.RemovalRequestStatus.Pending;
                partnerService.RemovalRequestedAt = DateTimeHelper.GetBrazilianTime();
                partnerService.RemovalRejectionReason = null;
                partnerService.RemovalReviewedAt = null;
                partnerService.RemovalReviewedBy = null;
                partnerService.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
                await _auditService.LogActionAsync(userId, "ServiceRemovalRequested", "PartnerService", partnerService.Id,
                    null, $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}");
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceRemovalRequestSent(serviceName);
                return RedirectToAction(ActionNames.Services);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao solicitar remoção do serviço: {ex.Message}";
                return RedirectToAction(ActionNames.Services);
            }
        }
        [HttpGet]
        public async Task<IActionResult> ServiceFiscalNotes(int page = 1, int pageSize = 10, string? serviceType = null, string? client = null, string? contractNumber = null, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var query = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(ca => ca.Location)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                            .ThenInclude(cl => cl.User)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(cl => cl.User)
                .Where(p =>
                    (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id) ||
                    (p.Reservation != null && p.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                )
                .Where(p => p.Status == PaymentStatus.Paid)
                .Where(p =>
                    (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) ||
                    (p.Reservation != null)
                )
                .Where(p => !_context.FiscalNotes.Any(fn =>
                    fn.PaymentId == p.Id &&
                    fn.Type == FiscalNoteType.Servico &&
                    (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada)
                ))
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(serviceType))
            {
                if (serviceType.Equals("EnderecoFiscal", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.SubscriptionId != null);
                else if (serviceType.Equals("ReservaSala", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.ReservationId != null);
            }
            if (!string.IsNullOrWhiteSpace(contractNumber))
            {
                var contractNum = contractNumber.Trim();
                query = query.Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ContractNumber != null && p.Subscription.Contract.ContractNumber.Contains(contractNum));
            }
            if (!string.IsNullOrWhiteSpace(client))
            {
                var term = client.Trim().ToLower();
                var termDoc = client.Trim().Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "");
                var matchingClientIds = await _context.Clients
                    .Where(c =>
                        (c.RazaoSocial != null && c.RazaoSocial.ToLower().Contains(term)) ||
                        (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(term)) ||
                        (c.ContactName != null && c.ContactName.ToLower().Contains(term)) ||
                        (c.CNPJ != null && c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Contains(termDoc)) ||
                        (c.CPF != null && c.CPF.Replace(".", "").Replace("-", "").Contains(termDoc)))
                    .Select(c => c.Id)
                    .ToListAsync();
                if (matchingClientIds.Any())
                {
                    query = query.Where(p =>
                        (p.Subscription != null && p.Subscription.Contract != null && matchingClientIds.Contains(p.Subscription.Contract.ClientId)) ||
                        (p.Reservation != null && matchingClientIds.Contains(p.Reservation.ClientId)));
                }
                else
                    query = query.Where(p => false);
            }
            var totalItems = await query.CountAsync();
            var totalAmount = totalItems > 0 ? await query.SumAsync(p => p.Amount) : 0m;
            var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = Math.Max(1, totalPages);
            var payments = await query
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var paymentIds = payments.Select(p => p.Id).ToList();
            var commissions = await _context.Commissions
                .Where(c => paymentIds.Contains(c.PaymentId))
                .ToListAsync();
            var commissionDict = commissions.ToDictionary(c => c.PaymentId, c => c);
            var hasSefazConfiguration = await _context.CoworkingSefazConfigurations
                .AnyAsync(c => c.CoworkingId == coworkingEntity.Id && c.IsActive);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalAmount = totalAmount;
            ViewBag.PageSize = pageSize;
            ViewBag.ServiceType = serviceType;
            ViewBag.Client = client;
            ViewBag.ContractNumber = contractNumber;
            ViewBag.CoworkingId = coworkingEntity.Id;
            ViewBag.CommissionDict = commissionDict;
            ViewBag.HasSefazConfiguration = hasSefazConfiguration;
            return View(payments);
        }
        [HttpGet]
        public async Task<IActionResult> ServiceFiscalNoteDetail(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
                return NotFound();
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(ca => ca.Location)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                            .ThenInclude(cl => cl!.User)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(cl => cl!.User)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null)
                return NotFound();
            var belongsToCoworking = (payment.Subscription != null && payment.Subscription.Contract?.CoworkingAddress?.CoworkingId == coworkingEntity.Id) ||
                (payment.Reservation != null && payment.Reservation.CoworkingAddress?.CoworkingId == coworkingEntity.Id);
            if (!belongsToCoworking)
                return NotFound();
            var client = payment.Subscription?.Contract?.Client ?? payment.Reservation?.Client;
            var contract = payment.Subscription?.Contract;
            var reservation = payment.Reservation;
            var location = contract?.CoworkingAddress?.Location ?? reservation?.CoworkingAddress?.Location;
            var addr = contract?.CoworkingAddress ?? reservation?.CoworkingAddress;
            static string? FormatAddr(CoworkingAddress? a)
            {
                if (a == null) return null;
                var parts = new List<string> { a.Address, a.Number, a.Complement, a.Neighborhood, a.City, a.State, a.ZipCode };
                return string.Join(", ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            var detail = new
            {
                Payment = new
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
                    Address = FormatAddr(contract.CoworkingAddress),
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
                    Status = reservation.Status,
                    Address = FormatAddr(reservation.CoworkingAddress),
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
        public async Task<IActionResult> IssueServiceFiscalNote(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Parceiro não encontrado.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.CoworkingAddress)
                                .ThenInclude(ca => ca.Location)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.Client)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r.CoworkingAddress)
                            .ThenInclude(ca => ca.Location)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r.Client)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Pagamento não encontrado.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var belongsToCoworking = false;
                string state = "ES";
                if (payment.Subscription?.Contract?.CoworkingAddress != null)
                {
                    belongsToCoworking = payment.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id;
                    state = payment.Subscription.Contract.CoworkingAddress.Location?.State ?? "ES";
                }
                else if (payment.Reservation?.CoworkingAddress != null)
                {
                    belongsToCoworking = payment.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id;
                    state = payment.Reservation.CoworkingAddress.Location?.State ?? "ES";
                }
                if (!belongsToCoworking)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este pagamento não pertence ao seu coworking.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.PaymentId == paymentId && fn.Type == FiscalNoteType.Servico && fn.Status == FiscalNoteStatus.Emitida);
                if (existingNote != null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Já existe uma NF de serviço emitida para este pagamento.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                if (!_sefazService.IsStateSupported(state))
                {
                    TempData[ErrorMessages.TempDataKey] = $"Integração SEFAZ ainda não implementada para o estado {state}.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var sefazConfig = await _sefazService.GetCoworkingConfigurationAsync(coworkingEntity.Id, state, "Producao");
                if (sefazConfig == null)
                {
                    TempData[ErrorMessages.TempDataKey] = $"Configuração SEFAZ não encontrada para o estado {state}. Configure em Configurações SEFAZ.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var fiscalNote = new FiscalNote
                {
                    Type = FiscalNoteType.Servico,
                    PaymentId = payment.Id,
                    CoworkingId = coworkingEntity.Id,
                    ClientId = payment.Subscription?.Contract?.ClientId ?? payment.Reservation?.ClientId,
                    ContractId = payment.Subscription?.ContractId,
                    ReservationId = payment.ReservationId,
                    Amount = payment.Amount,
                    ServiceAmount = payment.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = FiscalNoteStatus.Pendente,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();
                try
                {
                    var emittedNote = await _sefazService.IssueServiceNoteAsync(fiscalNote, sefazConfig);
                    _context.FiscalNotes.Update(emittedNote);
                    await _context.SaveChangesAsync();
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.ServiceInvoiceEmitted(emittedNote.AccessKey);
                    return RedirectToAction(ActionNames.ListServiceFiscalNotes);
                }
                catch (Exception ex)
                {
                    _context.FiscalNotes.Update(fiscalNote);
                    await _context.SaveChangesAsync();
                    TempData[ErrorMessages.TempDataKey] = $"Erro ao emitir NF: {ex.Message}";
                }
                return RedirectToAction(ActionNames.ServiceFiscalNotes);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao processar emissão de NF: {ex.Message}";
                return RedirectToAction(ActionNames.ServiceFiscalNotes);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportServiceFiscalNote(int paymentId, IFormFile pdfFile, IFormFile? xmlFile, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Parceiro não encontrado.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var payment = await _context.Payments
                    .Include(p => p.Subscription).ThenInclude(s => s.Contract).ThenInclude(c => c.CoworkingAddress)
                    .Include(p => p.Reservation).ThenInclude(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Pagamento não encontrado.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var belongsToCoworking = (payment.Subscription?.Contract?.CoworkingAddress != null && payment.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                    || (payment.Reservation?.CoworkingAddress != null && payment.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id);
                if (!belongsToCoworking)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este pagamento não pertence ao seu coworking.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var existing = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.PaymentId == paymentId && fn.Type == FiscalNoteType.Servico && (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada));
                if (existing != null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Já existe NF de serviço (emitida ou importada) para este pagamento.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = "Selecione o arquivo PDF da nota fiscal.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData[ErrorMessages.TempDataKey] = "O arquivo deve ser um PDF.";
                    return RedirectToAction(ActionNames.ServiceFiscalNotes);
                }
                var storage = await _storageFactory.GetStorageServiceAsync();
                var pdfFileName = $"imported_payment_{paymentId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                using var ms = new MemoryStream();
                await pdfFile.CopyToAsync(ms);
                var pdfPath = await storage.SaveFileAsync(ms.ToArray(), pdfFileName, $"invoices/imported/{coworkingEntity.Id}");
                var fiscalNote = new FiscalNote
                {
                    Type = FiscalNoteType.Servico,
                    PaymentId = payment.Id,
                    CoworkingId = coworkingEntity.Id,
                    ClientId = payment.Subscription?.Contract?.ClientId ?? payment.Reservation?.ClientId,
                    ContractId = payment.Subscription?.ContractId,
                    ReservationId = payment.ReservationId,
                    Amount = payment.Amount,
                    ServiceAmount = payment.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = FiscalNoteStatus.Importada,
                    PdfUrl = pdfPath,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceImportedPendingValidation;
                return RedirectToAction(ActionNames.ServiceFiscalNotes);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao importar NF: {ex.Message}";
                return RedirectToAction(ActionNames.ServiceFiscalNotes);
            }
        }
        [HttpGet]
        public async Task<IActionResult> ListServiceFiscalNotes(int page = 1, int pageSize = 10, string? status = null, string? type = null, string? noteNumber = null, string? client = null, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var query = _context.FiscalNotes
                .Include(fn => fn.Payment)
                .Include(fn => fn.Client)
                    .ThenInclude(c => c.User)
                .Include(fn => fn.Contract)
                    .ThenInclude(c => c.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .Include(fn => fn.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(ca => ca.Location)
                .Where(fn => fn.CoworkingId == coworkingEntity.Id && fn.Type == FiscalNoteType.Servico)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FiscalNoteStatus>(status, true, out var fiscalStatus))
            {
                query = query.Where(fn => fn.Status == fiscalStatus);
            }
            if (!string.IsNullOrWhiteSpace(noteNumber))
            {
                var num = noteNumber.Trim();
                query = query.Where(fn => fn.Number != null && fn.Number.Contains(num));
            }
            if (!string.IsNullOrWhiteSpace(client))
            {
                var c = client.Trim();
                query = query.Where(fn => fn.Client != null && (
                    (fn.Client.RazaoSocial != null && fn.Client.RazaoSocial.Contains(c)) ||
                    (fn.Client.NomeFantasia != null && fn.Client.NomeFantasia.Contains(c)) ||
                    (fn.Client.CNPJ != null && fn.Client.CNPJ.Contains(c)) ||
                    (fn.Client.CPF != null && fn.Client.CPF.Contains(c)) ||
                    (fn.Client.User != null && fn.Client.User.FullName != null && fn.Client.User.FullName.Contains(c))
                ));
            }
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            var fiscalNotes = await query
                .OrderByDescending(fn => fn.IssueDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Status = status;
            ViewBag.NoteNumber = noteNumber;
            ViewBag.Client = client;
            return View(fiscalNotes);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadFiscalNotePdf(int fiscalNoteId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var fn = await _context.FiscalNotes
                .FirstOrDefaultAsync(f => f.Id == fiscalNoteId && f.CoworkingId == coworkingEntity.Id);
            if (fn == null || string.IsNullOrEmpty(fn.PdfUrl))
            {
                return NotFound();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(fn.PdfUrl);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var fileName = Path.GetFileName(fn.PdfUrl) ?? $"nota_fiscal_{fn.Id}.pdf";
            Response.Headers.Append(HttpHeaderNames.ContentDisposition, $"inline; filename=\"{fileName}\"");
            return File(fileBytes, "application/pdf");
        }
        [HttpGet]
        public async Task<IActionResult> SefazConfiguration(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction(ActionNames.Register);
            }
            var configurations = await _context.CoworkingSefazConfigurations
                .Where(c => c.CoworkingId == coworkingEntity.Id)
                .OrderBy(c => c.State)
                .ThenBy(c => c.Environment)
                .ToListAsync();
            ViewBag.Coworking = coworkingEntity;
            ViewBag.SupportedStates = new[] { "ES" };
            return View(configurations);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSefazConfiguration(
            int? id,
            string state,
            string environment,
            string partnerCNPJ,
            string partnerMunicipalRegistration,
            string serviceCode,
            string serviceDescription,
            decimal issAliquot,
            string issMunicipality,
            string? homologationUrl,
            string? productionUrl)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData[ErrorMessages.TempDataKey] = "Parceiro não encontrado.";
                    return RedirectToAction(ActionNames.SefazConfiguration);
                }
                CoworkingSefazConfiguration config;
                if (id.HasValue)
                {
                    config = await _context.CoworkingSefazConfigurations
                        .FirstOrDefaultAsync(c => c.Id == id.Value && c.CoworkingId == coworkingEntity.Id);
                    if (config == null)
                    {
                        TempData[ErrorMessages.TempDataKey] = "Configuração não encontrada.";
                        return RedirectToAction(ActionNames.SefazConfiguration);
                    }
                }
                else
                {
                    var existing = await _context.CoworkingSefazConfigurations
                        .FirstOrDefaultAsync(c => c.CoworkingId == coworkingEntity.Id &&
                                                  c.State == state.ToUpper() &&
                                                  c.Environment == environment);
                    if (existing != null)
                    {
                        TempData[ErrorMessages.TempDataKey] = "Já existe uma configuração para este estado e ambiente.";
                        return RedirectToAction(ActionNames.SefazConfiguration);
                    }
                    config = new CoworkingSefazConfiguration
                    {
                        CoworkingId = coworkingEntity.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CoworkingSefazConfigurations.Add(config);
                }
                config.State = state.ToUpper();
                config.Environment = environment;
                config.PartnerCNPJ = partnerCNPJ;
                config.PartnerMunicipalRegistration = partnerMunicipalRegistration;
                config.ServiceCode = serviceCode;
                config.ServiceDescription = serviceDescription;
                config.ISSAliquot = issAliquot;
                config.ISSMunicipality = issMunicipality;
                config.HomologationUrl = homologationUrl;
                config.ProductionUrl = productionUrl;
                config.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.SefazConfigSaved;
                return RedirectToAction(ActionNames.SefazConfiguration);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao salvar configuração: {ex.Message}";
                return RedirectToAction(ActionNames.SefazConfiguration);
            }
        }
        #region Monthly Settlements (Repasses Mensais)
        [HttpGet]
        public async Task<IActionResult> MonthlySettlements(CancellationToken cancellationToken = default)
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth);
            }
            var settlements = await _monthlySettlementService.GetSettlementsByCoworkingAsync(coworking.Id);
            var summary = await _monthlySettlementService.GetDashboardSummaryAsync(coworking.Id);
            ViewBag.Summary = summary;
            return View(settlements);
        }
        [HttpGet]
        public async Task<IActionResult> SettlementDetails(int id, CancellationToken cancellationToken = default)
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth);
            }
            var settlement = await _monthlySettlementService.GetSettlementByIdAsync(id);
            if (settlement == null || settlement.CoworkingId != coworking.Id)
            {
                TempData[ErrorMessages.TempDataKey] = "Fechamento não encontrado.";
                return RedirectToAction(ActionNames.MonthlySettlements);
            }
            var invoice = await _monthlySettlementService.GetInvoiceBySettlementAsync(id);
            ViewBag.Invoice = invoice;
            return View(settlement);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPartnerInvoice(int settlementId, IFormFile pdfFile, IFormFile? xmlFile,
            string? invoiceNumber, decimal invoiceAmount, string? partnerNotes)
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth);
            }
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = "Por favor, selecione o arquivo PDF da nota fiscal.";
                    return RedirectToAction(ActionNames.MonthlySettlements);
                }
                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData[ErrorMessages.TempDataKey] = "O arquivo deve ser um PDF.";
                    return RedirectToAction(ActionNames.MonthlySettlements);
                }
                using var pdfStream = pdfFile.OpenReadStream();
                Stream? xmlStream = null;
                string? xmlFileName = null;
                if (xmlFile != null && xmlFile.Length > 0)
                {
                    if (!xmlFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData[ErrorMessages.TempDataKey] = "O arquivo XML deve ter extensão .xml";
                        return RedirectToAction(ActionNames.MonthlySettlements);
                    }
                    xmlStream = xmlFile.OpenReadStream();
                    xmlFileName = xmlFile.FileName;
                }
                var invoice = await _monthlySettlementService.UploadPartnerInvoiceAsync(
                    settlementId, coworking.Id, pdfStream, pdfFile.FileName,
                    xmlStream, xmlFileName, invoiceNumber, invoiceAmount, partnerNotes);
                if (invoice.AutoValidationPassed == true)
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceSentValidationOk;
                }
                else if (invoice.AutoValidationPassed == false)
                {
                    TempData[TempDataKeys.Warning] = WarningMessages.InvoiceSentValidationIssues(invoice.AutoValidationMessage);
                }
                else
                {
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.InvoiceSentPendingValidation;
                }
                return RedirectToAction(ActionNames.MonthlySettlements);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao enviar nota fiscal: {ex.Message}";
                return RedirectToAction(ActionNames.MonthlySettlements);
            }
        }
        private async Task<Coworking?> GetCurrentCoworkingAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return null;
            var userId = int.Parse(userIdClaim.Value);
            return await _context.Coworkings
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }
        #endregion
    }
}