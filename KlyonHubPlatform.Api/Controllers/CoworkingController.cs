using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.IO;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = "CoworkingPartner,Administrator")]
    public class CoworkingController : Controller
    {
        private readonly ICoworkingService _coworkingService;
        private readonly ICommissionService _commissionService;
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly IAuditService _auditService;
        private readonly IReservationTransferService _reservationTransferService;
        private readonly ISystemConfigurationService _systemConfig;
        private readonly ISefazService _sefazService;
        private readonly IMonthlySettlementService _monthlySettlementService;

        public CoworkingController(
            ICoworkingService coworkingService,
            ICommissionService commissionService,
            IPaymentService paymentService,
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            IAddressDeclarationService addressDeclarationService,
            IAuditService auditService,
            IReservationTransferService reservationTransferService,
            ISystemConfigurationService systemConfig,
            ISefazService sefazService,
            IMonthlySettlementService monthlySettlementService)
        {
            _coworkingService = coworkingService;
            _commissionService = commissionService;
            _paymentService = paymentService;
            _context = context;
            _storageFactory = storageFactory;
            _addressDeclarationService = addressDeclarationService;
            _auditService = auditService;
            _reservationTransferService = reservationTransferService;
            _systemConfig = systemConfig;
            _sefazService = sefazService;
            _monthlySettlementService = monthlySettlementService;
        }

        private async Task RefreshUserPrincipalAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName)
            };

            var tokenClaim = User.FindFirst("Token");
            if (tokenClaim != null)
            {
                claims.Add(new Claim("Token", tokenClaim.Value));
            }

            foreach (var roleClaim in User.FindAll(ClaimTypes.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            }

            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync("Cookies", claimsPrincipal);
            HttpContext.Session.SetString("UserName", user.FullName);
        }

        private async Task<long> GetMaxPhotoSizeBytesAsync()
        {
            var mb = await _systemConfig.GetValueAsync("Upload.MaxPhotoSizeMB");
            if (int.TryParse(mb, out var n) && n > 0) return n * 1024L * 1024L;
            return 5 * 1024L * 1024L;
        }

        /// <summary>Se o cadastro está Aprovado, qualquer alteração retorna para Em análise.</summary>
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
                alerts.Add(new { Type = "danger", Icon = "bi-exclamation-triangle-fill", Title = "Cadastro Aguardando Ajuste", Message = !string.IsNullOrEmpty(coworkingEntity.RejectionReason) ? coworkingEntity.RejectionReason : "Seu cadastro foi reprovado e requer ajustes. Verifique os itens indicados e reenvie para análise.", ActionUrl = Url.Action("Profile", "Coworking"), ActionText = "Ver Perfil" });
            }
            var rejectedDocs = await _context.CoworkingDocuments.Where(d => d.CoworkingId == coworkingEntity.Id && d.Status == CoworkingDocumentStatus.Rejected).OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).ToListAsync();
            foreach (var doc in rejectedDocs)
            {
                var docTypeName = GetDocumentTypeDisplayName(doc.Type);
                alerts.Add(new { Type = "danger", Icon = "bi-file-earmark-x-fill", Title = $"Documento Rejeitado: {docTypeName}", Message = !string.IsNullOrEmpty(doc.RejectionReason) ? doc.RejectionReason : "Este documento foi rejeitado e requer correção.", ActionUrl = Url.Action("Profile", "Coworking"), ActionText = "Ver Documentos" });
            }
            var pendingDocs = await _context.CoworkingDocuments.Where(d => d.CoworkingId == coworkingEntity.Id && d.Status == CoworkingDocumentStatus.Pending).CountAsync();
            if (pendingDocs > 0) alerts.Add(new { Type = "info", Icon = "bi-file-earmark-check", Title = "Documentos Em Análise", Message = $"{pendingDocs} documento(s) aguardando análise administrativa.", ActionUrl = Url.Action("Profile", "Coworking"), ActionText = "Ver Documentos" });
            var pendingNf = 0;
            try
            {
                // Contar pagamentos que não têm NF aprovada (Emitida ou Importada).
                // Só conta quando o contrato teve pagamento realizado com sucesso e está ativo (mesmo critério da tela Emitir NF).
                pendingNf = await _context.Payments
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id) ||
                                (p.Reservation != null && p.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id))
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) ||
                                (p.Reservation != null))
                    .Where(p => !_context.FiscalNotes.Any(fn =>
                        fn.PaymentId == p.Id &&
                        fn.Type == "Servico" &&
                        (fn.Status == "Emitida" || fn.Status == "Importada")
                    ))
                    .CountAsync();
            }
            catch { }
            if (pendingNf > 0) alerts.Add(new { Type = "warning", Icon = "bi-receipt", Title = "NF de Serviço Não Emitidas", Message = $"{pendingNf} pagamento(s) aguardando emissão de NF. Você só recebe o repasse após emitir a nota fiscal.", ActionUrl = Url.Action("ServiceFiscalNotes", "Coworking"), ActionText = "Emitir NF" });
            var rejectedNf = 0;
            try
            {
                // Contar apenas notas rejeitadas que não têm uma NF aprovada (Emitida ou Importada) posteriormente para o mesmo PaymentId
                // Se existe uma NF aprovada para aquele PaymentId, não há pendência, mesmo que existam notas rejeitadas
                rejectedNf = await _context.FiscalNotes
                    .Where(fn => fn.CoworkingId == coworkingEntity.Id && fn.Type == "Servico" && fn.Status == "Rejeitada")
                    .Where(fn => !_context.FiscalNotes.Any(fn2 =>
                        fn2.PaymentId == fn.PaymentId &&
                        fn2.Type == "Servico" &&
                        (fn2.Status == "Emitida" || fn2.Status == "Importada") &&
                        fn2.Id != fn.Id
                    ))
                    .CountAsync();
            }
            catch { }
            if (rejectedNf > 0) alerts.Add(new { Type = "danger", Icon = "bi-x-circle-fill", Title = "NF Importadas Rejeitadas", Message = $"{rejectedNf} NF(s) importada(s) foram rejeitadas. Verifique o motivo e reenvie pela tela Emitir NF de Serviço.", ActionUrl = Url.Action("ServiceFiscalNotes", "Coworking"), ActionText = "Emitir / Importar NF" });
            var addrs = await _context.CoworkingAddresses.Where(a => a.CoworkingId == coworkingEntity.Id && (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending || a.CapacityApprovalStatus == CapacityApprovalStatus.Rejected)).ToListAsync();
            foreach (var a in addrs)
            {
                if (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending) alerts.Add(new { Type = "warning", Icon = "bi-clock-history", Title = "Capacidade Fiscal Aguardando Aprovação", Message = $"A alteração de capacidade do endereço {a.Address}, {a.City} está aguardando aprovação administrativa.", ActionUrl = Url.Action("MyAddresses", "Coworking"), ActionText = "Ver Endereços" });
                else alerts.Add(new { Type = "danger", Icon = "bi-x-circle-fill", Title = "Capacidade Fiscal Rejeitada", Message = $"A alteração de capacidade do endereço {a.Address}, {a.City} foi rejeitada.", ActionUrl = Url.Action("MyAddresses", "Coworking"), ActionText = "Ver Endereços" });
            }
            return alerts;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Carregar coworking com endereços
            var coworkingWithAddresses = await _context.Coworkings
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.Id == coworkingEntity.Id);

            // Estatísticas
            var contracts = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .ToListAsync();

            var commissions = await _commissionService.GetPendingCommissionsAsync(coworkingEntity.Id);
            // Calcular valor creditado (TotalPaymentAmount - Amount da comissão)
            var totalPending = commissions.Sum(c => c.TotalPaymentAmount - c.Amount);

            // Contar pagamentos aguardando validação
            var pendingPaymentsCount = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .Where(p => p.Status == PaymentStatus.AwaitingValidation
                    && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .CountAsync();

            // Contar reservas pendentes
            var pendingReservationsCount = 0;
            try
            {
                // Contar interesses pendentes
                var pendingInterests = 0;
                try
                {
                    pendingInterests = await _context.Interests
                        .Include(i => i.CoworkingAddress)
                        .Where(i => i.CoworkingAddress.CoworkingId == coworkingEntity.Id && i.Status == "Pending")
                        .CountAsync();
                }
                catch
                {
                    // Tabela Interests pode não existir ainda
                }

                // Contar reservas confirmadas
                var confirmedReservations = await _context.Reservations
                    .Include(r => r.CoworkingAddress)
                    .Where(r => r.CoworkingAddress.CoworkingId == coworkingEntity.Id && r.Status == "Confirmed")
                    .CountAsync();

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
                // Contar pagamentos que não têm NF aprovada (Emitida ou Importada).
                // Só conta quando o contrato está ativo (mesmo critério da tela Emitir NF e do alerta NF de Serviço Não Emitidas).
                pendingNfCount = await _context.Payments
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id) ||
                                (p.Reservation != null && p.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id))
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) ||
                                (p.Reservation != null))
                    .Where(p => !_context.FiscalNotes.Any(fn =>
                        fn.PaymentId == p.Id &&
                        fn.Type == "Servico" &&
                        (fn.Status == "Emitida" || fn.Status == "Importada")
                    ))
                    .CountAsync();
            }
            catch { }

            // Verificar se há notificação de mudança de status
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
        public async Task<IActionResult> DismissStatusNotification()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity != null)
            {
                var coworkingToUpdate = await _context.Coworkings.FindAsync(coworkingEntity.Id);
                if (coworkingToUpdate != null)
                {
                    coworkingToUpdate.HasPendingStatusNotification = false;
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Dashboard");
        }

        /// <summary>Retorna as pendências administrativas com detalhes (ex.: lista de documentos em análise) para exibição em modal.</summary>
        [HttpGet]
        public async Task<IActionResult> GetPendencyDetails()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
                return Json(new { pendencies = Array.Empty<object>() });

            var pendencies = new List<object>();

            if (coworkingEntity.ApprovalStatus == CoworkingApprovalStatus.AwaitingAdjustment)
            {
                pendencies.Add(new
                {
                    type = "danger",
                    icon = "bi-exclamation-triangle-fill",
                    title = "Cadastro Aguardando Ajuste",
                    message = !string.IsNullOrEmpty(coworkingEntity.RejectionReason) ? coworkingEntity.RejectionReason : "Seu cadastro foi reprovado e requer ajustes. Verifique os itens indicados e reenvie para análise.",
                    actionUrl = Url.Action("Profile", "Coworking"),
                    actionText = "Ver Perfil",
                    details = (string[]?)null
                });
            }

            var rejectedDocs = await _context.CoworkingDocuments
                .Where(d => d.CoworkingId == coworkingEntity.Id && d.Status == CoworkingDocumentStatus.Rejected)
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .ToListAsync();
            foreach (var doc in rejectedDocs)
            {
                var docTypeName = GetDocumentTypeDisplayName(doc.Type);
                pendencies.Add(new
                {
                    type = "danger",
                    icon = "bi-file-earmark-x-fill",
                    title = $"Documento Rejeitado: {docTypeName}",
                    message = !string.IsNullOrEmpty(doc.RejectionReason) ? doc.RejectionReason : "Este documento foi rejeitado e requer correção.",
                    actionUrl = Url.Action("Profile", "Coworking") + "#documents",
                    actionText = "Ver Documentos",
                    details = (string[]?)null
                });
            }

            var pendingDocsList = await _context.CoworkingDocuments
                .Where(d => d.CoworkingId == coworkingEntity.Id && d.Status == CoworkingDocumentStatus.Pending)
                .ToListAsync();
            if (pendingDocsList.Count > 0)
            {
                var docTypeNames = pendingDocsList.Select(d => GetDocumentTypeDisplayName(d.Type)).Distinct().OrderBy(x => x).ToArray();
                pendencies.Add(new
                {
                    type = "info",
                    icon = "bi-file-earmark-check",
                    title = "Documentos Em Análise",
                    message = $"{pendingDocsList.Count} documento(s) aguardando análise administrativa.",
                    actionUrl = Url.Action("Profile", "Coworking") + "#documents",
                    actionText = "Ver Documentos",
                    details = docTypeNames
                });
            }

            int pendingNf = 0;
            try
            {
                pendingNf = await _context.Payments
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id) ||
                                (p.Reservation != null && p.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id))
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .Where(p => (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) || (p.Reservation != null))
                    .Where(p => !_context.FiscalNotes.Any(fn =>
                        fn.PaymentId == p.Id && fn.Type == "Servico" && (fn.Status == "Emitida" || fn.Status == "Importada")))
                    .CountAsync();
            }
            catch { }
            if (pendingNf > 0)
            {
                pendencies.Add(new
                {
                    type = "warning",
                    icon = "bi-receipt",
                    title = "NF de Serviço Não Emitidas",
                    message = $"{pendingNf} pagamento(s) aguardando emissão de NF. Você só recebe o repasse após emitir a nota fiscal.",
                    actionUrl = Url.Action("ServiceFiscalNotes", "Coworking"),
                    actionText = "Emitir NF",
                    details = (string[]?)null
                });
            }

            int rejectedNf = 0;
            try
            {
                rejectedNf = await _context.FiscalNotes
                    .Where(fn => fn.CoworkingId == coworkingEntity.Id && fn.Type == "Servico" && fn.Status == "Rejeitada")
                    .Where(fn => !_context.FiscalNotes.Any(fn2 =>
                        fn2.PaymentId == fn.PaymentId && fn2.Type == "Servico" &&
                        (fn2.Status == "Emitida" || fn2.Status == "Importada") && fn2.Id != fn.Id))
                    .CountAsync();
            }
            catch { }
            if (rejectedNf > 0)
            {
                pendencies.Add(new
                {
                    type = "danger",
                    icon = "bi-x-circle-fill",
                    title = "NF Importadas Rejeitadas",
                    message = $"{rejectedNf} NF(s) importada(s) foram rejeitadas. Verifique o motivo e reenvie pela tela Emitir NF de Serviço.",
                    actionUrl = Url.Action("ServiceFiscalNotes", "Coworking"),
                    actionText = "Emitir / Importar NF",
                    details = (string[]?)null
                });
            }

            var addrs = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingEntity.Id && (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending || a.CapacityApprovalStatus == CapacityApprovalStatus.Rejected))
                .ToListAsync();
            foreach (var a in addrs)
            {
                if (a.CapacityApprovalStatus == CapacityApprovalStatus.Pending)
                    pendencies.Add(new
                    {
                        type = "warning",
                        icon = "bi-clock-history",
                        title = "Capacidade Fiscal Aguardando Aprovação",
                        message = $"A alteração de capacidade do endereço {a.Address}, {a.City} está aguardando aprovação administrativa.",
                        actionUrl = Url.Action("MyAddresses", "Coworking"),
                        actionText = "Ver Endereços",
                        details = (string[]?)null
                    });
                else
                    pendencies.Add(new
                    {
                        type = "danger",
                        icon = "bi-x-circle-fill",
                        title = "Capacidade Fiscal Rejeitada",
                        message = $"A alteração de capacidade do endereço {a.Address}, {a.City} foi rejeitada.",
                        actionUrl = Url.Action("MyAddresses", "Coworking"),
                        actionText = "Ver Endereços",
                        details = (string[]?)null
                    });
            }

            return Json(new { pendencies });
        }

        private static string GetDocumentTypeDisplayName(CoworkingDocumentType type)
        {
            return type switch
            {
                CoworkingDocumentType.AddressCessionContract => "Contrato de Cessão de Endereço",
                CoworkingDocumentType.AddressProof => "Comprovante de Endereço",
                CoworkingDocumentType.CNPJCard => "Cartão CNPJ",
                CoworkingDocumentType.AddressAuthorization => "Declaração de Autorização",
                CoworkingDocumentType.UtilityBill => "Conta de Utilidade",
                _ => type.ToString()
            };
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            // Verificar se já existe um coworking cadastrado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var existingCoworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = existingCoworking.FirstOrDefault();

            if (coworkingEntity != null)
            {
                // Se já existe, redirecionar para o Profile para editar
                TempData["Info"] = $"Você já possui um coworking cadastrado: {coworkingEntity.Name}. Redirecionando para o perfil...";
                return RedirectToAction("Profile");
            }

            // Preencher o email do usuário logado automaticamente
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;

            // Buscar o FullName do usuário da tabela Users
            var user = await _context.Users.FindAsync(userId);
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
                // Se o model binding falhou, criar uma nova instância e fazer binding manual
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

                // Validar campos obrigatórios
                if (string.IsNullOrWhiteSpace(coworking.Name) || string.IsNullOrWhiteSpace(coworking.CNPJ))
                {
                    TempData["Error"] = "Nome e CNPJ são obrigatórios.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _context.Users.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }

                // Validar CNPJ
                var cnpjClean = coworking.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                {
                    TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _context.Users.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }

                // Verificar se o CNPJ já está cadastrado
                var existingCNPJ = await _context.Coworkings
                    .Where(c => c.CNPJ != null &&
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                    .FirstOrDefaultAsync();

                if (existingCNPJ != null)
                {
                    TempData["Error"] = $"O CNPJ {coworking.CNPJ} já está cadastrado no sistema.";
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    ViewBag.UserEmail = userEmail;
                    var user = await _context.Users.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)));
                    ViewBag.UserFullName = user?.FullName ?? string.Empty;
                    return View(coworking);
                }

                // Formatar CNPJ com máscara
                coworking.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                coworking.UserId = userId;
                coworking.IsActive = true;
                coworking.IsPartner = true;

                var createdCoworking = await _coworkingService.CreateAsync(coworking);
                createdCoworking.ApprovalStatus = CoworkingApprovalStatus.UnderReview;
                createdCoworking.SubmittedAt = DateTime.UtcNow;
                await _auditService.LogActionAsync(userId, "Submitted", "Coworking", createdCoworking.Id, null, "{\"ApprovalStatus\":\"UnderReview\"}");
                var maxPhotoBytes = await GetMaxPhotoSizeBytesAsync();
                var storageService = await _storageFactory.GetStorageServiceAsync();

                // Processar fotos
                if (Photos != null && Photos.Count > 0)
                {
                    var photoCount = 0;
                    foreach (var photo in Photos.Take(2)) // Máximo 2 fotos
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

                            _context.CoworkingPhotos.Add(coworkingPhoto);
                            photoCount++;
                        }
                    }
                }

                // Processar documentos obrigatórios
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

                        _context.CoworkingDocuments.Add(coworkingDocument);
                    }
                }

                // Validar se há horários de funcionamento configurados (apenas se EnableCoworkingMenus estiver habilitado)
                var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

                if (showCoworkingMenus)
                {
                    var operatingHours = await _context.CoworkingOperatingHours
                        .Where(h => h.CoworkingId == createdCoworking.Id && h.IsActive)
                        .CountAsync();

                    if (operatingHours == 0)
                    {
                        TempData["Warning"] = "Coworking cadastrado, mas é necessário configurar os horários de funcionamento no perfil para ativar reservas.";
                    }
                }

                await _context.SaveChangesAsync();

                // Atualizar o FullName do usuário na tabela Users com o nome do coworking
                var userToUpdate = await _context.Users.FindAsync(userId);
                if (userToUpdate != null && !string.IsNullOrWhiteSpace(coworking.Name))
                {
                    userToUpdate.FullName = coworking.Name;
                    userToUpdate.UpdatedAt = DateTime.UtcNow;
                    _context.Users.Update(userToUpdate);
                    await _context.SaveChangesAsync();
                    await RefreshUserPrincipalAsync(userToUpdate);
                }

                TempData["Success"] = "Coworking cadastrado com sucesso!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar coworking: {ex.Message}";
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                ViewBag.UserEmail = userEmail;
                return View(coworking ?? new Coworking());
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyAddresses(string? search = null, string? status = null, int? locationId = null, int page = 1, int pageSize = 15)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var query = _context.CoworkingAddresses
                .Include(a => a.Location)
                .Where(a => a.CoworkingId == coworkingEntity.Id)
                .AsQueryable();

            // Filtro de busca (endereço, cidade ou estado)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Address.Contains(search) ||
                    a.City.Contains(search) ||
                    a.State.Contains(search) ||
                    a.Neighborhood.Contains(search));
            }

            // Filtro por status (disponível/indisponível)
            if (!string.IsNullOrEmpty(status))
            {
                bool isAvailable = status == "available";
                query = query.Where(a => a.IsAvailable == isAvailable);
            }

            // Filtro por localidade
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(a => a.LocationId == locationId.Value);
            }

            // Ordenação
            query = query.OrderByDescending(a => a.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var addresses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.LocationId = locationId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            // Lista de localidades para o filtro
            var locations = await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.City)
                .ToListAsync();
            ViewBag.Locations = locations;

            // Verificar contratos ativos para cada endereço
            var addressIds = addresses.Select(a => a.Id).ToList();
            var activeContractsByAddress = await _context.Contracts
                .Where(c => addressIds.Contains(c.CoworkingAddressId) && c.Status == ContractStatus.Active)
                .GroupBy(c => c.CoworkingAddressId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.ActiveContractsByAddress = activeContractsByAddress;

            // Só pode adicionar endereço se ainda não tiver nenhum endereço fiscal cadastrado
            var hasFiscalAddress = await _context.CoworkingAddresses.AnyAsync(a => a.CoworkingId == coworkingEntity.Id);
            ViewBag.CanAddAddress = !hasFiscalAddress;

            return View(addresses);
        }

        [HttpGet]
        public async Task<IActionResult> AddAddress()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity != null)
            {
                var hasFiscalAddress = await _context.CoworkingAddresses.AnyAsync(a => a.CoworkingId == coworkingEntity.Id);
                if (hasFiscalAddress)
                {
                    TempData["Error"] = "Não é possível adicionar outro endereço. Você já possui um endereço fiscal cadastrado.";
                    return RedirectToAction("MyAddresses");
                }
            }

            // Verificar se o coworking tem o produto RoomRental ativo
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
                    // Se a tabela não existir, assume false
                    hasRoomRental = false;
                }
            }

            var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
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
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction("Register");
                }

                var hasFiscalAddress = await _context.CoworkingAddresses.AnyAsync(a => a.CoworkingId == coworkingEntity.Id);
                if (hasFiscalAddress)
                {
                    TempData["Error"] = "Não é possível adicionar outro endereço. Você já possui um endereço fiscal cadastrado.";
                    return RedirectToAction("MyAddresses");
                }

                // Validar se a localidade existe
                var location = await _context.Locations.FindAsync(locationId);
                if (location == null)
                {
                    TempData["Error"] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
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
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(locations);
                }

                // Validar preço base (não pode ser negativo)
                if (basePrice < 0)
                {
                    TempData["Error"] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
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
                    ViewBag.HasRoomRental = hasRoomRental;
                    return View(locations);
                }

                // Salas para aluguel são configuradas em Produtos > Aluguéis de Sala (CoworkingRoom)
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
                    IsAvailable = false, // Não disponível até aprovação
                    AddressStatus = AddressStatus.PendingApproval, // Aguardando aprovação
                    CurrentContracts = 0
                };

                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Endereço adicionado");
                await _coworkingService.CreateAddressAsync(coworkingAddress);

                // Inserir na tabela PartnerServices: CoworkingId, ServiceId (FISCAL_ADDRESS_OFFER), CreatedAt; restante null/0
                var fiscalAddressOfferService = await _context.Services
                    .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive);
                if (fiscalAddressOfferService != null)
                {
                    var alreadyHasService = await _context.PartnerServices
                        .AnyAsync(ps => ps.CoworkingId == coworkingEntity.Id && ps.ServiceId == fiscalAddressOfferService.Id);
                    if (!alreadyHasService)
                    {
                        _context.PartnerServices.Add(new PartnerService
                        {
                            CoworkingId = coworkingEntity.Id,
                            ServiceId = fiscalAddressOfferService.Id,
                            Percentage = 0,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["Success"] = "Endereço cadastrado com sucesso!";
                return RedirectToAction("MyAddresses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar endereço: {ex.Message}";
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
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
        public async Task<IActionResult> EditAddress(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "ID do endereço inválido.";
                return RedirectToAction("MyAddresses");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction("Register");
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CapacityChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == id.Value && a.CoworkingId == coworkingEntity.Id);

            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction("MyAddresses");
            }

            // Verificar se o coworking tem o produto RoomRental ativo
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
                // Se a tabela não existir, assume false
                hasRoomRental = false;
            }

            // Verificar se há contratos ativos para este endereço
            var hasActiveContracts = await _context.Contracts
                .AnyAsync(c => c.CoworkingAddressId == address.Id && c.Status == ContractStatus.Active);

            ViewBag.HasActiveContracts = hasActiveContracts;

            var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
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
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction("Register");
                }

                // Buscar o endereço existente com relacionamentos
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.CapacityChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);

                if (existingAddress == null)
                {
                    TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction("MyAddresses");
                }

                // Verificar se há contratos ativos - não permitir edição se houver
                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => c.CoworkingAddressId == existingAddress.Id && c.Status == ContractStatus.Active);

                if (hasActiveContracts)
                {
                    TempData["Error"] = "Não é possível editar um endereço que possui contrato(s) ativo(s).";
                    return RedirectToAction("MyAddresses");
                }

                // Validar se a localidade existe
                var location = await _context.Locations.FindAsync(locationId);
                if (location == null)
                {
                    TempData["Error"] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
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

                // Interpretar Preço Base manualmente (vírgula ou ponto) para evitar zerar por cultura do model binder
                var basePriceStr = Request.Form["BasePrice"].ToString().Trim().Replace(",", ".");
                if (!decimal.TryParse(basePriceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var basePrice))
                    basePrice = existingAddress.BasePrice;

                // Validar preço base (não pode ser negativo)
                if (basePrice < 0)
                {
                    TempData["Error"] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
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

                // Validar maxContracts (não pode ser menor que currentContracts)
                if (maxContracts < existingAddress.CurrentContracts)
                {
                    TempData["Error"] = $"O máximo de contratos não pode ser menor que o número de contratos ativos ({existingAddress.CurrentContracts}).";
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

                // Atualizar o endereço (salas para aluguel em Produtos > Aluguéis de Sala)
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
                existingAddress.IsAvailable = false; // Não disponível até nova aprovação
                existingAddress.AddressStatus = AddressStatus.PendingApproval; // Volta para pendente após atualização
                existingAddress.UpdatedAt = DateTime.UtcNow;

                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Endereço atualizado");
                await _coworkingService.UpdateAddressAsync(existingAddress);

                TempData["Success"] = "Endereço atualizado com sucesso!";
                return RedirectToAction("MyAddresses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar endereço: {ex.Message}";
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == id);
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
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
        public async Task<IActionResult> CardPrices(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "ID do endereço inválido.";
                return RedirectToAction("MyAddresses");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction("Register");
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CardPriceChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == id.Value && a.CoworkingId == coworkingEntity.Id);
            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction("MyAddresses");
            }

            return View(address);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CardPrices(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("MyAddresses");
                }

                var address = await _context.CoworkingAddresses
                    .Include(a => a.CardPriceChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);
                if (address == null)
                {
                    TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction("MyAddresses");
                }

                // Não permite nova solicitação se já existe uma pendente
                if (address.CardPricesApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Já existe uma solicitação de alteração de preços no cartão pendente para este endereço. Aguarde a aprovação administrativa.";
                    return RedirectToAction("CardPrices", new { id });
                }

                decimal? cardPrice1x = decimal.TryParse(Request.Form["CardPrice1x"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1) ? v1 : (decimal?)null;
                decimal? cardPriceMonthly = decimal.TryParse(Request.Form["CardPriceMonthly"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2) ? v2 : (decimal?)null;
                decimal? cardPrice12x = decimal.TryParse(Request.Form["CardPrice12x"].ToString().Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v3) ? v3 : (decimal?)null;
                if (cardPrice1x.HasValue && cardPrice1x.Value < 0) cardPrice1x = null;
                if (cardPriceMonthly.HasValue && cardPriceMonthly.Value < 0) cardPriceMonthly = null;
                if (cardPrice12x.HasValue && cardPrice12x.Value < 0) cardPrice12x = null;

                // Cria solicitação para aprovação administrativa — os preços só são exibidos aos clientes após aprovação
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

                TempData["Success"] = "Solicitação de alteração de preços no cartão enviada com sucesso! Os valores só serão exibidos aos clientes após aprovação administrativa.";
                return RedirectToAction("MyAddresses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao salvar: {ex.Message}";
                var address = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .Include(a => a.CardPriceChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id);
                return address != null ? View(address) : RedirectToAction("MyAddresses");
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditMaxContracts(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "ID do endereço inválido.";
                return RedirectToAction("MyAddresses");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction("Register");
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.MaxContractsChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == id.Value && a.CoworkingId == coworkingEntity.Id);

            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction("MyAddresses");
            }

            // Verificar se há contratos ativos - necessário para editar MaxContracts
            var hasActiveContracts = await _context.Contracts
                .AnyAsync(c => c.CoworkingAddressId == address.Id && c.Status == ContractStatus.Active);

            if (!hasActiveContracts)
            {
                TempData["Error"] = "Esta funcionalidade está disponível apenas para endereços com contratos ativos. Use a edição normal do endereço.";
                return RedirectToAction("MyAddresses");
            }

            ViewBag.Address = address;
            return View(address);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMaxContracts(int id, int maxContracts, string? justification)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction("Register");
                }

                // Buscar o endereço existente
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.MaxContractsChangeRequests)
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);

                if (existingAddress == null)
                {
                    TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction("MyAddresses");
                }

                // Verificar se há contratos ativos
                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => c.CoworkingAddressId == existingAddress.Id && c.Status == ContractStatus.Active);

                if (!hasActiveContracts)
                {
                    TempData["Error"] = "Esta funcionalidade está disponível apenas para endereços com contratos ativos.";
                    return RedirectToAction("MyAddresses");
                }

                // Validar que o novo valor seja pelo menos 20% maior que o atual
                var minRequired = (int)Math.Ceiling(existingAddress.MaxContracts * 1.2m);
                if (maxContracts < minRequired)
                {
                    TempData["Error"] = $"O novo valor de contratos máximos ({maxContracts}) deve ser pelo menos 20% maior que o valor atual ({existingAddress.MaxContracts}). O valor mínimo permitido é {minRequired}.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Validar que não seja menor que contratos ativos
                if (maxContracts < existingAddress.CurrentContracts)
                {
                    TempData["Error"] = $"O máximo de contratos não pode ser menor que o número de contratos ativos ({existingAddress.CurrentContracts}).";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Verificar se já existe solicitação pendente
                if (existingAddress.MaxContractsApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    TempData["Error"] = "Já existe uma solicitação de alteração de quantidade máxima pendente para este endereço.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Validar justificativa
                if (string.IsNullOrWhiteSpace(justification))
                {
                    TempData["Error"] = "A justificativa é obrigatória para solicitar alteração de quantidade máxima.";
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Criar solicitação de alteração
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

                // Atualizar status do endereço
                existingAddress.RequestedMaxContracts = maxContracts;
                existingAddress.MaxContractsApprovalStatus = CapacityApprovalStatus.Pending;
                existingAddress.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                TempData["Success"] = "Solicitação de alteração de quantidade máxima enviada com sucesso! Aguardando aprovação administrativa.";
                return RedirectToAction("MyAddresses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao processar solicitação: {ex.Message}";
                var existingAddress = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == id);
                ViewBag.Address = existingAddress;
                return View(existingAddress);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestCapacityChange([FromBody] CapacityChangeRequestDto request)
        {
            try
            {
                if (request == null || request.AddressId <= 0 || request.RequestedCapacity <= 0)
                {
                    return Json(new { success = false, message = "Dados inválidos." });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    return Json(new { success = false, message = "Coworking não encontrado." });
                }

                var address = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.CoworkingId == coworkingEntity.Id);

                if (address == null)
                {
                    return Json(new { success = false, message = "Endereço não encontrado ou você não tem permissão para editá-lo." });
                }

                // Validar capacidade solicitada
                if (request.RequestedCapacity < address.CurrentContracts)
                {
                    return Json(new { success = false, message = $"A capacidade solicitada ({request.RequestedCapacity}) não pode ser menor que o número de contratos ativos ({address.CurrentContracts})." });
                }

                // Verificar se já existe solicitação pendente
                if (address.CapacityApprovalStatus == CapacityApprovalStatus.Pending)
                {
                    return Json(new { success = false, message = "Já existe uma solicitação de alteração de capacidade pendente para este endereço." });
                }

                // Criar solicitação de alteração
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

                // Atualizar status do endereço
                address.RequestedFiscalCapacity = request.RequestedCapacity;
                address.CapacityApprovalStatus = CapacityApprovalStatus.Pending;
                address.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Solicitação de alteração de capacidade enviada com sucesso!" });
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
            int pageSize = 15)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Query base: contratos cujo endereço pertence ao coworking do parceiro
            var query = _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s.Payments)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .AsQueryable();

            // Filtro de status: se informado, filtra; se "Todos" (vazio), mostra todos os contratos
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, out var contractStatus))
            {
                query = query.Where(c => c.Status == contractStatus);
            }

            // Filtro de busca (cliente, endereço, cidade, estado, número do contrato)
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

            // Filtro por localidade
            if (locationId.HasValue && locationId.Value > 0)
            {
                query = query.Where(c => c.CoworkingAddress != null && c.CoworkingAddress.LocationId == locationId.Value);
            }

            // Filtro por nome do cliente
            if (!string.IsNullOrEmpty(clientName))
            {
                query = query.Where(c => c.Client != null &&
                    (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(clientName) ||
                     c.Client.ContactName != null && c.Client.ContactName.Contains(clientName)));
            }

            // Filtro por data de início (de)
            if (startDateFrom.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDateFrom.Value);
            }

            // Filtro por data de início (até)
            if (startDateTo.HasValue)
            {
                query = query.Where(c => c.StartDate <= startDateTo.Value);
            }

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Ordenação e paginação
            var contracts = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ViewBag para manter os filtros
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

            // Lista de localidades para o filtro
            var locations = await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.City)
                .ThenBy(l => l.State)
                .ToListAsync();
            ViewBag.Locations = locations;

            return View(contracts);
        }

        [HttpGet]
        public async Task<IActionResult> ContractDetails(int id, int timelinePage = 1, int timelinePageSize = 5)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Dashboard");
            }

            // Buscar contrato com todos os relacionamentos
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
                TempData["Error"] = "Contrato não encontrado ou você não tem permissão para visualizá-lo.";
                return RedirectToAction("ActiveContracts");
            }

            // Buscar AddressDeclarations
            var declarations = await _context.AddressDeclarations
                .Where(d => d.ContractId == contract.Id)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            // Buscar histórico de pagamentos ordenado
            var payments = new List<Payment>();
            var historiesByPaymentId = new Dictionary<int, List<PaymentHistory>>();

            if (contract.Subscription != null)
            {
                payments = await _context.Payments
                    .Where(p => p.SubscriptionId == contract.Subscription.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                // Buscar histórico de validação dos pagamentos
                var paymentIds = payments.Select(p => p.Id).ToList();
                if (paymentIds.Any())
                {
                    var paymentHistories = await _context.PaymentHistories
                        .Where(ph => paymentIds.Contains(ph.PaymentId))
                        .Include(ph => ph.ActionByUser)
                        .ToListAsync();

                    // Agrupar históricos por PaymentId
                    historiesByPaymentId = paymentHistories
                        .GroupBy(ph => ph.PaymentId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
            }

            // Criar timeline do fluxo
            var timeline = new List<object>();

            string ContractStatusPt(ContractStatus s) => s switch
            {
                ContractStatus.Active => "Ativo",
                ContractStatus.Suspended => "Suspenso",
                ContractStatus.PendingPayment => "Aguardando Pagamento",
                ContractStatus.PendingCancellation => "Cancelamento Pendente",
                ContractStatus.Cancelled => "Cancelado",
                ContractStatus.Expired => "Expirado",
                _ => s.ToString()
            };
            string PaymentStatusPt(PaymentStatus s) => s switch
            {
                PaymentStatus.Pending => "Aguardando",
                PaymentStatus.Processing => "Processando",
                PaymentStatus.AwaitingValidation => "Aguardando Validação",
                PaymentStatus.Paid => "Pago",
                PaymentStatus.Failed => "Falhou",
                PaymentStatus.Refunded => "Reembolsado",
                PaymentStatus.Cancelled => "Cancelado",
                _ => s.ToString()
            };
            string PaymentMethodPt(PaymentMethod m) => m switch
            {
                PaymentMethod.PIX => "PIX",
                PaymentMethod.CreditCard => "Cartão",
                PaymentMethod.BankTransfer => "Boleto",
                _ => m.ToString()
            };

            // Adicionar criação do contrato
            timeline.Add(new
            {
                Date = contract.CreatedAt,
                Type = "Criação",
                Title = "Contrato Criado",
                Description = $"Contrato {contract.ContractNumber} foi criado",
                Icon = "bi-file-earmark-plus",
                Color = "primary"
            });

            // Adicionar mudanças de status
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
                    Color = "info"
                });
            }

            // Adicionar pagamentos
            foreach (var payment in payments)
            {
                timeline.Add(new
                {
                    Date = payment.CreatedAt,
                    Type = "Pagamento",
                    Title = $"Pagamento de R$ {payment.Amount:N2}",
                    Description = $"Status: {PaymentStatusPt(payment.Status)} - Método: {PaymentMethodPt(payment.Method)}",
                    Icon = "bi-cash-coin",
                    Color = payment.Status == PaymentStatus.Paid ? "success" : "warning",
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
                        Color = "success",
                        Payment = payment
                    });
                }

                // Adicionar histórico de validação. Apenas Approved, Cancelled, ReceiptUploaded e Rejected entram na timeline.
                // ContractCreated não entra para não duplicar "Contrato Criado". "Pagamento Reprovado" só quando Action == "Rejected".
                if (historiesByPaymentId.ContainsKey(payment.Id))
                {
                    foreach (var history in historiesByPaymentId[payment.Id].OrderBy(h => h.ActionAt))
                    {
                        if (history.Action == "ContractCreated")
                            continue;

                        string title, description, icon, color;
                        if (history.Action == "Approved")
                        {
                            title = "Pagamento Aprovado";
                            description = $"Aprovado por {history.ActionByUser?.FullName ?? "Admin"}";
                            icon = "bi-check-circle";
                            color = "success";
                        }
                        else if (history.Action == "Cancelled")
                        {
                            title = "Pagamento Cancelado";
                            description = history.RejectionReason ?? "Cancelado (outro contrato ativado)";
                            icon = "bi-x-circle";
                            color = "secondary";
                        }
                        else if (history.Action == "ReceiptUploaded")
                        {
                            title = "Comprovante enviado";
                            description = "Cliente enviou o comprovante de pagamento. Aguardando validação.";
                            icon = "bi-file-earmark-arrow-up";
                            color = "info";
                        }
                        else if (history.Action == "Rejected")
                        {
                            title = "Pagamento Reprovado";
                            description = $"Reprovado por {history.ActionByUser?.FullName ?? "Admin"}: {history.RejectionReason}";
                            icon = "bi-x-circle";
                            color = "danger";
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

            // Adicionar declarações
            foreach (var declaration in declarations)
            {
                timeline.Add(new
                {
                    Date = declaration.CreatedAt,
                    Type = "Documento",
                    Title = $"Declaração {declaration.DeclarationNumber}",
                    Description = $"Versão {declaration.Version} gerada",
                    Icon = "bi-file-earmark-pdf",
                    Color = "info",
                    Declaration = declaration
                });
            }

            // Ordenar timeline por data
            timeline = timeline.OrderByDescending(t => ((dynamic)t).Date).ToList();

            // Paginação do histórico (timeline)
            var timelineTotal = timeline.Count;
            var timelineTotalPages = timelinePageSize > 0 ? (int)Math.Ceiling(timelineTotal / (double)timelinePageSize) : 1;
            if (timelinePage < 1) timelinePage = 1;
            if (timelinePage > timelineTotalPages) timelinePage = Math.Max(1, timelineTotalPages);
            var timelinePaged = timeline
                .Skip((timelinePage - 1) * timelinePageSize)
                .Take(timelinePageSize)
                .ToList();

            // Status de exibição do contrato: quando PendingPayment, refletir status do pagamento (Aguardando Pagamento / Aguardando Validação)
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
        public async Task<IActionResult> DownloadDeclaration(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction("Dashboard");
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
        public async Task<IActionResult> DownloadContractDocument(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null)
            {
                return RedirectToAction("Dashboard");
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
            int pageSize = 15)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            if (!string.Equals(enableCoworkingMenus?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Lista de endereços para o filtro (carregar antes para usar em caso de erro)
            var addresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingEntity.Id)
                .OrderBy(a => a.City)
                .ThenBy(a => a.Neighborhood)
                .ToListAsync();
            ViewBag.Addresses = addresses;

            // Buscar reservas confirmadas (tabela Reservations)
            var reservationsQuery = _context.Reservations
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .AsQueryable();

            // Aplicar filtros nas reservas confirmadas
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
                // Aplicar filtro de status apenas se for um status válido para Reservations
                // Status válidos: Confirmed, Completed, Cancelled
                if (status == "Confirmed" || status == "Completed" || status == "Cancelled")
                {
                    reservationsQuery = reservationsQuery.Where(r => r.Status == status);
                }
                // Se o status for de Interests (Pending, Contacted, etc), não mostrar Reservations
                else
                {
                    reservationsQuery = reservationsQuery.Where(r => false);
                }
            }

            var confirmedReservations = await reservationsQuery
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Buscar interesses (tabela Interests) - para compatibilidade
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
                    // Aplicar filtro de status apenas se for um status válido para Interests
                    // Status válidos: Pending, Contacted, Converted, Rejected
                    if (status == "Pending" || status == "Contacted" || status == "Converted" || status == "Rejected")
                    {
                        interestsQuery = interestsQuery.Where(i => i.Status == status);
                    }
                    // Se o status for de Reservations (Confirmed, Completed, etc), não mostrar Interests
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
                // Tabela Interests pode não existir ainda, continuar apenas com Reservations
            }

            // Combinar e ordenar todas as reservas usando Dictionary para garantir compatibilidade
            var allReservations = confirmedReservations.Select(r => new Dictionary<string, object>
            {
                ["Id"] = r.Id,
                ["Type"] = "Reservation",
                ["ReservationDate"] = r.ReservationDate,
                ["StartTime"] = r.StartTime ?? (object)DBNull.Value,
                ["EndTime"] = r.EndTime ?? (object)DBNull.Value,
                ["ClientName"] = r.Client.User.FullName ?? "",
                ["ClientEmail"] = r.Client.ContactEmail ?? r.Client.User.Email ?? "",
                ["ClientPhone"] = r.Client.ContactPhone ?? r.Client.User.Phone ?? "",
                ["Address"] = r.CoworkingAddress,
                ["Status"] = r.Status ?? "",
                ["CreatedAt"] = r.CreatedAt,
                ["RoomType"] = r.RoomType ?? "",
                ["TotalValue"] = (object?)r.TotalValue ?? DBNull.Value,
                ["ClientConfirmedAt"] = r.ClientConfirmedAt ?? (object)DBNull.Value,
                ["CoworkingConfirmedAt"] = r.CoworkingConfirmedAt ?? (object)DBNull.Value
            }).Concat(interests.Select(i => new Dictionary<string, object>
            {
                ["Id"] = i.Id,
                ["Type"] = "Interest",
                ["ReservationDate"] = i.AllocationDate ?? DateTime.MinValue,
                ["StartTime"] = i.StartTime ?? (object)DBNull.Value,
                ["EndTime"] = i.EndTime ?? (object)DBNull.Value,
                ["ClientName"] = $"{i.Name} {i.LastName}",
                ["ClientEmail"] = i.Email ?? "",
                ["ClientPhone"] = i.Phone ?? "",
                ["Address"] = i.CoworkingAddress,
                ["Status"] = i.Status ?? "",
                ["CreatedAt"] = i.CreatedAt,
                ["RoomType"] = (object?)null ?? DBNull.Value,
                ["TotalValue"] = DBNull.Value,
            })).OrderByDescending(x => ((DateTime)x["ReservationDate"]))
                .ThenByDescending(x => ((DateTime)x["CreatedAt"]))
                .ToList();

            // Paginação manual
            var totalItems = allReservations.Count;
            var totalPages = totalItems > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;

            // Garantir que a página não seja maior que o total de páginas
            if (page > totalPages && totalPages > 0)
            {
                page = totalPages;
            }

            var pagedReservations = allReservations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ViewBag para manter os filtros
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

            // Para compatibilidade, passar também como Model (interesses antigos)
            return View(interests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReservationStatus(int id, string status, string? notes = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Reservations");
            }

            var interest = await _context.Interests
                .Include(i => i.CoworkingAddress)
                .FirstOrDefaultAsync(i => i.Id == id && i.CoworkingAddress.CoworkingId == coworkingEntity.Id);

            if (interest == null)
            {
                TempData["Error"] = "Reserva não encontrada.";
                return RedirectToAction("Reservations");
            }

            interest.Status = status;
            interest.Notes = notes;
            interest.UpdatedAt = DateTime.UtcNow;

            if (status == "Contacted")
            {
                interest.ContactedByUserId = userId;
                interest.ContactedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Status da reserva atualizado com sucesso!";
            return RedirectToAction("Reservations");
        }

        /// <summary>
        /// Coworking confirma que o cliente compareceu e utilizou o espaço. Após dupla confirmação
        /// (cliente + coworking), o sistema repassa os créditos ao coworking e aplica a comissão da plataforma.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservationAttended(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();
            if (coworkingEntity == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Reservations"); }

            var r = await _context.Reservations
                .Include(x => x.CoworkingAddress)
                .FirstOrDefaultAsync(x => x.Id == id && x.CoworkingAddress != null && x.CoworkingAddress.CoworkingId == coworkingEntity.Id);
            if (r == null) { TempData["Error"] = "Reserva não encontrada."; return RedirectToAction("Reservations"); }
            if (string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) { TempData["Error"] = "Reserva cancelada."; return RedirectToAction("Reservations"); }
            if (string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase)) { TempData["Success"] = "Reserva já concluída e créditos repassados."; return RedirectToAction("Reservations"); }

            if (r.CoworkingConfirmedAt == null)
            {
                r.CoworkingConfirmedAt = DateTime.UtcNow;
                r.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            await _reservationTransferService.ExecuteTransferIfBothConfirmedAsync(r);
            TempData["Success"] = "Comparecimento confirmado. Após a confirmação do cliente, o pagamento será processado automaticamente.";
            return RedirectToAction("Reservations");
        }

        /// <summary>
        /// Relatório de receita de aluguel de salas: total, por tipo de sala (qual produto mais faturou) e por cliente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RoomRevenueReport(DateTime? dateFrom, DateTime? dateTo, int page = 1, int pageSize = 15)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) return RedirectToAction("Register");

            var q = _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress != null && r.CoworkingAddress.CoworkingId == cow.Id && r.Status != "Cancelled");

            if (dateFrom.HasValue)
                q = q.Where(r => r.ReservationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(r => r.ReservationDate <= dateTo.Value.Date.AddDays(1));

            var list = await q.ToListAsync();
            var listIds = list.Select(r => r.Id).ToList();

            // Buscar comissões e transfers (se as tabelas existirem)
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
                // Se as tabelas não existirem ainda (migrations não aplicadas), continua sem comissões/transfers
                // Isso permite que a página funcione mesmo sem essas informações
                commissions = new List<ReservationCommission>();
                transfers = new List<Transfer>();
            }

            string ClientDisplayName(Reservation r) => r.Client != null
                ? (r.Client.RazaoSocial ?? r.Client.NomeFantasia ?? r.Client.ContactName ?? "—")
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
                    ["DateFormatted"] = r.ReservationDate.ToString("dd/MM/yyyy"),
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
                        ["DateFormatted"] = r.ReservationDate.ToString("dd/MM/yyyy"),
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
        public async Task<IActionResult> Commissions()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var commissions = await _context.Commissions
                .Include(c => c.Payment)
                    .ThenInclude(p => p.Subscription)
                .Where(c => c.CoworkingId == coworkingEntity.Id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Buscar transferências relacionadas para verificar o status real
            var transfers = await _context.Transfers
                .Where(t => t.CoworkingId == coworkingEntity.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Criar um dicionário para mapear comissões para transferências
            var commissionTransferMap = new Dictionary<int, Transfer?>();

            // Primeiro, mapear usando os IDs armazenados no Description do Transfer (método mais confiável)
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
                                // Verificar se a comissão existe na lista
                                if (commissions.Any(c => c.Id == commissionId))
                                {
                                    commissionTransferMap[commissionId] = transfer;
                                }
                            }
                        }
                    }
                }
            }

            // Depois, usar heurística para comissões transferidas que não foram mapeadas (casos antigos)
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

                // Encontrar transferência que corresponde a este grupo
                var matchingTransfer = transfers
                    .Where(t =>
                        !commissionTransferMap.Values.Contains(t) && // Não usar transfers já mapeados
                        t.CreatedAt.Date == group.Key.Date &&
                        Math.Abs(t.CreatedAt.Hour - group.Key.Hour) <= 1 && // Dentro de 1 hora
                        Math.Abs(t.Amount - totalCreditado) < 0.01m) // Valor deve ser muito próximo (tolerância de 1 centavo)
                    .OrderBy(t => Math.Abs((t.CreatedAt - groupCommissions.First().TransferredAt.Value).TotalMinutes))
                    .FirstOrDefault();

                // Se não encontrou por valor exato, tentar por valor aproximado (pode haver taxas)
                if (matchingTransfer == null)
                {
                    matchingTransfer = transfers
                        .Where(t =>
                            !commissionTransferMap.Values.Contains(t) &&
                            t.CreatedAt.Date == group.Key.Date &&
                            Math.Abs(t.CreatedAt.Hour - group.Key.Hour) <= 1 &&
                            t.Amount >= totalCreditado * 0.95m && // Pelo menos 95% do valor
                            t.Amount <= totalCreditado * 1.05m) // No máximo 105% do valor
                        .OrderBy(t => Math.Abs((t.CreatedAt - groupCommissions.First().TransferredAt.Value).TotalMinutes))
                        .FirstOrDefault();
                }

                // Mapear todas as comissões do grupo para a mesma transferência
                foreach (var commission in groupCommissions)
                {
                    if (!commissionTransferMap.ContainsKey(commission.Id))
                    {
                        commissionTransferMap[commission.Id] = matchingTransfer;
                    }
                }
            }

            // Para transferências "Aguardando NF": verificar se a NF de serviço já foi emitida,
            // para exibir "NF Emitida" na tela e alinhar com ListServiceFiscalNotes e Admin/FiscalNotes.
            var paymentIds = commissions.Select(c => c.PaymentId).Distinct().ToList();
            var paymentIdsWithEmitidaNf = await _context.FiscalNotes
                .Where(fn => fn.PaymentId != null && paymentIds.Contains(fn.PaymentId.Value)
                    && fn.Type == "Servico" && (fn.Status == "Emitida" || fn.Status == "Importada"))
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
        public async Task<IActionResult> RequestTransfer()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Validar se os dados bancários estão cadastrados:
            // - Chave PIX preenchida OU
            // - Conta Corrente completa (Banco, Agência, Conta e Tipo de conta)
            var hasPixKey = !string.IsNullOrWhiteSpace(coworkingEntity.BankPixKey);
            var hasContaCorrente = !string.IsNullOrWhiteSpace(coworkingEntity.BankName) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAgency) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAccount) &&
                                  !string.IsNullOrWhiteSpace(coworkingEntity.BankAccountType);
            var hasBankData = hasPixKey || hasContaCorrente;

            if (!hasBankData)
            {
                TempData["Error"] = "Para solicitar repasse, é necessário cadastrar os dados bancários: Chave PIX ou Conta Corrente (Banco, Agência, Conta e Tipo de conta). <a href=\"" + Url.Action("Profile", "Coworking") + "\" class=\"alert-link\">Clique aqui para cadastrar seus dados bancários</a>.";
                return RedirectToAction("Dashboard");
            }

            var pendingCommissions = await _commissionService.GetPendingCommissionsAsync(coworkingEntity.Id);
            if (pendingCommissions.Count == 0)
            {
                TempData["Error"] = "Não há comissões pendentes para transferência";
                return RedirectToAction("Commissions");
            }

            try
            {
                var commissionIds = pendingCommissions.Select(c => c.Id).ToList();
                await _commissionService.CreateTransferAsync(coworkingEntity.Id, commissionIds);

                var totalCreditado = pendingCommissions.Sum(c => c.TotalPaymentAmount - c.Amount);
                TempData["Success"] = $"Repasse de R$ {totalCreditado:N2} solicitado com sucesso! O status do repasse pode ser acompanhado na tela de Meus Ganhos.";
                return RedirectToAction("Commissions");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocorreu um erro ao processar a solicitação de repasse. Por favor, tente novamente.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile(int partnersPage = 1, int partnersPageSize = 10, int photosPage = 1, int photosPageSize = 6)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var maxPhotoMb = 5;
            var mbStr = await _systemConfig.GetValueAsync("Upload.MaxPhotoSizeMB");
            if (int.TryParse(mbStr, out var n) && n > 0) maxPhotoMb = n;
            ViewBag.MaxPhotoSizeMB = maxPhotoMb;

            var coworkingWithDetails = await _context.Coworkings
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == coworkingEntity.Id);

            // Buscar o FullName do usuário da tabela Users para preencher o campo Name
            var user = await _context.Users.FindAsync(userId);
            ViewBag.UserFullName = user?.FullName ?? coworkingEntity.Name;

            // Carregar Products separadamente para evitar erro se a tabela não existir
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
                    // Se a tabela não existir, inicializa como lista vazia
                    coworkingWithDetails.Products = new List<Domain.Entities.CoworkingProduct>();
                }

                // Paginação dos sócios
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

                // Paginação das fotos
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
                // Se não houver detalhes, criar listas vazias
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

            // Carregar horários de funcionamento
            var operatingHours = await _context.CoworkingOperatingHours
                .Where(h => h.CoworkingId == (coworkingWithDetails ?? coworkingEntity).Id)
                .OrderBy(h => h.DayOfWeek)
                .ToListAsync();
            ViewBag.OperatingHours = operatingHours;

            // Carregar datas bloqueadas
            var blockedDates = await _context.BlockedDates
                .Where(b => b.CoworkingId == (coworkingWithDetails ?? coworkingEntity).Id)
                .OrderByDescending(b => b.BlockedDateValue)
                .ToListAsync();
            ViewBag.BlockedDates = blockedDates;

            // Email principal vem da tabela Users (somente leitura na tela)
            ViewBag.UserEmail = user?.Email ?? User.FindFirstValue(ClaimTypes.Email);

            var alerts = await BuildAdministrativeAlertsAsync(coworkingWithDetails ?? coworkingEntity);
            ViewBag.AdministrativeAlerts = alerts;
            ViewBag.PendenciasTotal = alerts.Count;

            return View(coworkingWithDetails ?? coworkingEntity);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Coworking coworking)
        {
            try
            {
                // Verificar se é requisição AJAX (declarar uma única vez no início do método)
                var isAjaxRequest = Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var existingCoworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = existingCoworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Register");
                }

                var activeTab = Request.Form["activeTab"].ToString();

                // Guardar valores atuais dos dados críticos (para não retornar à análise só por descrição/contato/redes)
                var oldName = coworkingEntity.Name;
                var oldCNPJ = coworkingEntity.CNPJ;
                var oldTradeName = coworkingEntity.TradeName;

                // Atualizar apenas os campos da aba enviada (evita apagar endereço, contato, etc. ao salvar outra aba)
                switch (activeTab)
                {
                    case "basic":
                        // Validar que o nome é obrigatório
                        if (string.IsNullOrWhiteSpace(coworking.Name))
                        {
                            var errorMessage = "Nome do Coworking é obrigatório.";
                            TempData["Error"] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction("Profile");
                        }

                        // Validar CNPJ antes de atualizar
                        if (string.IsNullOrWhiteSpace(coworking.CNPJ))
                        {
                            var errorMessage = "CNPJ é obrigatório.";
                            TempData["Error"] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction("Profile");
                        }

                        var cnpjClean = coworking.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                        if (cnpjClean.Length != 14 || !cnpjClean.All(char.IsDigit))
                        {
                            var errorMessage = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
                            TempData["Error"] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction("Profile");
                        }

                        // Verificar se o CNPJ já está cadastrado em outro coworking
                        var existingCNPJ = await _context.Coworkings
                            .Where(c => c.Id != coworkingEntity.Id &&
                                c.CNPJ != null &&
                                c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                            .FirstOrDefaultAsync();

                        if (existingCNPJ != null)
                        {
                            var errorMessage = $"O CNPJ {coworking.CNPJ} já está cadastrado para outro coworking.";
                            TempData["Error"] = errorMessage;
                            if (isAjaxRequest)
                            {
                                return Json(new { success = false, error = errorMessage });
                            }
                            return RedirectToAction("Profile");
                        }

                        // Formatar CNPJ com máscara
                        coworking.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";

                        coworkingEntity.Name = coworking.Name;
                        coworkingEntity.CNPJ = coworking.CNPJ;
                        coworkingEntity.TradeName = coworking.TradeName;
                        coworkingEntity.Description = coworking.Description;
                        break;
                    case "contact":
                        // Email principal vem da tabela Users (não atualizar aqui)
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
                        // Chave PIX preenchida dispensa Banco, Agência, Conta e Tipo de Conta.
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
                        // Fallback: atualizar todas as seções (ex.: activeTab ausente em requisições antigas)
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

                // Determinar qual seção foi alterada para a mensagem de notificação
                string changeReason = activeTab switch
                {
                    "basic" => "Dados básicos atualizados",
                    "contact" => "Dados de contato atualizados",
                    "address" => "Endereço atualizado",
                    "bank" => "Dados bancários atualizados",
                    "social" => "Redes sociais atualizadas",
                    _ => "Perfil atualizado"
                };

                // Retornar para análise só quando dados críticos mudam (evita sumir da ChooseLocation ao editar descrição, contato, redes ou horários)
                bool shouldReturnToReview = activeTab switch
                {
                    "contact" => false,  // telefone/email não exigem nova análise
                    "social" => false,   // redes sociais não exigem nova análise
                    "basic" => coworkingEntity.Name != oldName || coworkingEntity.CNPJ != oldCNPJ || coworkingEntity.TradeName != oldTradeName,  // só Nome/CNPJ/TradeName
                    _ => true   // address, bank, default: retornar para análise
                };
                if (shouldReturnToReview)
                    await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, changeReason);

                await _coworkingService.UpdateAsync(coworkingEntity);

                // Atualizar o FullName do usuário na tabela Users com o nome do coworking (apenas se for a aba basic)
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

                // Validar se há horários de funcionamento configurados (obrigatório apenas se EnableCoworkingMenus estiver habilitado)
                var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

                if (showCoworkingMenus)
                {
                    var operatingHoursCount = await _context.CoworkingOperatingHours
                        .Where(h => h.CoworkingId == coworkingEntity.Id && h.IsActive)
                        .CountAsync();

                    if (operatingHoursCount == 0)
                    {
                        TempData["Warning"] = "É obrigatório configurar os horários de funcionamento. Configure na aba 'Horários de Funcionamento'.";
                        TempData["ActiveTab"] = "hours";
                    }
                }

                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData["ActiveTab"] = activeTab;
                }

                // Se for requisição AJAX, retornar JSON
                if (isAjaxRequest)
                {
                    return Json(new { success = true, message = "Perfil atualizado com sucesso!" });
                }

                TempData["Success"] = "Perfil atualizado com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                // Verificar se é requisição AJAX no catch também
                var isAjaxRequestCatch = Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

                var errorMessage = $"Erro ao atualizar dados: {ex.Message}";
                TempData["Error"] = errorMessage;
                var activeTab = Request.Form["activeTab"].ToString();
                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData["ActiveTab"] = activeTab;
                }

                // Se for requisição AJAX, retornar JSON com erro
                if (isAjaxRequestCatch)
                {
                    return Json(new { success = false, error = errorMessage });
                }

                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProducts(List<string>? productTypes)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Coworking não encontrado." });
                    }
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                // Remover produtos existentes
                var existingProducts = await _context.CoworkingProducts
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .ToListAsync();
                _context.CoworkingProducts.RemoveRange(existingProducts);

                // Adicionar novos produtos selecionados
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

                // Se for requisição AJAX, retornar JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var updatedProducts = await _context.CoworkingProducts
                        .Where(p => p.CoworkingId == coworkingEntity.Id && p.IsActive)
                        .Select(p => p.ProductType)
                        .ToListAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Produtos atualizados com sucesso!",
                        hasFiscalAddress = updatedProducts.Contains("FiscalAddress"),
                        hasRoomRental = updatedProducts.Contains("RoomRental")
                    });
                }

                TempData["Success"] = "Produtos atualizados com sucesso!";
                return RedirectToAction("Products");
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Erro ao atualizar produtos: {ex.Message}" });
                }
                TempData["Error"] = $"Erro ao atualizar produtos: {ex.Message}";
                return RedirectToAction("Products");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null)
            {
                return RedirectToAction("Register");
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
        public async Task<IActionResult> AddressRooms(int addressId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) return RedirectToAction("Register");
            var address = await _context.CoworkingAddresses
                .Include(a => a.Rooms)
                .ThenInclude(r => r.Photos)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado.";
                return RedirectToAction("Products");
            }
            ViewBag.Address = address;
            ViewBag.RoomTypes = RoomTypes.All;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoom(int addressId, string RoomType, string Name, int Capacity, int CreditsPerHour, IFormFile[]? RoomPhotos)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Products"); }
            var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null) { TempData["Error"] = "Endereço não encontrado."; return RedirectToAction("Products"); }
            if (!RoomTypes.All.Contains(RoomType)) { TempData["Error"] = "Tipo de sala inválido."; return RedirectToAction("AddressRooms", new { addressId }); }
            if (Capacity < 1 || CreditsPerHour < 0) { TempData["Error"] = "Capacidade deve ser >= 1 e créditos >= 0."; return RedirectToAction("AddressRooms", new { addressId }); }
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
            TempData["Success"] = "Sala adicionada com sucesso.";
            return RedirectToAction("AddressRooms", new { addressId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoom(int roomId, int addressId, string RoomType, string Name, int Capacity, int CreditsPerHour, bool IsActive, IFormFile[]? RoomPhotos)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Products"); }
            var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == cow.Id);
            if (address == null) { TempData["Error"] = "Endereço não encontrado."; return RedirectToAction("Products"); }
            var room = await _context.CoworkingRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.CoworkingAddressId == addressId);
            if (room == null) { TempData["Error"] = "Sala não encontrada."; return RedirectToAction("AddressRooms", new { addressId }); }
            if (!RoomTypes.All.Contains(RoomType)) { TempData["Error"] = "Tipo de sala inválido."; return RedirectToAction("AddressRooms", new { addressId }); }
            if (Capacity < 1 || CreditsPerHour < 0) { TempData["Error"] = "Capacidade deve ser >= 1 e créditos >= 0."; return RedirectToAction("AddressRooms", new { addressId }); }
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
            TempData["Success"] = "Sala atualizada com sucesso.";
            return RedirectToAction("AddressRooms", new { addressId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int roomId, int addressId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Products"); }
            var room = await _context.CoworkingRooms.Include(r => r.Photos).Include(r => r.CoworkingAddress).FirstOrDefaultAsync(r => r.Id == roomId && r.CoworkingAddress.CoworkingId == cow.Id);
            if (room == null) { TempData["Error"] = "Sala não encontrada."; return RedirectToAction("AddressRooms", new { addressId }); }
            var storage = await _storageFactory.GetStorageServiceAsync();
            foreach (var p in room.Photos)
            {
                try { await storage.DeleteFileAsync(p.FilePath); } catch { }
            }
            await ReturnToReviewIfApprovedAsync(cow, userId, "Sala excluída");
            _context.CoworkingRooms.Remove(room);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Sala excluída.";
            return RedirectToAction("AddressRooms", new { addressId });
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomPhoto(int photoId)
        {
            var photo = await _context.CoworkingRoomPhotos.Include(p => p.CoworkingRoom).ThenInclude(r => r.CoworkingAddress).FirstOrDefaultAsync(p => p.Id == photoId);
            if (photo == null) return NotFound();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var cow = (await _coworkingService.GetByUserIdAsync(userId)).FirstOrDefault();
            if (cow == null || photo.CoworkingRoom.CoworkingAddress.CoworkingId != cow.Id) { if (!User.IsInRole("Administrator")) return Forbid(); }
            var storage = await _storageFactory.GetStorageServiceAsync();
            var bytes = await storage.GetFileAsync(photo.FilePath);
            if (bytes == null) return NotFound();
            var lower = photo.FilePath.ToLower();
            var ct = lower.EndsWith(".png") ? "image/png" : lower.EndsWith(".gif") ? "image/gif" : lower.EndsWith(".webp") ? "image/webp" : "image/jpeg";
            return File(bytes, ct);
        }

        [HttpGet]
        public async Task<IActionResult> PendingPayments(string? search = null, string? method = null, DateTime? dateFrom = null, DateTime? dateTo = null, decimal? minValue = null, decimal? maxValue = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Buscar pagamentos aguardando validação dos contratos deste coworking
            var query = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Where(p => p.Status == PaymentStatus.AwaitingValidation
                    && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id);

            // Aplicar filtros
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

            // Passar valores dos filtros para a view
            ViewBag.Search = search;
            ViewBag.Method = method;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.MinValue = minValue;
            ViewBag.MaxValue = maxValue;

            return View(payments);
        }

        [HttpGet]
        public async Task<IActionResult> ValidatePayment(int id)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);
            if (payment == null)
            {
                TempData["Error"] = "Pagamento não encontrado.";
                return RedirectToAction("PendingPayments");
            }

            // Verificar se o pagamento pertence a um contrato deste coworking
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var contract = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);

            if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
            {
                TempData["Error"] = "Você não tem permissão para validar este pagamento.";
                return RedirectToAction("PendingPayments");
            }

            // Carregar dados completos
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
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("PendingPayments");
                }

                // Verificar se o pagamento pertence a um contrato deste coworking
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    return RedirectToAction("Register");
                }

                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);

                if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
                {
                    TempData["Error"] = "Você não tem permissão para validar este pagamento.";
                    return RedirectToAction("PendingPayments");
                }

                if (payment.Status != PaymentStatus.AwaitingValidation)
                {
                    TempData["Error"] = "Este pagamento já foi processado.";
                    return RedirectToAction("PendingPayments");
                }

                // Aprovar pagamento
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                payment.ValidatedBy = userId;
                payment.ValidatedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                _context.PaymentHistories.Add(new PaymentHistory
                {
                    PaymentId = paymentId,
                    Action = "Approved",
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                });
                await _context.SaveChangesAsync();

                // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                var previousPaidPayments = await _context.Payments
                    .Where(p => p.SubscriptionId == payment.SubscriptionId &&
                               p.Id != paymentId &&
                               p.Status == PaymentStatus.Paid)
                    .CountAsync();

                var isFirstPayment = previousPaidPayments == 0;

                // Guardar o status original antes de alterar
                var originalStatus = contract.Status;

                // Ativar contrato se estiver aguardando pagamento ou suspenso
                if (contract.Status == ContractStatus.PendingPayment || contract.Status == ContractStatus.Suspended)
                {
                    contract.Status = ContractStatus.Active;
                    contract.UpdatedAt = DateTime.UtcNow;

                    // Se for o primeiro pagamento, definir validade de 1 ano a partir do pagamento
                    if (isFirstPayment && contract.ExpiryDate == null)
                    {
                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                    }
                }

                await _context.SaveChangesAsync();

                // Quando um contrato for aprovado, cancelar os outros contratos pendentes do mesmo cliente (só pode ter um endereço fiscal ativo)
                // e também os pagamentos desses contratos (PaymentStatus.Cancelled) e registrar histórico com motivo claro
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
                                Action = "Cancelled",
                                RejectionReason = rejectionReasonPayment,
                                ActionBy = userId,
                                ActionAt = nowBr
                            });
                        }
                    }
                    if (others.Count > 0)
                        await _context.SaveChangesAsync();
                }

                // Atualizar contador de contratos do endereço apenas se for o primeiro pagamento
                // e o contrato estava aguardando pagamento (não suspenso, pois suspenso já foi contado)
                if (isFirstPayment && contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                {
                    contract.CoworkingAddress.CurrentContracts++;
                    if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                    {
                        contract.CoworkingAddress.IsAvailable = false;
                    }
                    await _coworkingService.UpdateAddressAsync(contract.CoworkingAddress);
                }

                // Criar comissão
                await _commissionService.CreateCommissionAsync(payment.Id);

                // Gerar declaração automaticamente se for o primeiro pagamento e o contrato tiver ExpiryDate
                if (isFirstPayment && contract.ExpiryDate.HasValue)
                {
                    try
                    {
                        await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log do erro mas não impede a aprovação do pagamento
                        // A declaração pode ser gerada manualmente depois se necessário
                        System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                    }
                }

                TempData["Success"] = "Pagamento aprovado e contrato ativado com sucesso!" +
                    (isFirstPayment && contract.ExpiryDate.HasValue ? " Declaração gerada automaticamente." : "");
                return RedirectToAction("PendingPayments");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao aprovar pagamento: {ex.Message}";
                return RedirectToAction("PendingPayments");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string validationNotes)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("PendingPayments");
                }

                // Verificar se o pagamento pertence a um contrato deste coworking
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    return RedirectToAction("Register");
                }

                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);

                if (contract == null || contract.CoworkingAddress.CoworkingId != coworkingEntity.Id)
                {
                    TempData["Error"] = "Você não tem permissão para validar este pagamento.";
                    return RedirectToAction("PendingPayments");
                }

                if (payment.Status != PaymentStatus.AwaitingValidation)
                {
                    TempData["Error"] = "Este pagamento já foi processado.";
                    return RedirectToAction("PendingPayments");
                }

                if (string.IsNullOrWhiteSpace(validationNotes))
                {
                    TempData["Error"] = "Por favor, informe a justificativa da reprovação.";
                    return RedirectToAction("ValidatePayment", new { id = paymentId });
                }

                // Reprovar pagamento
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = validationNotes;
                payment.ValidatedBy = userId;
                payment.ValidatedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Pagamento reprovado. O cliente foi notificado.";
                return RedirectToAction("PendingPayments");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao reprovar pagamento: {ex.Message}";
                return RedirectToAction("PendingPayments");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPaymentReceipt(int paymentId)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }

            // Verificar se o pagamento pertence a um contrato deste coworking
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
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
        public async Task<IActionResult> UploadPhotos(List<IFormFile> Photos)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Register");
                }

                if (Photos == null || Photos.Count == 0)
                {
                    TempData["Error"] = "Por favor, selecione pelo menos uma foto.";
                    return RedirectToAction("Profile");
                }

                // Verificar limite de fotos (máximo 2)
                var existingPhotosCount = await _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .CountAsync();

                var maxPhotos = 2;
                var photosToAdd = Photos.Take(maxPhotos - existingPhotosCount);
                if (!photosToAdd.Any())
                {
                    TempData["Error"] = $"Você já atingiu o limite máximo de {maxPhotos} fotos. Remova uma foto antes de adicionar outra.";
                    TempData["ActiveTab"] = "photos";
                    return RedirectToAction("Profile");
                }

                var maxBytes = await GetMaxPhotoSizeBytesAsync();
                var maxMb = (int)(maxBytes / (1024 * 1024));
                foreach (var p in photosToAdd)
                {
                    if (p.Length > 0 && p.Length > maxBytes)
                    {
                        TempData["Error"] = $"O arquivo \"{p.FileName}\" é muito grande. Tamanho máximo: {maxMb}MB. Ajuste em Admin > Configurações > Upload.MaxPhotoSizeMB.";
                        TempData["ActiveTab"] = "photos";
                        return RedirectToAction("Profile");
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
                            IsMain = existingPhotosCount == 0 && photoCount == existingPhotosCount, // Primeira foto como principal
                            DisplayOrder = photoCount,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.CoworkingPhotos.Add(coworkingPhoto);
                        photoCount++;
                    }
                }

                // Adicionar fotos retorna para análise (igual a adicionar/substituir documentos obrigatórios)
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Foto(s) adicionada(s)");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{photosToAdd.Count()} foto(s) adicionada(s) com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao fazer upload das fotos: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhoto(int photoId)
        {
            var photo = await _context.CoworkingPhotos
                .Include(p => p.Coworking)
                .FirstOrDefaultAsync(p => p.Id == photoId);

            if (photo == null)
            {
                return NotFound();
            }

            // Verificar se o coworking está ativo e é parceiro (fotos públicas no marketplace)
            // Se não for público, verificar permissões
            var isPublic = photo.Coworking.IsActive && photo.Coworking.IsPartner;

            if (!isPublic && User.Identity?.IsAuthenticated == true)
            {
                // Verificar se a foto pertence ao coworking do usuário logado OU se o usuário é cliente/administrador
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                // Permitir acesso se:
                // 1. É o dono do coworking
                // 2. É administrador
                // 3. É cliente (pode ver fotos de qualquer coworking para escolher endereços)
                var canAccess = User.IsInRole("Administrator") ||
                              (coworkingEntity != null && photo.CoworkingId == coworkingEntity.Id) ||
                              User.IsInRole("Client");

                if (!canAccess)
                {
                    return Forbid();
                }
            }
            else if (!isPublic)
            {
                // Se não for público e usuário não autenticado, negar acesso
                return Forbid();
            }

            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(photo.FilePath);

            if (fileBytes == null)
            {
                return NotFound();
            }

            // Detectar tipo de conteúdo baseado na extensão
            var contentType = "image/jpeg"; // padrão
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
        public async Task<IActionResult> SetMainPhoto(int photoId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var photo = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.CoworkingId == coworkingEntity.Id);

                if (photo == null)
                {
                    TempData["Error"] = "Foto não encontrada.";
                    return RedirectToAction("Profile");
                }

                // Remover principal de todas as fotos
                var allPhotos = await _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .ToListAsync();

                foreach (var p in allPhotos)
                {
                    p.IsMain = false;
                }

                // Definir nova principal
                photo.IsMain = true;
                await ReturnToReviewIfApprovedAsync(coworkingEntity, userId, "Foto principal alterada");
                await _context.SaveChangesAsync();

                TempData["Success"] = "Foto principal atualizada com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar foto principal: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int photoId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var photo = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.CoworkingId == coworkingEntity.Id);

                if (photo == null)
                {
                    TempData["Error"] = "Foto não encontrada.";
                    return RedirectToAction("Profile");
                }

                // Deletar arquivo do storage
                try
                {
                    var storageService = await _storageFactory.GetStorageServiceAsync();
                    await storageService.DeleteFileAsync(photo.FilePath);
                }
                catch
                {
                    // Continuar mesmo se não conseguir deletar o arquivo
                }

                // Apagar foto não retorna para análise (parceiro continua visível na ChooseLocation)
                _context.CoworkingPhotos.Remove(photo);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Foto excluída com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao excluir foto: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPartner(CoworkingPartner partner, IFormFile? partnerPhoto, bool IsPrincipal = false, bool IsContractSigner = false)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var maxBytes = await GetMaxPhotoSizeBytesAsync();
                var maxMb = (int)(maxBytes / (1024 * 1024));
                if (partnerPhoto != null && partnerPhoto.Length > 0 && partnerPhoto.Length > maxBytes)
                {
                    TempData["Error"] = $"A foto do sócio é muito grande. Tamanho máximo: {maxMb}MB.";
                    TempData["ActiveTab"] = "partners";
                    return RedirectToAction("Profile");
                }

                partner.CoworkingId = coworkingEntity.Id;
                partner.CreatedAt = DateTime.UtcNow;
                partner.IsPrincipal = IsPrincipal;
                partner.IsContractSigner = IsContractSigner;

                // Converter SharePercentage de string (pode vir com vírgula) para decimal
                if (Request.Form.ContainsKey("SharePercentage") && !string.IsNullOrWhiteSpace(Request.Form["SharePercentage"]))
                {
                    var sharePercentageStr = Request.Form["SharePercentage"].ToString().Trim().Replace(",", ".");
                    if (decimal.TryParse(sharePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sharePercentage))
                    {
                        // Validar range (0 a 100)
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

                TempData["Success"] = "Sócio adicionado com sucesso!";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar sócio: {ex.Message}";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePartner(CoworkingPartner partner, IFormFile? partnerPhoto, bool IsPrincipal = false, bool IsContractSigner = false)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var existingPartner = await _context.CoworkingPartners
                    .FirstOrDefaultAsync(p => p.Id == partner.Id && p.CoworkingId == coworkingEntity.Id);

                if (existingPartner == null)
                {
                    TempData["Error"] = "Sócio não encontrado.";
                    return RedirectToAction("Profile");
                }

                existingPartner.Name = partner.Name;
                existingPartner.CPF = partner.CPF;
                existingPartner.Email = partner.Email;
                existingPartner.Phone = partner.Phone;
                existingPartner.Role = partner.Role;
                existingPartner.IsPrincipal = IsPrincipal;
                existingPartner.IsContractSigner = IsContractSigner;
                existingPartner.UpdatedAt = DateTime.UtcNow;

                // Converter SharePercentage de string (pode vir com vírgula) para decimal
                if (Request.Form.ContainsKey("SharePercentage") && !string.IsNullOrWhiteSpace(Request.Form["SharePercentage"]))
                {
                    var sharePercentageStr = Request.Form["SharePercentage"].ToString().Trim().Replace(",", ".");
                    if (decimal.TryParse(sharePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sharePercentage))
                    {
                        // Validar range (0 a 100)
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
                    TempData["Error"] = $"A foto do sócio é muito grande. Tamanho máximo: {maxMbPartner}MB.";
                    TempData["ActiveTab"] = "partners";
                    return RedirectToAction("Profile");
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

                TempData["Success"] = "Sócio atualizado com sucesso!";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar sócio: {ex.Message}";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePartner(int partnerId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var partner = await _context.CoworkingPartners
                    .FirstOrDefaultAsync(p => p.Id == partnerId && p.CoworkingId == coworkingEntity.Id);

                if (partner == null)
                {
                    TempData["Error"] = "Sócio não encontrado.";
                    return RedirectToAction("Profile");
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

                TempData["Success"] = "Sócio excluído com sucesso!";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao excluir sócio: {ex.Message}";
                TempData["ActiveTab"] = "partners";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResubmitForApproval()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null) { TempData["Error"] = "Coworking não encontrado."; return RedirectToAction("Dashboard"); }
            if (cow.ApprovalStatus != CoworkingApprovalStatus.AwaitingAdjustment)
            {
                TempData["Error"] = "Apenas cadastros com status Aguardando ajuste podem ser reenviados.";
                return RedirectToAction("Profile");
            }
            cow.ApprovalStatus = CoworkingApprovalStatus.UnderReview;
            cow.SubmittedAt = DateTime.UtcNow;
            await _coworkingService.UpdateAsync(cow);
            await _auditService.LogActionAsync(userId, "ResubmitForApproval", "Coworking", cow.Id, "{\"ApprovalStatus\":\"AwaitingAdjustment\"}", "{\"ApprovalStatus\":\"UnderReview\"}");
            TempData["Success"] = "Cadastro reenviado para análise. Aguarde a aprovação do administrador.";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> GetPartnerPhoto(int partnerId)
        {
            var partner = await _context.CoworkingPartners.Include(p => p.Coworking).FirstOrDefaultAsync(p => p.Id == partnerId);
            if (partner == null || string.IsNullOrEmpty(partner.PhotoPath)) return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var cow = coworking.FirstOrDefault();
            if (cow == null || partner.CoworkingId != cow.Id)
            {
                if (!User.IsInRole("Administrator")) return Forbid();
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
        public async Task<IActionResult> UploadDocument(int documentType, IFormFile documentFile)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Register");
                }

                if (documentFile == null || documentFile.Length == 0)
                {
                    TempData["Error"] = "Por favor, selecione um arquivo.";
                    return RedirectToAction("Profile");
                }

                if (documentFile.Length > 10 * 1024 * 1024) // 10MB
                {
                    TempData["Error"] = "O arquivo excede o tamanho máximo de 10MB.";
                    return RedirectToAction("Profile");
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

                // Verificar se já existe um documento deste tipo
                var existingDoc = await _context.CoworkingDocuments
                    .FirstOrDefaultAsync(d => d.CoworkingId == coworkingEntity.Id && d.Type == docType);

                if (existingDoc != null)
                {
                    // Substituir documento existente
                    try
                    {
                        await storageService.DeleteFileAsync(existingDoc.FilePath);
                    }
                    catch
                    {
                        // Ignorar erro ao deletar arquivo antigo
                    }

                    existingDoc.FileName = documentFile.FileName;
                    existingDoc.FilePath = filePath;
                    existingDoc.UpdatedAt = DateTime.UtcNow;
                    existingDoc.IsValid = true;
                    existingDoc.Status = CoworkingDocumentStatus.Pending;
                    existingDoc.RejectionReason = null;

                    // Definir data de expiração para comprovante de endereço (90 dias)
                    if (docType == CoworkingDocumentType.AddressProof)
                    {
                        existingDoc.ExpiresAt = DateTime.UtcNow.AddDays(90);
                    }
                }
                else
                {
                    // Criar novo documento (Status = Em análise)
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
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao enviar documento: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            var document = await _context.CoworkingDocuments.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            // Verificar se o documento pertence ao coworking do usuário logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null || document.CoworkingId != coworkingEntity.Id)
            {
                if (!User.IsInRole("Administrator"))
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
        public async Task<IActionResult> SaveOperatingHours(int CoworkingId, IFormCollection form)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            if (!string.Equals(enableCoworkingMenus?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Profile");

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null || coworkingEntity.Id != CoworkingId)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var daysOfWeek = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };

                foreach (var day in daysOfWeek)
                {
                    var isActiveKey = $"IsActive_{day}";
                    var isActive = form.ContainsKey(isActiveKey) && form[isActiveKey].ToString() == "true";
                    var openingTimeStr = form.ContainsKey($"OpeningTime_{day}") ? form[$"OpeningTime_{day}"].ToString() : null;
                    var closingTimeStr = form.ContainsKey($"ClosingTime_{day}") ? form[$"ClosingTime_{day}"].ToString() : null;

                    // Converter strings de horário para TimeSpan
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
                TempData["Success"] = "Horários de funcionamento salvos com sucesso!";
                TempData["ActiveTab"] = "hours";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao salvar horários: {ex.Message}";
                TempData["ActiveTab"] = "hours";
            }

            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBlockedDate(int CoworkingId, DateTime BlockedDateValue, string Reason, bool BlocksFullDay, TimeSpan? StartTime, TimeSpan? EndTime)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            if (!string.Equals(enableCoworkingMenus?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Profile");

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null || coworkingEntity.Id != CoworkingId)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                // Verificar se já existe bloqueio para esta data
                var existing = await _context.BlockedDates
                    .FirstOrDefaultAsync(b => b.CoworkingId == CoworkingId && b.BlockedDateValue.Date == BlockedDateValue.Date);

                if (existing != null)
                {
                    TempData["Error"] = "Já existe um bloqueio para esta data.";
                    TempData["ActiveTab"] = "hours";
                    return RedirectToAction("Profile");
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

                TempData["Success"] = "Data bloqueada adicionada com sucesso!";
                TempData["ActiveTab"] = "hours";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar data bloqueada: {ex.Message}";
                TempData["ActiveTab"] = "hours";
            }

            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlockedDate(int id)
        {
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            if (!string.Equals(enableCoworkingMenus?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Profile");

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Profile");
                }

                var blockedDate = await _context.BlockedDates
                    .FirstOrDefaultAsync(b => b.Id == id && b.CoworkingId == coworkingEntity.Id);

                if (blockedDate == null)
                {
                    TempData["Error"] = "Data bloqueada não encontrada.";
                    TempData["ActiveTab"] = "hours";
                    return RedirectToAction("Profile");
                }

                _context.BlockedDates.Remove(blockedDate);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Data bloqueada removida com sucesso!";
                TempData["ActiveTab"] = "hours";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao remover data bloqueada: {ex.Message}";
                TempData["ActiveTab"] = "hours";
            }

            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> MyContract()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var partnerServices = await _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == coworkingEntity.Id)
                .ToListAsync();

            // Remover itens com Service nulo (dados órfãos) e ordenar por nome
            partnerServices = partnerServices
                .Where(ps => ps.Service != null)
                .OrderBy(ps => ps.Service!.Name)
                .ToList();

            // Verificar se os menus de coworking estão habilitados
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

            // Se os menus de coworking estiverem desabilitados, mostrar apenas "Ofertar Endereço Fiscal"
            if (!showCoworkingMenus)
            {
                partnerServices = partnerServices
                    .Where(ps => ps.Service != null && ps.Service.Code == FiscalAddressOffer)
                    .ToList();
            }

            return View(partnerServices);
        }

        [HttpGet]
        public async Task<IActionResult> Services()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Verificar se os menus de coworking estão habilitados
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

            // Serviços relacionados a coworking que devem ser filtrados se a flag estiver desabilitada
            var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();

            // Buscar todos os serviços disponíveis (excluir "Contratar Endereço Fiscal", que é apenas para clientes)
            var allServicesQuery = _context.Services
                .Where(s => s.IsActive && s.Code != FiscalAddressContracting);

            // Se a flag de coworking não estiver habilitada, filtrar os serviços de coworking
            if (!showCoworkingMenus)
            {
                allServicesQuery = allServicesQuery.Where(s => !coworkingServiceNames.Contains(s.Name));
            }

            var allServices = await allServicesQuery
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();

            // Buscar serviços já cadastrados pelo parceiro
            var partnerServicesQuery = _context.PartnerServices
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == coworkingEntity.Id);

            // Se a flag de coworking não estiver habilitada, filtrar os serviços de coworking
            if (!showCoworkingMenus)
            {
                partnerServicesQuery = partnerServicesQuery.Where(ps => !coworkingServiceNames.Contains(ps.Service.Name));
            }

            var partnerServices = await partnerServicesQuery.ToListAsync();

            // Não exibir "Contratar Endereço Fiscal" (serviço só do cliente) na tela do parceiro
            partnerServices = partnerServices.Where(ps => ps.Service?.Code != FiscalAddressContracting).ToList();

            var partnerServiceIds = partnerServices.Select(ps => ps.ServiceId).ToHashSet();

            // Verificar se há contratos ativos para desabilitar botão de remoção
            var coworkingAddresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingEntity.Id)
                .Select(a => a.Id)
                .ToListAsync();

            var hasActiveContracts = await _context.Contracts
                .AnyAsync(c => coworkingAddresses.Contains(c.CoworkingAddressId)
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended));

            // Verificar se há algum pagamento "Aguardando Validação" em contratos deste parceiro (desabilita Remover)
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
        public async Task<IActionResult> AddService(int serviceId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Services");
                }

                // Verificar se os menus de coworking estão habilitados
                var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
                var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

                // Serviços relacionados a coworking que devem ser filtrados se a flag estiver desabilitada
                var coworkingServiceNames = _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToList();

                // Verificar se o serviço existe e está ativo
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive);

                if (service == null)
                {
                    TempData["Error"] = "Serviço não encontrado ou inativo.";
                    return RedirectToAction("Services");
                }

                // Se a flag de coworking não estiver habilitada, não permitir adicionar serviços de coworking
                if (!showCoworkingMenus && coworkingServiceNames.Contains(service.Name))
                {
                    TempData["Error"] = "Este serviço não está disponível no momento.";
                    return RedirectToAction("Services");
                }

                // Verificar se já está cadastrado
                var existing = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingEntity.Id && ps.ServiceId == serviceId);

                if (existing != null)
                {
                    TempData["Error"] = "Este serviço já está cadastrado.";
                    return RedirectToAction("Services");
                }

                // Criar novo PartnerService
                // A percentagem será definida pelo administrador em contrato, inicialmente 0
                var partnerService = new PartnerService
                {
                    CoworkingId = coworkingEntity.Id,
                    ServiceId = serviceId,
                    Percentage = 0, // Será definido pelo admin em contrato
                    CreatedAt = DateTime.UtcNow
                };

                _context.PartnerServices.Add(partnerService);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Serviço '{service.Name}' adicionado com sucesso! A percentagem será definida em contrato pelo administrador.";
                return RedirectToAction("Services");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar serviço: {ex.Message}";
                return RedirectToAction("Services");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveService(int partnerServiceId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Services");
                }

                // Verificar se o PartnerService pertence ao coworking
                var partnerService = await _context.PartnerServices
                    .Include(ps => ps.Service)
                    .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId && ps.CoworkingId == coworkingEntity.Id);

                if (partnerService == null)
                {
                    TempData["Error"] = "Serviço não encontrado ou não pertence a este parceiro.";
                    return RedirectToAction("Services");
                }

                // Verificar se já existe uma solicitação pendente
                if (partnerService.RemovalRequestStatus == Domain.Core.Enums.RemovalRequestStatus.Pending)
                {
                    TempData["Info"] = "Já existe uma solicitação de remoção pendente para este serviço. Aguarde a aprovação do administrador.";
                    return RedirectToAction("Services");
                }

                // Verificar se há contratos ativos ou pagamento aguardando validação
                var coworkingAddresses = await _context.CoworkingAddresses
                    .Where(a => a.CoworkingId == coworkingEntity.Id)
                    .Select(a => a.Id)
                    .ToListAsync();

                var hasActiveContracts = await _context.Contracts
                    .AnyAsync(c => coworkingAddresses.Contains(c.CoworkingAddressId)
                        && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended));

                if (hasActiveContracts)
                {
                    TempData["Error"] = "Não é possível solicitar a remoção deste serviço enquanto houver contratos ativos. A solicitação só pode ser feita após o fim de todos os contratos.";
                    return RedirectToAction("Services");
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
                    TempData["Error"] = "Não é possível solicitar a remoção deste serviço enquanto houver cliente(s) com pagamento aguardando validação.";
                    return RedirectToAction("Services");
                }

                var serviceName = partnerService.Service.Name;

                // Criar solicitação de remoção ao invés de remover diretamente
                partnerService.RemovalRequestStatus = Domain.Core.Enums.RemovalRequestStatus.Pending;
                partnerService.RemovalRequestedAt = DateTimeHelper.GetBrazilianTime();
                partnerService.RemovalRejectionReason = null;
                partnerService.RemovalReviewedAt = null;
                partnerService.RemovalReviewedBy = null;
                partnerService.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                // Registrar no audit log
                await _auditService.LogActionAsync(userId, "ServiceRemovalRequested", "PartnerService", partnerService.Id,
                    null, $"{{\"ServiceName\":\"{serviceName}\",\"Status\":\"Pending\"}}");

                TempData["Success"] = $"Solicitação de remoção do serviço '{serviceName}' enviada com sucesso! Aguardando aprovação do administrador.";
                return RedirectToAction("Services");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao solicitar remoção do serviço: {ex.Message}";
                return RedirectToAction("Services");
            }
        }

        /// <summary>
        /// Lista pagamentos pendentes de emissão de NF de serviço
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ServiceFiscalNotes(int page = 1, int pageSize = 20, string? serviceType = null, string? client = null, string? contractNumber = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Buscar pagamentos relacionados a este coworking que ainda não têm NF de serviço aprovada.
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
                    fn.Type == "Servico" &&
                    (fn.Status == "Emitida" || fn.Status == "Importada")
                ))
                .AsQueryable();

            // Filtro: tipo de serviço
            if (!string.IsNullOrWhiteSpace(serviceType))
            {
                if (serviceType.Equals("EnderecoFiscal", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.SubscriptionId != null);
                else if (serviceType.Equals("ReservaSala", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.ReservationId != null);
            }

            // Filtro: código do contrato (apenas para pagamentos de assinatura)
            if (!string.IsNullOrWhiteSpace(contractNumber))
            {
                var contractNum = contractNumber.Trim();
                query = query.Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ContractNumber != null && p.Subscription.Contract.ContractNumber.Contains(contractNum));
            }

            // Filtro: cliente (nome, razão social, CNPJ, CPF)
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

        /// <summary>
        /// Retorna os dados completos de um pagamento (NF de serviço) para exibição em modal (JSON).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ServiceFiscalNoteDetail(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
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

        /// <summary>
        /// Emite NF de serviço para um pagamento específico
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueServiceFiscalNote(int paymentId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Parceiro não encontrado.";
                    return RedirectToAction("ServiceFiscalNotes");
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
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                // Verificar se o pagamento pertence a este coworking
                var belongsToCoworking = false;
                string state = "ES"; // Default

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
                    TempData["Error"] = "Este pagamento não pertence ao seu coworking.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                // Verificar se já existe NF emitida
                var existingNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.PaymentId == paymentId && fn.Type == "Servico" && fn.Status == "Emitida");

                if (existingNote != null)
                {
                    TempData["Error"] = "Já existe uma NF de serviço emitida para este pagamento.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                // Verificar se o estado tem integração
                if (!_sefazService.IsStateSupported(state))
                {
                    TempData["Error"] = $"Integração SEFAZ ainda não implementada para o estado {state}.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                // Buscar configuração SEFAZ do parceiro
                var sefazConfig = await _sefazService.GetCoworkingConfigurationAsync(coworkingEntity.Id, state, "Producao");
                if (sefazConfig == null)
                {
                    TempData["Error"] = $"Configuração SEFAZ não encontrada para o estado {state}. Configure em Configurações SEFAZ.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                // Criar registro de NF com valor total (com percentagem)
                var fiscalNote = new FiscalNote
                {
                    Type = "Servico",
                    PaymentId = payment.Id,
                    CoworkingId = coworkingEntity.Id,
                    ClientId = payment.Subscription?.Contract?.ClientId ?? payment.Reservation?.ClientId,
                    ContractId = payment.Subscription?.ContractId,
                    ReservationId = payment.ReservationId,
                    Amount = payment.Amount, // Valor total (com percentagem)
                    ServiceAmount = payment.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = "Pendente",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();

                // Emitir NF via SEFAZ
                try
                {
                    var emittedNote = await _sefazService.IssueServiceNoteAsync(fiscalNote, sefazConfig);
                    _context.FiscalNotes.Update(emittedNote);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"NF de serviço emitida com sucesso! Chave de acesso: {emittedNote.AccessKey}";
                    return RedirectToAction("ListServiceFiscalNotes");
                }
                catch (Exception ex)
                {
                    _context.FiscalNotes.Update(fiscalNote);
                    await _context.SaveChangesAsync();
                    TempData["Error"] = $"Erro ao emitir NF: {ex.Message}";
                }

                return RedirectToAction("ServiceFiscalNotes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao processar emissão de NF: {ex.Message}";
                return RedirectToAction("ServiceFiscalNotes");
            }
        }

        /// <summary>
        /// Importa NF de serviço (upload manual) para um pagamento. Útil quando o contador emite a NF.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportServiceFiscalNote(int paymentId, IFormFile pdfFile, IFormFile? xmlFile)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();
                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Parceiro não encontrado.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                var payment = await _context.Payments
                    .Include(p => p.Subscription).ThenInclude(s => s.Contract).ThenInclude(c => c.CoworkingAddress)
                    .Include(p => p.Reservation).ThenInclude(r => r.CoworkingAddress)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);
                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                var belongsToCoworking = (payment.Subscription?.Contract?.CoworkingAddress != null && payment.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                    || (payment.Reservation?.CoworkingAddress != null && payment.Reservation.CoworkingAddress.CoworkingId == coworkingEntity.Id);
                if (!belongsToCoworking)
                {
                    TempData["Error"] = "Este pagamento não pertence ao seu coworking.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                var existing = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.PaymentId == paymentId && fn.Type == "Servico" && (fn.Status == "Emitida" || fn.Status == "Importada"));
                if (existing != null)
                {
                    TempData["Error"] = "Já existe NF de serviço (emitida ou importada) para este pagamento.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData["Error"] = "Selecione o arquivo PDF da nota fiscal.";
                    return RedirectToAction("ServiceFiscalNotes");
                }
                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "O arquivo deve ser um PDF.";
                    return RedirectToAction("ServiceFiscalNotes");
                }

                var storage = await _storageFactory.GetStorageServiceAsync();
                var pdfFileName = $"imported_payment_{paymentId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                using var ms = new MemoryStream();
                await pdfFile.CopyToAsync(ms);
                var pdfPath = await storage.SaveFileAsync(ms.ToArray(), pdfFileName, $"invoices/imported/{coworkingEntity.Id}");

                var fiscalNote = new FiscalNote
                {
                    Type = "Servico",
                    PaymentId = payment.Id,
                    CoworkingId = coworkingEntity.Id,
                    ClientId = payment.Subscription?.Contract?.ClientId ?? payment.Reservation?.ClientId,
                    ContractId = payment.Subscription?.ContractId,
                    ReservationId = payment.ReservationId,
                    Amount = payment.Amount, // Valor total (com percentagem)
                    ServiceAmount = payment.Amount,
                    IssueDate = DateTime.UtcNow,
                    Status = "Importada",
                    PdfUrl = pdfPath,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FiscalNotes.Add(fiscalNote);
                await _context.SaveChangesAsync();

                TempData["Success"] = "NF importada com sucesso! Aguardando validação do administrador.";
                return RedirectToAction("ServiceFiscalNotes");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao importar NF: {ex.Message}";
                return RedirectToAction("ServiceFiscalNotes");
            }
        }

        /// <summary>
        /// Lista notas fiscais de serviço emitidas pelo parceiro
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ListServiceFiscalNotes(int page = 1, int pageSize = 20, string? status = null, string? type = null, string? noteNumber = null, string? client = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            // Buscar notas fiscais de serviço emitidas por este coworking
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
                .Where(fn => fn.CoworkingId == coworkingEntity.Id && fn.Type == "Servico")
                .AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(fn => fn.Status == status);
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

        /// <summary>
        /// Download do PDF da nota fiscal
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadFiscalNotePdf(int fiscalNoteId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
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
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
            return File(fileBytes, "application/pdf");
        }

        /// <summary>
        /// Configurações SEFAZ do parceiro
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SefazConfiguration()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var configurations = await _context.CoworkingSefazConfigurations
                .Where(c => c.CoworkingId == coworkingEntity.Id)
                .OrderBy(c => c.State)
                .ThenBy(c => c.Environment)
                .ToListAsync();

            ViewBag.Coworking = coworkingEntity;
            ViewBag.SupportedStates = new[] { "ES" }; // Por enquanto apenas ES

            return View(configurations);
        }

        /// <summary>
        /// Criar/Editar configuração SEFAZ do parceiro
        /// </summary>
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
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var coworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = coworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Parceiro não encontrado.";
                    return RedirectToAction("SefazConfiguration");
                }

                CoworkingSefazConfiguration config;

                if (id.HasValue)
                {
                    config = await _context.CoworkingSefazConfigurations
                        .FirstOrDefaultAsync(c => c.Id == id.Value && c.CoworkingId == coworkingEntity.Id);

                    if (config == null)
                    {
                        TempData["Error"] = "Configuração não encontrada.";
                        return RedirectToAction("SefazConfiguration");
                    }
                }
                else
                {
                    // Verificar se já existe configuração para este estado/ambiente
                    var existing = await _context.CoworkingSefazConfigurations
                        .FirstOrDefaultAsync(c => c.CoworkingId == coworkingEntity.Id &&
                                                  c.State == state.ToUpper() &&
                                                  c.Environment == environment);

                    if (existing != null)
                    {
                        TempData["Error"] = "Já existe uma configuração para este estado e ambiente.";
                        return RedirectToAction("SefazConfiguration");
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

                TempData["Success"] = "Configuração SEFAZ salva com sucesso!";
                return RedirectToAction("SefazConfiguration");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao salvar configuração: {ex.Message}";
                return RedirectToAction("SefazConfiguration");
            }
        }

        #region Monthly Settlements (Repasses Mensais)

        /// <summary>
        /// Lista os fechamentos mensais do parceiro
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MonthlySettlements()
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var settlements = await _monthlySettlementService.GetSettlementsByCoworkingAsync(coworking.Id);
            var summary = await _monthlySettlementService.GetDashboardSummaryAsync(coworking.Id);

            ViewBag.Summary = summary;
            return View(settlements);
        }

        /// <summary>
        /// Detalhes de um fechamento
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SettlementDetails(int id)
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var settlement = await _monthlySettlementService.GetSettlementByIdAsync(id);
            if (settlement == null || settlement.CoworkingId != coworking.Id)
            {
                TempData["Error"] = "Fechamento não encontrado.";
                return RedirectToAction("MonthlySettlements");
            }

            var invoice = await _monthlySettlementService.GetInvoiceBySettlementAsync(id);
            ViewBag.Invoice = invoice;

            return View(settlement);
        }

        /// <summary>
        /// Upload da nota fiscal do parceiro
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPartnerInvoice(int settlementId, IFormFile pdfFile, IFormFile? xmlFile,
            string? invoiceNumber, decimal invoiceAmount, string? partnerNotes)
        {
            var coworking = await GetCurrentCoworkingAsync();
            if (coworking == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData["Error"] = "Por favor, selecione o arquivo PDF da nota fiscal.";
                    return RedirectToAction("MonthlySettlements");
                }

                // Validar tipo de arquivo
                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "O arquivo deve ser um PDF.";
                    return RedirectToAction("MonthlySettlements");
                }

                using var pdfStream = pdfFile.OpenReadStream();
                Stream? xmlStream = null;
                string? xmlFileName = null;

                if (xmlFile != null && xmlFile.Length > 0)
                {
                    if (!xmlFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["Error"] = "O arquivo XML deve ter extensão .xml";
                        return RedirectToAction("MonthlySettlements");
                    }
                    xmlStream = xmlFile.OpenReadStream();
                    xmlFileName = xmlFile.FileName;
                }

                var invoice = await _monthlySettlementService.UploadPartnerInvoiceAsync(
                    settlementId, coworking.Id, pdfStream, pdfFile.FileName,
                    xmlStream, xmlFileName, invoiceNumber, invoiceAmount, partnerNotes);

                if (invoice.AutoValidationPassed == true)
                {
                    TempData["Success"] = "Nota fiscal enviada com sucesso! Validação automática OK. Aguardando aprovação do administrador.";
                }
                else if (invoice.AutoValidationPassed == false)
                {
                    TempData["Warning"] = $"Nota fiscal enviada, mas a validação automática encontrou problemas: {invoice.AutoValidationMessage}. O administrador irá analisar.";
                }
                else
                {
                    TempData["Success"] = "Nota fiscal enviada com sucesso! Aguardando validação do administrador.";
                }

                return RedirectToAction("MonthlySettlements");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao enviar nota fiscal: {ex.Message}";
                return RedirectToAction("MonthlySettlements");
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

    // DTO para solicitação de alteração de capacidade
    public class CapacityChangeRequestDto
    {
        public int AddressId { get; set; }
        public int RequestedCapacity { get; set; }
        public string? Justification { get; set; }
    }
}
