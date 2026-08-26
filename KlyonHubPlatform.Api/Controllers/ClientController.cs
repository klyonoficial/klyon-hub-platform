using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.FiscalOnboarding;
using KlyonHubPlatform.Application.DTOs.ClientOnboarding;
using KlyonHubPlatform.Application.DTOs.ClientProfile;
using KlyonHubPlatform.Application.DTOs.OpenCnpj;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Core.Helpers;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Api.Constants;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using KlyonHubPlatform.Api.Requests;
using KlyonHubPlatform.Api.Dtos;
using CreditPurchase = KlyonHubPlatform.Domain.Entities.CreditPurchase;
using ClientCredit = KlyonHubPlatform.Domain.Entities.ClientCredit;
namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = KlyonHubPlatform.Domain.Core.Constants.RoleNames.Client + "," + Administrator)]
    public class ClientController : Controller
    {
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentService _paymentService;
        private readonly IDocumentService _documentService;
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly IAsaasService _asaasService;
        private readonly IClientAppService _clientAppService;
        private readonly IReservationTransferService _reservationTransferService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClientController> _logger;
        private readonly ISystemConfigurationService _systemConfig;
        private readonly IClientMenuService _clientMenuService;
        private readonly IServiceCatalogService _serviceCatalogService;
        private readonly IClientDashboardService _clientDashboardService;
        private readonly ApplicationDbContext _context;
        private readonly ICorrespondenceService _correspondenceService;
        private readonly StorageServiceFactory _storageFactory;
        private readonly IOperatingHoursRepository _operatingHoursRepository;
        private readonly IFiscalOnboardingService _fiscalOnboardingService;
        private readonly IKlyonOnboardingPublicDocumentService _klyonOnboardingPublicDocumentService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IOpenCnpjQueryService _openCnpjQueryService;
        private readonly IContractDocumentsEnsureService _contractDocumentsEnsureService;
        private readonly IPartnerListingService _partnerListingService;
        private readonly IContractPartnerDocumentService _contractPartnerDocumentService;
        private readonly IClientOnboardingDocumentService _clientOnboardingDocumentService;
        private readonly IContractTimelineService _contractTimelineService;
        private readonly IClientProfileService _clientProfileService;
        private readonly IPromoCodeService _promoCodeService;
        private readonly IAddressPricingService _addressPricingService;
        public ClientController(
            IContractService contractService,
            ICoworkingService coworkingService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IDocumentService documentService,
            IAddressDeclarationService addressDeclarationService,
            IAsaasService asaasService,
            IClientAppService clientAppService,
            IReservationTransferService reservationTransferService,
            IEmailService emailService,
            ILogger<ClientController> logger,
            ISystemConfigurationService systemConfig,
            IClientMenuService clientMenuService,
            IServiceCatalogService serviceCatalogService,
            IClientDashboardService clientDashboardService,
            ApplicationDbContext context,
            ICorrespondenceService correspondenceService,
            StorageServiceFactory storageFactory,
            IOperatingHoursRepository operatingHoursRepository,
            IFiscalOnboardingService fiscalOnboardingService,
            IKlyonOnboardingPublicDocumentService klyonOnboardingPublicDocumentService,
            IWebHostEnvironment webHostEnvironment,
            IOpenCnpjQueryService openCnpjQueryService,
            IContractDocumentsEnsureService contractDocumentsEnsureService,
            IPartnerListingService partnerListingService,
            IContractPartnerDocumentService contractPartnerDocumentService,
            IClientOnboardingDocumentService clientOnboardingDocumentService,
            IContractTimelineService contractTimelineService,
            IClientProfileService clientProfileService,
            IPromoCodeService promoCodeService,
            IAddressPricingService addressPricingService)
        {
            _contractService = contractService;
            _coworkingService = coworkingService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _documentService = documentService;
            _addressDeclarationService = addressDeclarationService;
            _asaasService = asaasService;
            _clientAppService = clientAppService;
            _reservationTransferService = reservationTransferService;
            _emailService = emailService;
            _logger = logger;
            _systemConfig = systemConfig;
            _clientMenuService = clientMenuService;
            _serviceCatalogService = serviceCatalogService;
            _clientDashboardService = clientDashboardService;
            _context = context;
            _correspondenceService = correspondenceService;
            _storageFactory = storageFactory;
            _operatingHoursRepository = operatingHoursRepository;
            _fiscalOnboardingService = fiscalOnboardingService;
            _klyonOnboardingPublicDocumentService = klyonOnboardingPublicDocumentService;
            _webHostEnvironment = webHostEnvironment;
            _openCnpjQueryService = openCnpjQueryService;
            _contractDocumentsEnsureService = contractDocumentsEnsureService;
            _partnerListingService = partnerListingService;
            _contractPartnerDocumentService = contractPartnerDocumentService;
            _clientOnboardingDocumentService = clientOnboardingDocumentService;
            _contractTimelineService = contractTimelineService;
            _clientProfileService = clientProfileService;
            _promoCodeService = promoCodeService;
            _addressPricingService = addressPricingService;
        }
        private async Task<long> GetMaxPhotoSizeBytesAsync()
        {
            var mb = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mb, out var n) && n > 0) return n * 1024L * 1024L;
            return 5 * 1024L * 1024L;
        }

        private const int PaymentSuccessAutoRedirectDelaySeconds = 4;

        private string? LocalReturnUrl(string? returnUrl) =>
            !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        private string PaymentSuccessUrl(int paymentId) =>
            Url.Action(ActionNames.PaymentSuccess, ControllerNames.Client, new { id = paymentId })!;

        private object CardPaymentSuccessJson(Payment payment, string message) =>
            new { success = true, message, redirectUrl = PaymentSuccessUrl(payment.Id) };

        private async Task<decimal?> GetAddressSurchargePercentAsync(int coworkingAddressId, CancellationToken cancellationToken = default) =>
            await _context.CoworkingAddresses.AsNoTracking()
                .Where(a => a.Id == coworkingAddressId)
                .Select(a => a.CardMonthlySurchargePercent)
                .FirstOrDefaultAsync(cancellationToken);

        private async Task<decimal?> GetContractSurchargePercentAsync(Contract contract, CancellationToken cancellationToken = default)
        {
            if (contract.SnapshotCardMonthlySurchargePercent.HasValue)
                return contract.SnapshotCardMonthlySurchargePercent;
            return await GetAddressSurchargePercentAsync(contract.CoworkingAddressId, cancellationToken);
        }

        private static decimal GetDiscountedBasePrice(decimal snapshotListPrice, decimal snapshotDiscountAmount) =>
            Math.Max(0, snapshotListPrice - snapshotDiscountAmount);

        private decimal ResolvePromoBaseAmount(
            decimal snapshotListPrice,
            decimal? surchargePercent,
            PaymentMethod method,
            ContractCardPlanType? cardPlanType)
        {
            if (method == PaymentMethod.CreditCard && cardPlanType == ContractCardPlanType.CardMonthly)
                return _addressPricingService.GetMonthlyInstallment(snapshotListPrice, surchargePercent);
            return snapshotListPrice;
        }

        private decimal CalculateCheckoutAmount(
            decimal snapshotListPrice,
            decimal snapshotDiscountAmount,
            decimal? surchargePercent,
            PaymentMethod method,
            ContractCardPlanType? cardPlanType)
        {
            if (method == PaymentMethod.CreditCard && cardPlanType == ContractCardPlanType.CardMonthly)
            {
                var monthlyBeforeDiscount = _addressPricingService.GetMonthlyInstallment(snapshotListPrice, surchargePercent);
                return Math.Max(0, monthlyBeforeDiscount - snapshotDiscountAmount);
            }

            var discountedBase = GetDiscountedBasePrice(snapshotListPrice, snapshotDiscountAmount);
            if (method == PaymentMethod.BankTransfer)
                return _addressPricingService.GetBoletoPrice(discountedBase);
            return _addressPricingService.GetAnnualPrice(discountedBase);
        }

        private async Task SyncCheckoutAmountFromSnapshotAsync(
            Payment payment,
            Contract contract,
            PaymentMethod method,
            ContractCardPlanType? cardPlanType,
            CancellationToken cancellationToken)
        {
            var snapshotListPrice = ResolveSnapshotListPrice(payment, contract);
            var surcharge = await GetContractSurchargePercentAsync(contract, cancellationToken);
            var effectiveCardPlan = cardPlanType ?? contract.CardPlanType;

            var amount = CalculateCheckoutAmount(
                snapshotListPrice,
                payment.SnapshotDiscountAmount,
                surcharge,
                method,
                method == PaymentMethod.CreditCard ? effectiveCardPlan : null);

            var previousAmount = payment.Amount;
            payment.Amount = amount;
            payment.UpdatedAt = DateTime.UtcNow;

            if (method == PaymentMethod.BankTransfer && previousAmount != amount)
            {
                payment.BoletoCode = null;
                payment.BoletoBarcode = null;
                payment.BoletoExpiresAt = null;
                payment.BankSlipUrl = null;
            }

            contract.MonthlyValue = amount;
            contract.SnapshotMonthlyValue = amount;

            if (method == PaymentMethod.PIX && cardPlanType.HasValue)
                contract.CardPlanType = cardPlanType.Value;
            else if (method == PaymentMethod.CreditCard)
                contract.CardPlanType = effectiveCardPlan ?? ContractCardPlanType.Card1x;

            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value, cancellationToken);
                if (subscription != null)
                    subscription.MonthlyAmount = amount;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private Task<(decimal ChargeAmount, PaymentMethod InitialPaymentMethod, ContractCardPlanType? CardPlanType)> ResolveInitialContractPricingAsync(
            decimal basePrice,
            string option,
            decimal? surchargePercent,
            CancellationToken cancellationToken = default)
        {
            (decimal ChargeAmount, PaymentMethod InitialPaymentMethod, ContractCardPlanType? CardPlanType) result = option switch
            {
                "Card1x" => (
                    _addressPricingService.GetChargeAmount(basePrice, surchargePercent, ContractCardPlanType.Card1x),
                    PaymentMethod.CreditCard,
                    (ContractCardPlanType?)ContractCardPlanType.Card1x),
                "CardMonthly" => (
                    _addressPricingService.GetChargeAmount(basePrice, surchargePercent, ContractCardPlanType.CardMonthly),
                    PaymentMethod.CreditCard,
                    (ContractCardPlanType?)ContractCardPlanType.CardMonthly),
                "PixAnnual" => (
                    _addressPricingService.GetChargeAmount(basePrice, surchargePercent, ContractCardPlanType.PixAnnual),
                    PaymentMethod.PIX,
                    (ContractCardPlanType?)ContractCardPlanType.PixAnnual),
                "PIX" => (
                    _addressPricingService.GetChargeAmount(basePrice, surchargePercent, ContractCardPlanType.None),
                    PaymentMethod.PIX,
                    (ContractCardPlanType?)ContractCardPlanType.None),
                _ => (
                    _addressPricingService.GetChargeAmount(basePrice, surchargePercent, ContractCardPlanType.PixAnnual),
                    PaymentMethod.PIX,
                    (ContractCardPlanType?)ContractCardPlanType.PixAnnual)
            };
            return Task.FromResult(result);
        }

        private decimal ResolveSnapshotListPrice(Payment payment, Contract? contract) =>
            payment.SnapshotListPrice > 0
                ? payment.SnapshotListPrice
                : contract?.SnapshotListPrice > 0
                    ? contract.SnapshotListPrice
                    : contract?.MonthlyValue ?? payment.Amount;

        private async Task<(decimal Price1x, decimal PriceMonthly)> ResolveSnapshotCardPricesAsync(
            Payment payment,
            Contract? contract,
            CancellationToken cancellationToken = default)
        {
            var listPrice = ResolveSnapshotListPrice(payment, contract);
            var surcharge = contract != null
                ? await GetContractSurchargePercentAsync(contract, cancellationToken)
                : null;
            return (
                _addressPricingService.GetChargeAmount(listPrice, surcharge, ContractCardPlanType.Card1x),
                _addressPricingService.GetChargeAmount(listPrice, surcharge, ContractCardPlanType.CardMonthly));
        }

        private async Task<string?> ResolvePostPaymentOnboardingRedirectUrlAsync(
            int userId,
            int clientId,
            Contract contract,
            CancellationToken cancellationToken)
        {
            var onboarding = await _clientOnboardingDocumentService.GetPageAsync(
                contract.Id,
                clientId,
                User.IsInRole(Administrator),
                cancellationToken);
            if (onboarding.Success && onboarding.CanUpload)
            {
                var uploadUrl = Url.Action(ActionNames.UploadDocuments, new { contractId = contract.Id })!;
                var party = await _clientProfileService.GetContractPartyStatusByUserIdAsync(userId, cancellationToken);
                if (!party.IsComplete)
                {
                    TempData[TempDataKeys.Info] = party.ErrorMessage ?? InfoMessages.CompleteContractPartyData;
                    return Url.Action(ActionNames.Profile, new { returnUrl = LocalReturnUrl(uploadUrl) });
                }
                return uploadUrl;
            }
            if (!onboarding.Success && !contract.DocumentsApproved)
                return Url.Action(ActionNames.UploadDocuments, new { contractId = contract.Id });
            return null;
        }

        private async Task<IActionResult?> RedirectToProfileIfContractPartyIncompleteAsync(
            int userId,
            string? returnUrl,
            CancellationToken cancellationToken)
        {
            var party = await _clientProfileService.GetContractPartyStatusByUserIdAsync(userId, cancellationToken);
            if (party.IsComplete)
                return null;
            TempData[TempDataKeys.Info] = party.ErrorMessage ?? InfoMessages.CompleteContractPartyData;
            return RedirectToAction(ActionNames.Profile, new { returnUrl = LocalReturnUrl(returnUrl) });
        }
        [HttpGet]
        public async Task<IActionResult> Services(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (User.IsInRole(RoleNames.Client) && !User.IsInRole(Administrator))
            {
                var status = await _clientMenuService.GetRegistrationStatusAsync(userId);
                if (!status.IsComplete)
                {
                    TempData[TempDataKeys.Info] = status.TempDataMessageKey == ClientRegistrationMessageKeys.CompleteRegistrationWithFields
                        ? InfoMessages.CompleteRegistrationWithFields
                        : InfoMessages.CompleteRegistration;
                    return RedirectToAction(status.RedirectActionName ?? ActionNames.RegisterClient);
                }
            }
            var hasActiveContract = await _clientMenuService.ClientHasActiveContractAsync(userId);
            var marketplaceRoute = Url.Action(ActionNames.Marketplace, ControllerNames.Home) ?? "/Home/Marketplace";
            var chooseLocationRoute = Url.Action(ActionNames.ChooseLocation, ControllerNames.Client) ?? "/Client/ChooseLocation";
            var services = await _serviceCatalogService.GetActiveServicesForClientAsync(userId, marketplaceRoute, chooseLocationRoute);
            ViewBag.HasActiveContract = hasActiveContract;
            return View(services);
        }
        public Task<IActionResult> Dashboard()
        {
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.Services));
        }
        [HttpGet]
        public async Task<IActionResult> DashboardDetails(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _clientDashboardService.GetDashboardDetailsAsync(userId, cancellationToken);
            ViewBag.Client = result.Client;
            ViewBag.ReservationsCount = result.ReservationsCount;
            ViewBag.HasPending = result.HasFinancialPending || result.HasCertificateExpiring;
            ViewBag.HasFinancialPending = result.HasFinancialPending;
            ViewBag.HasCertificateExpiring = result.HasCertificateExpiring;
            ViewBag.FirstPendingPaymentId = result.FirstPendingPaymentId;
            ViewBag.SubscriptionIdsWithPaymentAwaitingValidation = result.SubscriptionIdsWithPaymentAwaitingValidation;
            var party = await _clientProfileService.GetContractPartyStatusByUserIdAsync(userId, cancellationToken);
            ViewBag.ContractPartyIncomplete = !party.IsComplete;
            ViewBag.ContractPartyMessage = party.ErrorMessage;
            return View(ViewNames.Dashboard);
        }
        [HttpGet]
        public async Task<IActionResult> Profile(string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsNoTrackingAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFoundCompleteRegistration;
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
            ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
            var party = ClientContractPartyRules.Evaluate(client);
            ViewBag.ContractPartyIncomplete = !party.IsComplete;
            ViewBag.ContractPartyMissingFields = party.MissingFields;
            var maxPhotoMb = 5;
            var mbStr = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mbStr, out var n) && n > 0) maxPhotoMb = n;
            ViewBag.MaxPhotoSizeMB = maxPhotoMb;
            return View(client);
        }
        [HttpGet]
        public async Task<IActionResult> GetLogo(int clientId, CancellationToken cancellationToken = default)
        {
            var client = await _clientAppService.GetClientByIdAsync(clientId, cancellationToken);
            if (client == null || string.IsNullOrEmpty(client.LogoPath))
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var userClient = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            var canAccess = User.IsInRole(Administrator) || (userClient != null && userClient.Id == clientId);
            if (!canAccess)
            {
                return Forbid();
            }
            var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
            var storageService = await storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(client.LogoPath);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var contentType = "image/jpeg";
            var lowerPath = client.LogoPath.ToLower();
            if (lowerPath.EndsWith(".png"))
                contentType = "image/png";
            else if (lowerPath.EndsWith(".gif"))
                contentType = "image/gif";
            else if (lowerPath.EndsWith(".webp"))
                contentType = "image/webp";
            return File(fileBytes, contentType);
        }
        private async Task RefreshUserPrincipalAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, ClientDisplayName.Resolve(user.FullName, user.Email))
            };
            foreach (var roleClaim in User.FindAll(ClaimTypes.Role))
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal);
            var displayName = ClientDisplayName.Resolve(user.FullName, user.Email);
            if (!string.IsNullOrEmpty(displayName))
                HttpContext.Session.SetString(SessionKeys.UserName, displayName);
        }
        private async Task SaveClientRegistrationDocumentsIfNeededAsync(int clientId, IFormFile? cnpjDoc, IFormFile? contratoDoc, IFormFile? rgCnhDoc, CancellationToken cancellationToken)
        {
            if (User.FindFirstValue(AuthConstants.ClientHasCnpjClaimType) != "true")
                return;
            const long maxFileBytes = 10 * 1024 * 1024; // 10MB
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var baseFolder = $"clients/{clientId}/registration_documents";
            async Task SaveOneAsync(ClientRegistrationDocumentType type, IFormFile? file)
            {
                if (file == null || file.Length == 0) return;
                if (file.Length > maxFileBytes) return;
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                var fileBytes = stream.ToArray();
                var folder = type switch
                {
                    ClientRegistrationDocumentType.Cnpj => "cnpj",
                    ClientRegistrationDocumentType.ContratoSocial => "contrato_social",
                    ClientRegistrationDocumentType.RgCnhRepresentantes => "rg_cnh_representantes",
                    _ => "other"
                };
                var fileName = $"{folder}_{clientId}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
                var filePath = await storageService.SaveFileAsync(fileBytes, fileName, $"{baseFolder}/{folder}");
                var existing = await _context.ClientRegistrationDocuments.FirstOrDefaultAsync(d => d.ClientId == clientId && d.Type == type, cancellationToken);
                if (existing != null)
                {
                    try { await storageService.DeleteFileAsync(existing.FilePath); } catch { }
                    existing.FileName = file.FileName;
                    existing.FilePath = filePath;
                }
                else
                {
                    _context.ClientRegistrationDocuments.Add(new ClientRegistrationDocument
                    {
                        ClientId = clientId,
                        Type = type,
                        FileName = file.FileName,
                        FilePath = filePath,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await SaveOneAsync(ClientRegistrationDocumentType.Cnpj, cnpjDoc);
            await SaveOneAsync(ClientRegistrationDocumentType.ContratoSocial, contratoDoc);
            await SaveOneAsync(ClientRegistrationDocumentType.RgCnhRepresentantes, rgCnhDoc);
            await _context.SaveChangesAsync(cancellationToken);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Client client, IFormFile? Logo, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var isAjaxRequest = Request.Headers[HttpHeaderNames.XRequestedWith].ToString() == HttpHeaderNames.XRequestedWithValue;
            var safeReturnUrl = LocalReturnUrl(returnUrl);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                ClientProfileLogoInput? logoInput = null;
                if (Logo != null && Logo.Length > 0)
                {
                    await using var stream = new MemoryStream();
                    await Logo.CopyToAsync(stream, cancellationToken);
                    logoInput = new ClientProfileLogoInput
                    {
                        Content = stream.ToArray(),
                        FileName = Logo.FileName
                    };
                }

                var saveResult = await _clientProfileService.SaveProfileAsync(new ClientProfileSaveInput
                {
                    UserId = userId,
                    AccountEmail = userEmail,
                    RazaoSocial = client.RazaoSocial,
                    NomeFantasia = client.NomeFantasia,
                    CNPJ = client.CNPJ,
                    CPF = client.CPF,
                    ContactName = client.ContactName,
                    ContactPhone = client.ContactPhone,
                    ZipCode = client.ZipCode,
                    Address = client.Address,
                    AddressNumber = client.AddressNumber,
                    Neighborhood = client.Neighborhood,
                    Complement = client.Complement,
                    City = client.City,
                    State = client.State,
                    Logo = logoInput,
                    MaxLogoBytes = await GetMaxPhotoSizeBytesAsync()
                }, cancellationToken);

                if (!saveResult.Success)
                {
                    var err = saveResult.ErrorMessage ?? "Não foi possível salvar o perfil.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile, new { returnUrl = safeReturnUrl });
                }

                var userToUpdate = await _clientAppService.GetUserByIdAsync(userId, cancellationToken);
                if (userToUpdate != null)
                    await RefreshUserPrincipalAsync(userToUpdate);

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = JsonMessage.PerfilAtualizadoComSucesso,
                        warning = saveResult.WarningMessage,
                        contactName = saveResult.ContactName,
                        displayName = saveResult.DisplayName,
                        redirectUrl = safeReturnUrl
                    });
                }

                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ProfileUpdated;
                if (!string.IsNullOrEmpty(saveResult.WarningMessage))
                    TempData[TempDataKeys.Warning] = saveResult.WarningMessage;
                if (!string.IsNullOrEmpty(safeReturnUrl))
                    return Redirect(safeReturnUrl);
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                var err = $"Erro ao atualizar perfil: {ex.Message}";
                TempData[ErrorMessages.TempDataKey] = err;
                if (isAjaxRequest) return Json(new { success = false, error = err });
                return RedirectToAction(ActionNames.Profile, new { returnUrl = safeReturnUrl });
            }
        }
        [HttpGet]
        public async Task<IActionResult> RegisterClient(int? id = null, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ClientHasCnpj = User.FindFirstValue(AuthConstants.ClientHasCnpjClaimType) == "true";
            if (id.HasValue)
            {
                var client = await _clientAppService.GetClientByIdWithUserAsync(id.Value, cancellationToken);
                if (client != null)
                {
                    if (User.IsInRole(Administrator) || client.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"))
                    {
                        ViewBag.PersonType = string.IsNullOrWhiteSpace(client.CNPJ) ? "Fisica" : "Juridica";
                        return View(client);
                    }
                }
            }
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> CheckDocumentExists(string? document, string? type, int? excludeClientId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(type))
            {
                return Json(new { valid = false, exists = false, message = JsonMessage.DocumentoNaoInformado });
            }
            if (!Enum.TryParse<TipoDocumentoCliente>(type.Trim(), true, out var docType))
            {
                return Json(new { valid = false, exists = false, message = JsonMessage.TipoDeveSerCpfOuCnpj });
            }
            var validation = DocumentValidator.ValidateCPForCNPJ(document);
            if (!validation.isValid)
            {
                return Json(new { valid = false, exists = false, message = validation.message });
            }
            if (validation.type != docType)
            {
                return Json(new { valid = false, exists = false, message = docType == TipoDocumentoCliente.CPF ? JsonMessage.InformeCpfValido11Digitos : JsonMessage.InformeCnpjValido14Digitos });
            }
            if (docType == TipoDocumentoCliente.CPF)
            {
                var cpfClean = document.Replace(".", "").Replace("-", "").Trim();
                var exists = await _clientAppService.ClientExistsWithCpfAsync(cpfClean, excludeClientId, cancellationToken);
                if (exists)
                    return Json(new { valid = true, exists = true, message = string.Format(JsonMessage.CpfJaCadastradoNoSistema, document) });
            }
            else
            {
                var cnpjClean = document.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var exists = await _clientAppService.ClientExistsWithCnpjAsync(cnpjClean, excludeClientId, cancellationToken);
                if (exists)
                    return Json(new { valid = true, exists = true, message = string.Format(JsonMessage.CnpjJaCadastradoNoSistema, document) });
            }
            return Json(new { valid = true, exists = false, message = JsonMessage.Empty });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClient(Client client, string? returnUrl = null, string? PersonType = null, IFormFile? CnpjDocument = null, IFormFile? ContratoSocialDocument = null, IFormFile? RgCnhRepresentantesDocument = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var isAdmin = User.IsInRole(Administrator);
                var personType = (PersonType ?? "").Trim();
                ViewBag.PersonType = string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) ? "Juridica" : "Fisica";
                if (!string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase))
                {
                    client.CNPJ = null;
                    client.RazaoSocial = null;
                    client.NomeFantasia = null;
                }
                if (client.Id > 0)
                {
                    var existingClient = await _clientAppService.GetClientByIdWithUserAsync(client.Id, cancellationToken);
                    if (existingClient == null)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    if (!isAdmin && existingClient.UserId != userId)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.NoPermissionToEditClient;
                        return RedirectToAction(ActionNames.DashboardDetails);
                    }
                    if (!string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.FullNameRequired;
                        ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                        return View(existingClient);
                    }
                    if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                        client.ContactName = client.RazaoSocial ?? client.NomeFantasia ?? existingClient.ContactName;
                    if (string.IsNullOrWhiteSpace(client.ContactPhone))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.PhoneRequired;
                        ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                        return View(existingClient);
                    }
                    existingClient.RazaoSocial = client.RazaoSocial;
                    existingClient.NomeFantasia = client.NomeFantasia;
                    existingClient.ContactName = client.ContactName;
                    existingClient.ContactPhone = client.ContactPhone;
                    existingClient.Address = client.Address;
                    existingClient.AddressNumber = client.AddressNumber;
                    existingClient.Neighborhood = client.Neighborhood;
                    existingClient.Complement = client.Complement;
                    existingClient.City = client.City;
                    existingClient.State = client.State;
                    existingClient.ZipCode = client.ZipCode;
                    if (isAdmin && !string.IsNullOrEmpty(client.ContactEmail))
                    {
                        existingClient.ContactEmail = client.ContactEmail;
                    }
                    if (!string.IsNullOrWhiteSpace(client.CNPJ))
                    {
                        var cnpjValidation = DocumentValidator.ValidateCPForCNPJ(client.CNPJ);
                        if (!cnpjValidation.isValid || cnpjValidation.type != TipoDocumentoCliente.CNPJ)
                        {
                            TempData[ErrorMessages.TempDataKey] = cnpjValidation.message;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        var cnpjClean = client.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                        var allClientsEdit = await _clientAppService.GetClientsWithCnpjExcludingAsync(existingClient.Id, cancellationToken);
                        var existingCNPJ = allClientsEdit.FirstOrDefault(c => 
                            c.CNPJ != null && 
                            c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                        if (existingCNPJ != null)
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjAlreadyRegistered(client.CNPJ);
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        existingClient.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    }
                    else
                    {
                        existingClient.CNPJ = null;
                    }
                    if (!string.IsNullOrWhiteSpace(client.CPF))
                    {
                        var cpfValidation = DocumentValidator.ValidateCPForCNPJ(client.CPF);
                        if (!cpfValidation.isValid || cpfValidation.type != TipoDocumentoCliente.CPF)
                        {
                            TempData[ErrorMessages.TempDataKey] = cpfValidation.message;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        var cpfClean = client.CPF.Replace(".", "").Replace("-", "").Trim();
                        var allClientsEditCpf = await _clientAppService.GetClientsWithCpfExcludingAsync(existingClient.Id, cancellationToken);
                        var existingCPF = allClientsEditCpf.FirstOrDefault(c => 
                            c.CPF != null && 
                            c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                        if (existingCPF != null)
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfAlreadyRegistered(client.CPF);
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        existingClient.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    }
                    else
                    {
                        existingClient.CPF = null;
                    }
                    if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(existingClient.CNPJ))
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjRequiredForPj;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        if (string.IsNullOrWhiteSpace(existingClient.CPF))
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfResponsavelRequiredForPj;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        if (string.IsNullOrWhiteSpace(existingClient.RazaoSocial))
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CompanyNameRequiredForPj;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(existingClient.CPF))
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfRequiredForPf;
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                    }
                    if (!string.IsNullOrEmpty(existingClient.ContactPhone))
                    {
                        existingClient.ContactPhone = existingClient.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                    }
                    if (!string.IsNullOrEmpty(existingClient.ZipCode))
                    {
                        existingClient.ZipCode = existingClient.ZipCode.Replace("-", "");
                    }
                    if (existingClient.User != null)
                        existingClient.User.FullName = existingClient.ContactName ?? "";
                    var asaasWarning = await _clientAppService.SaveClientProfileAsync(existingClient.Id, userId, cancellationToken);
                    if (!isAdmin && existingClient.User != null)
                        await RefreshUserPrincipalAsync(existingClient.User);
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.ClientUpdated;
                    if (!string.IsNullOrEmpty(asaasWarning))
                        TempData[TempDataKeys.Warning] = asaasWarning;
                    if (isAdmin)
                    {
                        return RedirectToAction(ActionNames.Contracts, ControllerNames.Admin);
                    }
                    await SaveClientRegistrationDocumentsIfNeededAsync(existingClient.Id, CnpjDocument, ContratoSocialDocument, RgCnhRepresentantesDocument, cancellationToken);
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction(ActionNames.DashboardDetails);
                }
                var userClient = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (userClient != null && !isAdmin)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientAlreadyHasRegistration;
                    return RedirectToAction(ActionNames.DashboardDetails);
                }
                if (!string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.FullNameRequired;
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }
                if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                    client.ContactName = client.RazaoSocial?.Trim() ?? client.NomeFantasia?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(client.CNPJ))
                {
                    var cnpjValidation = DocumentValidator.ValidateCPForCNPJ(client.CNPJ);
                    if (!cnpjValidation.isValid || cnpjValidation.type != TipoDocumentoCliente.CNPJ)
                    {
                        TempData[ErrorMessages.TempDataKey] = cnpjValidation.message;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    var cnpjCleanNew = client.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    var allClientsCnpj = await _clientAppService.GetClientsWithCnpjExcludingAsync(0, cancellationToken);
                    var existingCNPJ = allClientsCnpj.FirstOrDefault(c =>
                        c.CNPJ != null &&
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjCleanNew);
                    if (existingCNPJ != null)
                    {
                        if (!isAdmin && existingCNPJ.UserId == userId)
                        {
                            return RedirectToAction(ActionNames.RegisterClient, new { id = existingCNPJ.Id });
                        }
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjAlreadyRegisteredInSystem(client.CNPJ);
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    client.CNPJ = $"{cnpjCleanNew.Substring(0, 2)}.{cnpjCleanNew.Substring(2, 3)}.{cnpjCleanNew.Substring(5, 3)}/{cnpjCleanNew.Substring(8, 4)}-{cnpjCleanNew.Substring(12, 2)}";
                }
                else
                {
                    client.CNPJ = null;
                }
                if (!string.IsNullOrWhiteSpace(client.CPF))
                {
                    var cpfValidation = DocumentValidator.ValidateCPForCNPJ(client.CPF);
                    if (!cpfValidation.isValid || cpfValidation.type != TipoDocumentoCliente.CPF)
                    {
                        TempData[ErrorMessages.TempDataKey] = cpfValidation.message;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    var cpfCleanNew = client.CPF.Replace(".", "").Replace("-", "").Trim();
                    var allClientsCpf = await _clientAppService.GetClientsWithCpfExcludingAsync(0, cancellationToken);
                    var existingCPF = allClientsCpf.FirstOrDefault(c =>
                        c.CPF != null &&
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfCleanNew);
                    if (existingCPF != null)
                    {
                        if (!isAdmin && existingCPF.UserId == userId)
                        {
                            return RedirectToAction(ActionNames.RegisterClient, new { id = existingCPF.Id });
                        }
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfAlreadyRegisteredInSystem(client.CPF);
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    client.CPF = $"{cpfCleanNew.Substring(0, 3)}.{cpfCleanNew.Substring(3, 3)}.{cpfCleanNew.Substring(6, 3)}-{cpfCleanNew.Substring(9, 2)}";
                }
                else
                {
                    client.CPF = null;
                }
                if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(client.CNPJ))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjRequiredForPj;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    if (string.IsNullOrWhiteSpace(client.CPF))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfResponsavelRequiredForPj;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    if (string.IsNullOrWhiteSpace(client.RazaoSocial))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CompanyNameRequiredForPj;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(client.CPF))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfRequiredForPf;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                }
                if (string.IsNullOrWhiteSpace(client.ContactPhone))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PhoneRequired;
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }
                client.ContactEmail = userEmail;
                client.UserId = userId;
                client.IsActive = true;
                if (!string.IsNullOrEmpty(client.ContactPhone))
                {
                    client.ContactPhone = client.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                }
                if (!string.IsNullOrEmpty(client.ZipCode))
                {
                    client.ZipCode = client.ZipCode.Replace("-", "");
                }
                await _clientAppService.AddClientAsync(client, cancellationToken);
                await SaveClientRegistrationDocumentsIfNeededAsync(client.Id, CnpjDocument, ContratoSocialDocument, RgCnhRepresentantesDocument, cancellationToken);
                var userForNewClient = await _clientAppService.GetUserByIdAsync(userId, cancellationToken);
                if (userForNewClient != null && !string.IsNullOrWhiteSpace(client.ContactName))
                {
                    userForNewClient.FullName = client.ContactName;
                    await _clientAppService.SaveChangesAsync(cancellationToken);
                    await RefreshUserPrincipalAsync(userForNewClient);
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.RegistrationCompleted;
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(ActionNames.DashboardDetails);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjOrCpfAlreadyRegistered;
                ViewBag.UserEmail = userEmail;
                return View(client);
            }
            catch (Exception ex)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("cadastrar cliente", ex.Message);
                ViewBag.UserEmail = userEmail;
                return View(client);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> FiscalOnboarding(int coworkingAddressId, string? serviceType = null, CancellationToken cancellationToken = default)
        {
            ViewBag.CoworkingAddressId = coworkingAddressId;
            ViewBag.FiscalAcceptLgpdTerms = false;
            ViewBag.ServiceType = serviceType?.ToLowerInvariant() ?? "";
            await TrySetFiscalOnboardingPartnerDisplayAsync(coworkingAddressId, cancellationToken);
            await PrefillFiscalOnboardingFromLoggedInUserAsync(cancellationToken);
            return View();
        }

        private async Task PrefillFiscalOnboardingFromLoggedInUserAsync(CancellationToken cancellationToken)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            ViewBag.IsLoggedInCheckout = true;
            ViewBag.ContactEmail = user?.Email ?? "";
            ViewBag.ContactPhone = client?.ContactPhone ?? user?.Phone ?? "";
            ViewBag.CpfValue = client?.CPF ?? "";
            ViewBag.CnpjValue = client?.CNPJ ?? "";
            ViewBag.DeclaresHasCnpj = !string.IsNullOrWhiteSpace(client?.CNPJ) || user?.ClientHasCnpj == true;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ValidateCnpjForFiscal(string cnpj, CancellationToken cancellationToken = default)
        {
            var digits = System.Text.RegularExpressions.Regex.Replace(cnpj ?? "", @"[^\d]", "");
            if (digits.Length != 14)
                return Json(new { valid = false, message = "CNPJ inválido." });
            if (!DocumentValidator.IsValidCNPJ(digits))
                return Json(new { valid = false, message = "CNPJ inválido." });
            try
            {
                var result = await _openCnpjQueryService.ValidateForFiscalOnboardingAsync(digits, cancellationToken);
                if (result.Success)
                    return Json(new { valid = true, razaoSocial = result.RazaoSocial, cnae = result.CnaePrincipal });
                return Json(new { valid = false, message = result.ErrorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ValidateCnpjForFiscal falhou para CNPJ.");
                return Json(new { valid = false, message = "Não foi possível validar o CNPJ no momento. Tente novamente." });
            }
        }

        private async Task TrySetFiscalOnboardingPartnerDisplayAsync(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            if (coworkingAddressId <= 0) return;
            var addr = await _context.CoworkingAddresses.AsNoTracking()
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == coworkingAddressId, cancellationToken);
            if (addr?.Coworking == null) return;
            var trade = !string.IsNullOrWhiteSpace(addr.Coworking.TradeName) ? addr.Coworking.TradeName : addr.Coworking.Name;
            ViewBag.PartnerDisplay = $"{trade} · {addr.City}/{addr.State}";
        }

        /// <summary>Campo oculto <c>declaresHasCnpj</c> no form (true/false). Sem valor = padrão CNPJ (Sim), para compatibilidade.</summary>
        private static bool ParseDeclaresHasCnpjFromForm(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return true;
            var v = raw.Trim();
            if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) || v == "0")
                return false;
            return true;
        }

        private static bool IsPlausibleFiscalEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            try
            {
                var trimmed = email.Trim();
                var addr = new MailAddress(trimmed);
                return string.Equals(addr.Address, trimmed, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Lista única de pendências para o formulário de adesão fiscal (mesmo padrão no cliente).</summary>
        private static List<string> BuildFiscalOnboardingValidationIssues(
            bool declaresHasCnpj,
            string? cpf,
            string? cnpj,
            string? contactEmail,
            string? contactPhone,
            bool aceiteLgpd)
        {
            var issues = new List<string>();
            var phoneDigits = System.Text.RegularExpressions.Regex.Replace(contactPhone ?? "", @"[^\d]", "");

            // CPF — sempre obrigatório
            var cpfDigits = System.Text.RegularExpressions.Regex.Replace(cpf ?? "", @"[^\d]", "");
            if (cpfDigits.Length == 0)
                issues.Add("CPF — não informado");
            else if (cpfDigits.Length < 11)
                issues.Add("CPF — incompleto (11 dígitos)");
            else if (!DocumentValidator.IsValidCPF(cpfDigits))
                issues.Add("CPF — inválido");

            // CNPJ — obrigatório apenas quando declarado
            if (declaresHasCnpj)
            {
                var cnpjDigits = System.Text.RegularExpressions.Regex.Replace(cnpj ?? "", @"[^\d]", "");
                if (cnpjDigits.Length == 0)
                    issues.Add("CNPJ — não informado");
                else if (cnpjDigits.Length < 14)
                    issues.Add("CNPJ — incompleto (14 dígitos)");
                else if (!DocumentValidator.IsValidCNPJ(cnpjDigits))
                    issues.Add("CNPJ — inválido");
            }

            if (string.IsNullOrWhiteSpace(contactEmail))
                issues.Add("E-mail — não informado");
            else if (!IsPlausibleFiscalEmail(contactEmail))
                issues.Add("E-mail — inválido");

            if (phoneDigits.Length == 0)
                issues.Add("Telefone / WhatsApp — não informado");
            else if (phoneDigits.Length < 10)
                issues.Add("Telefone / WhatsApp — informe com DDD (mínimo 10 dígitos)");

            if (!aceiteLgpd)
                issues.Add("Termos de Uso e Política de Privacidade (LGPD) — aceite obrigatório");

            return issues;
        }

        private async Task<IActionResult> ReturnFiscalOnboardingViewWithValidationAsync(
            int coworkingAddressId,
            bool declaresHasCnpjBool,
            string cpf,
            string cnpj,
            string contactEmail,
            string? contactPhone,
            bool aceiteLgpd,
            IReadOnlyList<string> issues,
            string? validationTitleOverride,
            CancellationToken cancellationToken,
            string? serviceType = null)
        {
            ViewBag.CoworkingAddressId = coworkingAddressId;
            await TrySetFiscalOnboardingPartnerDisplayAsync(coworkingAddressId, cancellationToken);
            ViewBag.DeclaresHasCnpj = declaresHasCnpjBool;
            ViewBag.CpfValue = cpf;
            ViewBag.CnpjValue = cnpj;
            ViewBag.ContactEmail = contactEmail;
            ViewBag.ContactPhone = contactPhone;
            ViewBag.FiscalAcceptLgpdTerms = aceiteLgpd;
            ViewBag.FiscalShowLgpdError = issues.Any(static i => i.Contains("LGPD", StringComparison.Ordinal));
            ViewBag.FiscalValidationIssues = issues.ToList();
            var resolvedServiceType = serviceType?.ToLowerInvariant() ?? "";
            ViewBag.ServiceType = resolvedServiceType;
            if (!string.IsNullOrEmpty(validationTitleOverride))
                ViewBag.FiscalValidationTitle = validationTitleOverride;
            return View("FiscalOnboarding");
        }

        private sealed record SyncClientResult(bool Success, string? ErrorMessage);

        private async Task<SyncClientResult> SyncLoggedInClientFromFiscalOnboardingAsync(
            bool declaresHasCnpj,
            string? cpf,
            string? cnpj,
            string contactEmail,
            string? contactPhone,
            bool isFiscal,
            CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return new SyncClientResult(false, "Usuário não encontrado.");

            var email = (contactEmail ?? "").Trim().ToLowerInvariant();
            if (!string.Equals(user.Email.Trim().ToLowerInvariant(), email, StringComparison.Ordinal))
                return new SyncClientResult(false, "Use o e-mail da sua conta logada para continuar.");

            var cpfDigits = System.Text.RegularExpressions.Regex.Replace(cpf ?? "", @"[^\d]", "");
            var cnpjDigits = System.Text.RegularExpressions.Regex.Replace(cnpj ?? "", @"[^\d]", "");
            var taxDigits = declaresHasCnpj ? cnpjDigits : cpfDigits;

            string? razaoSocial = null;
            if (isFiscal && declaresHasCnpj && taxDigits.Length == 14)
            {
                var cnpjCheck = await _openCnpjQueryService.ValidateForFiscalOnboardingAsync(taxDigits, cancellationToken);
                if (!cnpjCheck.Success)
                    return new SyncClientResult(false, cnpjCheck.ErrorMessage ?? "CNPJ não aprovado para endereço fiscal.");
                razaoSocial = cnpjCheck.RazaoSocial;
            }

            var phoneDigits = System.Text.RegularExpressions.Regex.Replace(contactPhone ?? "", @"[^\d]", "");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                client = new Client
                {
                    UserId = userId,
                    ContactEmail = user.Email,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Clients.Add(client);
            }

            if (cpfDigits.Length == 11)
                client.CPF = $"{cpfDigits[..3]}.{cpfDigits[3..6]}.{cpfDigits[6..9]}-{cpfDigits[9..]}";
            if (declaresHasCnpj && cnpjDigits.Length == 14)
                client.CNPJ = $"{cnpjDigits[..2]}.{cnpjDigits[2..5]}.{cnpjDigits[5..8]}/{cnpjDigits[8..12]}-{cnpjDigits[12..]}";
            client.ContactPhone = phoneDigits;
            client.ContactEmail = user.Email;
            client.ContactName = !string.IsNullOrWhiteSpace(razaoSocial)
                ? razaoSocial
                : ClientDisplayName.NullIfPlaceholder(client.ContactName, user.Email)
                    ?? ClientDisplayName.NullIfPlaceholder(user.FullName, user.Email);
            if (ClientDisplayName.IsPlaceholderName(user.FullName, user.Email))
                user.FullName = client.ContactName ?? string.Empty;
            client.RazaoSocial = razaoSocial ?? client.RazaoSocial;
            client.UpdatedAt = DateTime.UtcNow;
            user.ClientHasCnpj = declaresHasCnpj;
            user.Phone = phoneDigits;
            await _context.SaveChangesAsync(cancellationToken);
            return new SyncClientResult(true, null);
        }

        private async Task<IActionResult> ReturnFiscalOnboardingBlockedUiAsync(
            int coworkingAddressId,
            bool declaresHasCnpjBool,
            string cpf,
            string cnpj,
            string contactEmail,
            string? contactPhone,
            bool aceiteLgpd,
            FiscalOnboardingStartBlockedKind blockedKind,
            string? message,
            CancellationToken cancellationToken,
            string? serviceType = null)
        {
            ViewBag.CoworkingAddressId = coworkingAddressId;
            await TrySetFiscalOnboardingPartnerDisplayAsync(coworkingAddressId, cancellationToken);
            ViewBag.DeclaresHasCnpj = declaresHasCnpjBool;
            ViewBag.CpfValue = cpf;
            ViewBag.CnpjValue = cnpj;
            ViewBag.ContactEmail = contactEmail;
            ViewBag.ContactPhone = contactPhone;
            ViewBag.FiscalAcceptLgpdTerms = aceiteLgpd;
            ViewBag.FiscalShowLgpdError = false;
            ViewBag.FiscalValidationIssues = null;
            ViewBag.ServiceType = serviceType?.ToLowerInvariant() ?? "";
            ViewBag.FiscalOnboardingBlockedKind = blockedKind switch
            {
                FiscalOnboardingStartBlockedKind.EmailMismatchForTaxId => "EmailMismatch",
                FiscalOnboardingStartBlockedKind.AlreadyHasFiscalAddressActive => "AlreadyHasAddress",
                FiscalOnboardingStartBlockedKind.AlreadyRegistered => "AlreadyRegistered",
                _ => null
            };
            ViewBag.FiscalOnboardingBlockedMessage = message;
            var loginReturnUrl = Url.Action(ActionNames.FiscalOnboarding, ControllerNames.Client, new { coworkingAddressId, serviceType });
            ViewBag.FiscalOnboardingLoginUrl = Url.Action(ActionNames.Login, ControllerNames.Auth, new { returnUrl = loginReturnUrl });
            return View("FiscalOnboarding");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalOnboarding(
            int coworkingAddressId,
            string? declaresHasCnpj,
            string? cpf,
            string? cnpj,
            string contactEmail,
            string? contactPhone,
            string? serviceType = null,
            string? acceptLgpdTerms = null,
            CancellationToken cancellationToken = default)
        {
            var isCommercial = string.Equals(serviceType?.Trim(), "comercial", StringComparison.OrdinalIgnoreCase);
            var declaresHasCnpjBool = ParseDeclaresHasCnpjFromForm(declaresHasCnpj);
            var acceptLgpdRaw = acceptLgpdTerms ?? Request.Form["acceptLgpdTerms"].ToString();
            var aceiteLgpd = StringBooleanValue.IsTrue(acceptLgpdRaw)
                || string.Equals(acceptLgpdRaw?.Trim(), "on", StringComparison.OrdinalIgnoreCase);

            var issues = BuildFiscalOnboardingValidationIssues(declaresHasCnpjBool, cpf, cnpj, contactEmail, contactPhone, aceiteLgpd);
            if (issues.Count > 0)
            {
                return await ReturnFiscalOnboardingViewWithValidationAsync(
                    coworkingAddressId, declaresHasCnpjBool, cpf ?? "", cnpj ?? "", contactEmail ?? "",
                    contactPhone, aceiteLgpd, issues, validationTitleOverride: null, cancellationToken, serviceType);
            }

            // Usuário já logado: atualiza perfil mínimo e cria contrato (caminho mais curto que cadastro anônimo)
            if (User?.Identity?.IsAuthenticated == true)
            {
                var syncResult = await SyncLoggedInClientFromFiscalOnboardingAsync(
                    declaresHasCnpjBool, cpf, cnpj, contactEmail, contactPhone, isFiscal: !isCommercial, cancellationToken);
                if (!syncResult.Success)
                {
                    var syncErrors = new List<string> { syncResult.ErrorMessage ?? "Não foi possível atualizar seus dados." };
                    return await ReturnFiscalOnboardingViewWithValidationAsync(
                        coworkingAddressId, declaresHasCnpjBool, cpf ?? "", cnpj ?? "", contactEmail ?? "",
                        contactPhone, aceiteLgpd, syncErrors, validationTitleOverride: "Não foi possível continuar:", cancellationToken, serviceType);
                }
                TempData[SuccessMessage.TempDataKey] = "Dados confirmados! Escolha abaixo a forma de pagamento.";
                return RedirectToAction(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId, serviceType });
            }

            var req = new FiscalOnboardingStartRequest
            {
                CoworkingAddressId = coworkingAddressId,
                DeclaresHasCnpj = declaresHasCnpjBool,
                TaxId = declaresHasCnpjBool ? (cnpj ?? "") : (cpf ?? ""),
                Cpf = cpf ?? "",
                ContactEmail = contactEmail ?? "",
                ContactPhone = contactPhone
            };

            // ── Fluxo Endereço Comercial: criar conta + contrato + logar + PaymentDetails ──
            // ── Fluxo unificado: criar conta + contrato + logar + PaymentDetails ──
            // Fiscal: valida CNPJ via OpenCnpj (situação cadastral). Sem restrição de CNAE. Comercial: mesma jornada, sem essa consulta.
            var isFiscal = !isCommercial;
            var serviceDescription = isFiscal ? "Endereço Fiscal" : "Endereço Comercial";

            var onboardingResult = await RegisterClientAndCreateContractAsync(
                req, validateCnpjForFiscal: isFiscal, serviceDescription, cancellationToken,
                serviceCode: isFiscal ? ServiceCodes.FiscalAddressContracting : ServiceCodes.CommercialAddressContracting);

            if (!onboardingResult.Success)
            {
                if (onboardingResult.BlockedKind != FiscalOnboardingStartBlockedKind.None)
                {
                    return await ReturnFiscalOnboardingBlockedUiAsync(
                        coworkingAddressId, declaresHasCnpjBool, cpf ?? "", cnpj ?? "", contactEmail ?? "",
                        contactPhone, aceiteLgpd, onboardingResult.BlockedKind,
                        onboardingResult.ErrorMessage, cancellationToken, serviceType);
                }
                var errors = new List<string> { onboardingResult.ErrorMessage ?? "Não foi possível processar o cadastro." };
                return await ReturnFiscalOnboardingViewWithValidationAsync(
                    coworkingAddressId, declaresHasCnpjBool, cpf ?? "", cnpj ?? "", contactEmail ?? "",
                    contactPhone, aceiteLgpd, errors,
                    validationTitleOverride: "Não foi possível continuar:", cancellationToken, serviceType);
            }

            await SignInCommercialUserAsync(onboardingResult.User!);
            TempData[SuccessMessage.TempDataKey] = "Cadastro realizado com sucesso! Escolha abaixo a forma de pagamento que preferir.";
            return RedirectToAction(ActionNames.PaymentDetails, new { id = onboardingResult.PaymentId });
        }

        /// <summary>
        /// Cria conta (User + Client) + Contrato + Payment para qualquer tipo de onboarding (Fiscal ou Comercial).
        /// Implementado no controller para evitar dependência circular no DI
        /// (IContractService → ISubscriptionService → IPaymentService → IFiscalOnboardingService → IContractService).
        /// Para Endereço Fiscal com CNPJ, consulta OpenCnpj (situação cadastral). Não restringe CNAE.
        /// </summary>
        private async Task<CommercialOnboardingFullResult> RegisterClientAndCreateContractAsync(
            FiscalOnboardingStartRequest req, bool validateCnpjForFiscal, string serviceDescription, CancellationToken ct, string? serviceCode = null)
        {
            var email = (req.ContactEmail ?? "").Trim().ToLowerInvariant();

            // Verificar e-mail duplicado
            var emailExists = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == email, ct);
            if (emailExists)
                return CommercialOnboardingFullResult.Blocked(
                    FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                    "Identificamos que este e-mail já possui um cadastro na plataforma. Faça login para continuar.");

            var taxDigits = System.Text.RegularExpressions.Regex.Replace(req.TaxId ?? "", @"[^\d]", "");
            var cpfDigitsReq = System.Text.RegularExpressions.Regex.Replace(req.Cpf ?? "", @"[^\d]", "");

            // Verificar CPF duplicado (sempre)
            if (cpfDigitsReq.Length == 11)
            {
                var cpfFormatted = $"{cpfDigitsReq[..3]}.{cpfDigitsReq[3..6]}.{cpfDigitsReq[6..9]}-{cpfDigitsReq[9..]}";
                if (await _context.Clients.AsNoTracking().AnyAsync(c => c.CPF == cpfFormatted, ct))
                    return CommercialOnboardingFullResult.Blocked(
                        FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                        "Identificamos que este CPF já possui um cadastro na plataforma. Faça login para continuar.");
            }

            // Verificar CNPJ duplicado (quando declarado)
            if (req.DeclaresHasCnpj && taxDigits.Length == 14)
            {
                var cnpjFormatted = $"{taxDigits[..2]}.{taxDigits[2..5]}.{taxDigits[5..8]}/{taxDigits[8..12]}-{taxDigits[12..]}";
                if (await _context.Clients.AsNoTracking().AnyAsync(c => c.CNPJ == cnpjFormatted, ct))
                    return CommercialOnboardingFullResult.Blocked(
                        FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                        "Identificamos que este CNPJ já possui um cadastro na plataforma. Faça login para continuar.");
            }

            // Validar endereço
            var addressEntity = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == req.CoworkingAddressId, ct);
            if (addressEntity == null)
                return CommercialOnboardingFullResult.Fail("Endereço não encontrado.");
            if (!addressEntity.Coworking.IsActive
                || addressEntity.AddressStatus != AddressStatus.Active
                || !addressEntity.IsAvailable)
                return CommercialOnboardingFullResult.Fail("Este endereço não está disponível para contratação no momento.");

            // Para Endereço Fiscal com CNPJ: validar situação cadastral via OpenCnpj (sem bloqueio por CNAE)
            string? razaoSocial = null;
            if (validateCnpjForFiscal && req.DeclaresHasCnpj && taxDigits.Length == 14)
            {
                OpenCnpjValidationResult cnpjCheck;
                try
                {
                    cnpjCheck = await _openCnpjQueryService.ValidateForFiscalOnboardingAsync(taxDigits, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FiscalOnboarding: falha na consulta OpenCnpj para {TaxDigits}.", taxDigits);
                    return CommercialOnboardingFullResult.Fail("Não foi possível validar o CNPJ no momento. Tente novamente em instantes.");
                }
                if (!cnpjCheck.Success)
                    return CommercialOnboardingFullResult.Fail(cnpjCheck.ErrorMessage ?? "CNPJ não aprovado na validação para endereço fiscal.");
                razaoSocial = cnpjCheck.RazaoSocial;
            }

            var phoneDigits = System.Text.RegularExpressions.Regex.Replace(req.ContactPhone ?? "", @"[^\d]", "");
            var contactName = ClientDisplayName.NullIfPlaceholder(razaoSocial, email);

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Client, ct);
            if (role == null)
                return CommercialOnboardingFullResult.Fail("Configuração interna inválida. Contate o suporte.");

            // Criar usuário com senha aleatória (cliente definirá nova senha via link enviado por e-mail)
            var randomPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = contactName ?? string.Empty,
                Phone = phoneDigits,
                IsActive = true,
                EmailVerified = false,
                MustChangePassword = true,
                ClientHasCnpj = req.DeclaresHasCnpj,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

            // Criar cliente — CPF sempre salvo; CNPJ salvo quando declarado
            string? cnpjToSave = null;
            string? cpfToSave  = null;

            if (cpfDigitsReq.Length == 11)
                cpfToSave = $"{cpfDigitsReq[..3]}.{cpfDigitsReq[3..6]}.{cpfDigitsReq[6..9]}-{cpfDigitsReq[9..]}";

            if (req.DeclaresHasCnpj && taxDigits.Length == 14)
                cnpjToSave = $"{taxDigits[..2]}.{taxDigits[2..5]}.{taxDigits[5..8]}/{taxDigits[8..12]}-{taxDigits[12..]}";

            var client = new Client
            {
                UserId = user.Id,
                ContactEmail = email,
                ContactName = contactName,
                RazaoSocial = razaoSocial,
                CNPJ = cnpjToSave,
                CPF = cpfToSave,
                ContactPhone = phoneDigits,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(ct);

            // Resolver o ID do serviço contratado (fiscal ou comercial) para registrar no contrato
            if (string.IsNullOrEmpty(serviceCode))
                return CommercialOnboardingFullResult.Fail("Tipo de serviço não informado. Contate o suporte.");

            var resolvedServiceId = await _context.Services.AsNoTracking()
                .Where(s => s.Code == serviceCode)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (!resolvedServiceId.HasValue)
                return CommercialOnboardingFullResult.Fail($"Serviço '{serviceCode}' não encontrado. Verifique a configuração do sistema.");

            // Criar contrato — sem PIX/boleto pré-gerado; usuário escolhe em PaymentDetails
            Contract contract;
            try
            {
                contract = await _contractService.CreateContractAsync(
                    client.Id,
                    req.CoworkingAddressId,
                    addressEntity.BasePrice,
                    resolvedServiceId.Value,
                    assistedSaleAdminId: null,
                    initialPaymentMethod: null,
                    cardPlanType: null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Onboarding: falha ao criar contrato para cliente {ClientId}", client.Id);
                return CommercialOnboardingFullResult.Fail($"Não foi possível criar o contrato: {ex.Message}");
            }

            // Localizar pagamento pendente gerado pelo contrato
            var subscription = await _context.Subscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ContractId == contract.Id, ct);
            if (subscription == null)
                return CommercialOnboardingFullResult.Fail("Assinatura não gerada. Contate o suporte.");

            var payment = await _context.Payments.AsNoTracking()
                .Where(p => p.SubscriptionId == subscription.Id && p.Status == PaymentStatus.Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (payment == null)
                return CommercialOnboardingFullResult.Fail("Pagamento não gerado. Contate o suporte.");

            // Gerar token de definição de senha (72 h) e enviar e-mail de boas-vindas
            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(72);
            await _context.SaveChangesAsync(ct);
            try
            {
                await _emailService.SendClientWelcomeEmailAsync(email, contactName, user.PasswordResetToken, serviceDescription);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Onboarding: falha ao enviar e-mail de boas-vindas para {Email}.", email);
            }

            _logger.LogInformation(
                "Onboarding ({ServiceDescription}): UserId={UserId} ClientId={ClientId} ContractId={ContractId} PaymentId={PaymentId}.",
                serviceDescription, user.Id, client.Id, contract.Id, payment.Id);

            return CommercialOnboardingFullResult.Ok(user, payment.Id);
        }

        /// <summary>Faz o sign-in via cookie para o cliente recém-criado no onboarding comercial.</summary>
        private async Task SignInCommercialUserAsync(User user)
        {
            var roles = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToListAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, ClientDisplayName.Resolve(user.FullName, user.Email))
            };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
            if (user.ClientHasCnpj == true)
                claims.Add(new Claim(AuthConstants.ClientHasCnpjClaimType, "true"));

            var identity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, principal);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email ?? "");
            HttpContext.Session.SetString(SessionKeys.UserName, ClientDisplayName.Resolve(user.FullName, user.Email));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> FiscalOnboardingPayment(Guid token, CancellationToken cancellationToken = default)
        {
            var vm = await _fiscalOnboardingService.GetPaymentViewByPublicTokenAsync(token, cancellationToken);
            if (vm == null)
                return NotFound();
            ViewData[ViewDataKeys.Title] = "Pagamento PIX — adesão";
            return View(vm);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ChooseLocation(string? region = null, string? city = null, string? state = null, int page = 1, CancellationToken cancellationToken = default)
        {
            const int pageSize = 10;
            var fullAddressList = await _coworkingService.GetAvailableAddressesAsync();
            ViewBag.Regions = fullAddressList
                .Where(a => a.Location != null && !string.IsNullOrEmpty(a.Location.Region))
                .Select(a => a.Location!.Region!)
                .Distinct()
                .OrderBy(r => r)
                .ToList();
            ViewBag.States = fullAddressList
                .Where(a => a.Location != null)
                .Select(a => a.Location!.State)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
            ViewBag.Cities = fullAddressList
                .Select(a => a.Location?.City ?? a.City)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            var filteredAddresses = fullAddressList.AsEnumerable();
            if (!string.IsNullOrEmpty(region))
                filteredAddresses = filteredAddresses.Where(a => a.Location != null && a.Location.Region == region);
            if (!string.IsNullOrEmpty(city))
                filteredAddresses = filteredAddresses.Where(a => (a.Location?.City ?? a.City) == city);
            if (!string.IsNullOrEmpty(state))
                filteredAddresses = filteredAddresses.Where(a => a.Location != null && a.Location.State == state);
            var sortedAddresses = filteredAddresses
                .OrderBy(a => a.Location?.State ?? a.State)
                .ThenBy(a => a.Location?.City ?? a.City)
                .ThenBy(a => a.Address)
                .ToList();
            var totalItems = sortedAddresses.Count;
            var totalPages = totalItems > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));
            var pagedAddresses = sortedAddresses
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var contractedAddressIds = new HashSet<int>();
            int? activeContractId = null;
            var pendingPaymentByAddressId = new Dictionary<int, int>();
            var pendingPaymentStatusByAddressId = new Dictionary<int, PaymentStatus>();
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsNoTrackingAsync(userId, cancellationToken);
                if (client != null)
                {
                    // Heal: pagamento Paid + contrato ainda PendingPayment (race do cartão) → mesma fonte de verdade do Confirm/MyPayments
                    await _clientAppService.PromotePendingContractsToActiveIfPaidAsync(client.Id, cancellationToken);

                    var ids = await _clientAppService.GetContractAddressIdsByClientIdAsync(client.Id, cancellationToken);
                    contractedAddressIds = new HashSet<int>(ids);
                    activeContractId = await _clientAppService.GetActiveContractIdByClientIdAsync(client.Id, cancellationToken);

                    var pendingContracts = await _clientAppService.GetPendingContractsWithSubscriptionPaymentsAsync(client.Id, cancellationToken);
                    foreach (var c in pendingContracts)
                    {
                        var payment = c.Subscription?.Payments?
                            .OrderByDescending(p => p.CreatedAt)
                            .FirstOrDefault(p =>
                                p.Status == PaymentStatus.Pending
                                || p.Status == PaymentStatus.Processing
                                || p.Status == PaymentStatus.AwaitingValidation);
                        if (payment != null)
                        {
                            pendingPaymentByAddressId[c.CoworkingAddressId] = payment.Id;
                            pendingPaymentStatusByAddressId[c.CoworkingAddressId] = payment.Status;
                        }
                    }
                }
            }
            ViewBag.PendingPaymentByAddressId = pendingPaymentByAddressId;
            ViewBag.PendingPaymentStatusByAddressId = pendingPaymentStatusByAddressId;
            ViewBag.ContractedAddressIds = contractedAddressIds;
            ViewBag.ActiveContractId = activeContractId;
            var coworkingIds = pagedAddresses.Select(a => a.CoworkingId).Distinct().ToList();
            var mainPhotos = new Dictionary<int, CoworkingPhoto>();
            foreach (var coworkingId in coworkingIds)
            {
                var mainPhoto = await _clientAppService.GetMainPhotoByCoworkingIdAsync(coworkingId, cancellationToken);
                if (mainPhoto != null)
                    mainPhotos[coworkingId] = mainPhoto;
            }
            ViewBag.MainPhotos = mainPhotos;
            ViewBag.Region = region;
            ViewBag.City = city;
            ViewBag.State = state;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            return View(pagedAddresses);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> AddressDetail(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            if (coworkingAddressId <= 0)
                return RedirectToAction(ActionNames.Index, ControllerNames.Home);

            var listing = await _partnerListingService.GetPartnerListingAsync(coworkingAddressId, cancellationToken);
            if (listing == null)
                return RedirectToAction(ActionNames.FiscalAddress, ControllerNames.Home);

            ViewBag.Photos = listing.Photos;
            ViewBag.TrustDocuments = listing.TrustDocuments;
            ViewBag.IsSolePublishedAddress = listing.IsSolePublishedAddress;
            ViewBag.FiscalOnboardingUrl = Url.Action(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId });
            ViewBag.CommercialOnboardingUrl = Url.Action(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId, serviceType = "comercial" });
            ViewBag.ChooseLocationUrl = listing.IsSolePublishedAddress
                ? Url.Action(ActionNames.Index, ControllerNames.Home)
                : Url.Action(ActionNames.ChooseLocation);

            return View(listing.Address);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ViewPartnerDocument(int coworkingAddressId, CoworkingDocumentType type, bool download = false, CancellationToken cancellationToken = default)
        {
            var file = await _partnerListingService.GetPublicTrustDocumentAsync(coworkingAddressId, type, cancellationToken);
            if (file == null)
                return NotFound();
            if (download)
                return File(file.Content, file.ContentType, file.FileName);
            return File(file.Content, file.ContentType);
        }

        [HttpGet]
        public async Task<IActionResult> UploadDocuments(int contractId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null && !User.IsInRole(Administrator))
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth);

            if (client != null && !User.IsInRole(Administrator))
            {
                var incomplete = await RedirectToProfileIfContractPartyIncompleteAsync(
                    userId,
                    Url.Action(ActionNames.UploadDocuments, new { contractId }),
                    cancellationToken);
                if (incomplete != null)
                    return incomplete;
            }

            var page = await _clientOnboardingDocumentService.GetPageAsync(contractId, client?.Id ?? 0, User.IsInRole(Administrator), cancellationToken);
            if (!page.Success || page.Contract == null)
            {
                TempData[ErrorMessages.TempDataKey] = page.ErrorMessage ?? ErrorMessages.ContractNotFound;
                return RedirectToAction(ActionNames.MyContracts);
            }
            if (page.CanViewGeneratedDocuments)
                return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });

            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadRequestLimits.MaxRequestBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimits.MaxRequestBodyBytes)]
        public async Task<IActionResult> UploadDocuments(
            int contractId,
            ClientOnboardingIdentityPath identityPath,
            IFormFile? rgFrontFile,
            IFormFile? rgBackFile,
            IFormFile? cpfFile,
            IFormFile? cnhFile,
            IFormFile? proofFile,
            CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.UploadDocuments, new { contractId });
            }

            var result = await _clientOnboardingDocumentService.UploadSetAsync(new ClientOnboardingSetUploadInput
            {
                ContractId = contractId,
                ClientId = client.Id,
                Path = identityPath,
                RgFront = await ToOnboardingUploadAsync(rgFrontFile, cancellationToken),
                RgBack = await ToOnboardingUploadAsync(rgBackFile, cancellationToken),
                Cpf = await ToOnboardingUploadAsync(cpfFile, cancellationToken),
                Cnh = await ToOnboardingUploadAsync(cnhFile, cancellationToken),
                ProofOfResidence = await ToOnboardingUploadAsync(proofFile, cancellationToken)
            }, cancellationToken);

            if (!result.Success)
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage;
            else
                TempData[SuccessMessage.TempDataKey] = result.SuccessMessage;
            return RedirectToAction(ActionNames.UploadDocuments, new { contractId });
        }

        private static async Task<ClientOnboardingUploadInput?> ToOnboardingUploadAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return null;
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            return new ClientOnboardingUploadInput
            {
                FileName = file.FileName,
                ContentType = file.ContentType ?? string.Empty,
                Content = stream.ToArray()
            };
        }

        [HttpGet]
        public async Task<IActionResult> ViewOnboardingDocument(int id, bool download = false, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            var file = await _clientOnboardingDocumentService.GetFileAsync(
                id,
                client?.Id,
                null,
                User.IsInRole(Administrator),
                cancellationToken);
            if (file == null)
                return NotFound();
            if (download)
                return File(file.Content, file.ContentType, file.FileName);
            return File(file.Content, file.ContentType);
        }

        [HttpGet]
        public async Task<IActionResult> AvailableAddresses(int locationId, CancellationToken cancellationToken = default)
        {
            if (locationId <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidLocation;
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            var location = await _clientAppService.GetLocationByIdAsync(locationId, cancellationToken);
            if (location == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.LocationNotFound;
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            var addresses = await _coworkingService.GetAvailableAddressesAsync(locationId);
            var contractedAddressIds = new HashSet<int>();
            int? activeContractId = null;
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsNoTrackingAsync(userId, cancellationToken);
                if (client != null)
                {
                    await _clientAppService.PromotePendingContractsToActiveIfPaidAsync(client.Id, cancellationToken);
                    var ids = await _clientAppService.GetContractAddressIdsByClientIdAsync(client.Id, cancellationToken);
                    contractedAddressIds = new HashSet<int>(ids);
                    activeContractId = await _clientAppService.GetActiveContractIdByClientIdAsync(client.Id, cancellationToken);
                }
            }
            ViewBag.ContractedAddressIds = contractedAddressIds;
            ViewBag.ActiveContractId = activeContractId;
            var coworkingIds = addresses.Select(a => a.CoworkingId).Distinct().ToList();
            var mainPhotos = new Dictionary<int, CoworkingPhoto>();
            foreach (var coworkingId in coworkingIds)
            {
                var mainPhoto = await _clientAppService.GetMainPhotoByCoworkingIdAsync(coworkingId, cancellationToken);
                if (mainPhoto != null)
                    mainPhotos[coworkingId] = mainPhoto;
            }
            ViewBag.LocationId = locationId;
            ViewBag.Location = location;
            ViewBag.MainPhotos = mainPhotos;
            return View(addresses);
        }
        [HttpGet]
        public async Task<IActionResult> CompleteContractAfterLogin(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth, new { returnUrl = Url.Action(ActionNames.CompleteContractAfterLogin, ControllerNames.Client, new { coworkingAddressId }) });
            }
            var userId = int.Parse(userIdStr);
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            var returnUrlComplete = Url.Action(ActionNames.CompleteContractAfterLogin, ControllerNames.Client, new { coworkingAddressId });
            if (client == null)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForFiscalAddress;
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId });
            }
            var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
            var missingRequired = !hasDocument || string.IsNullOrWhiteSpace(client.ContactPhone);
            if (missingRequired)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForContract;
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId });
            }
            return RedirectToAction(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId });
        }
        /// <summary>Redireciona para criação de contrato com forma padrão e PaymentDetails (página SelectPaymentPlan removida).</summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SelectPaymentPlan(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            return RedirectToAction(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId });
        }

        /// <summary>
        /// Ponto de entrada para contratação de endereço via AddressDetail.
        /// Se não autenticado → redireciona para FiscalOnboarding (coleta de dados).
        /// Se autenticado → usa dados do perfil existente, cria contrato e vai direto para PaymentDetails.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CreateContractAndGoToPayment(int coworkingAddressId, string? serviceType = null, CancellationToken cancellationToken = default)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId, serviceType });
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId, serviceType });
            }
            var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
            var missingRequired = !hasDocument || string.IsNullOrWhiteSpace(client.ContactPhone);
            if (missingRequired)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForContract;
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId, serviceType });
            }
            var addressEntity = await _clientAppService.GetCoworkingAddressWithDetailsAsync(coworkingAddressId, cancellationToken);
            if (addressEntity == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            if (!addressEntity.Coworking.IsActive || addressEntity.AddressStatus != AddressStatus.Active || !addressEntity.IsAvailable)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotAvailableForContract;
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            var existingContractForAddress = await _clientAppService.GetExistingContractForAddressAsync(client.Id, coworkingAddressId, cancellationToken);
            if (existingContractForAddress != null)
            {
                var statusMsg = existingContractForAddress.Status.ToDisplayStringLower();
                TempData[ErrorMessages.TempDataKey] = $"Você já possui um contrato {statusMsg} para este endereço. Número do contrato: {existingContractForAddress.ContractNumber}";
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            if (await _clientMenuService.ClientHasActiveContractAsync(userId))
            {
                var activeContractIdVal = await _clientAppService.GetActiveContractIdByClientIdAsync(client.Id, cancellationToken);
                var activeContract = activeContractIdVal ?? 0;
                var contractDetailsUrl = activeContract != 0
                    ? Url.Action(ActionNames.ContractDetails, new { id = activeContract })
                    : Url.Action(ActionNames.MyContracts);
                var msg = "Você já possui um endereço fiscal ativo. Para adicionar outro, cancele ou encerre o contrato atual no menu <strong><a href=\"" + contractDetailsUrl + "\" class=\"alert-link text-white text-decoration-underline\">Contrato de Endereço Fiscal</a></strong>.";
                TempData[TempDataKeys.Info] = InfoMessages.Generic(msg);
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            var existingContractInLocation = await _clientAppService.GetExistingContractInLocationAsync(client.Id, addressEntity.LocationId, cancellationToken);
            if (existingContractInLocation != null)
            {
                var locationInfo = existingContractInLocation.CoworkingAddress?.Location != null
                    ? $"{existingContractInLocation.CoworkingAddress.Location.City}/{existingContractInLocation.CoworkingAddress.Location.State}"
                    : $"{addressEntity.City}/{addressEntity.State}";
                var statusLower = existingContractInLocation.Status.ToDisplayStringLower();
                TempData[ErrorMessages.TempDataKey] = $"Você já possui um contrato {statusLower} nesta localidade ({locationInfo}). Número do contrato: {existingContractInLocation.ContractNumber}";
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            var hiddenRaw = await _systemConfig.GetValueAsync(PaymentConfigKey.HiddenPaymentOptions.ToConfigKey());
            var hiddenSet = (hiddenRaw ?? "").Split(',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasCard = _addressPricingService.SupportsCardPayment(addressEntity.BasePrice);
            string option;
            if (!hiddenSet.Contains("PixAnnual"))
                option = "PixAnnual";
            else if (!hiddenSet.Contains("PIX"))
                option = "PIX";
            else if (hasCard && !hiddenSet.Contains("Card1x"))
                option = "Card1x";
            else if (hasCard && !hiddenSet.Contains("CardMonthly"))
                option = "CardMonthly";
            else
                option = "PIX";
            var surchargePercent = addressEntity.CardMonthlySurchargePercent;
            var (monthlyValueForContract, initialPaymentMethod, cardPlanType) =
                await ResolveInitialContractPricingAsync(addressEntity.BasePrice, option, surchargePercent, cancellationToken);
            var isCommercialFlow = string.Equals(serviceType?.Trim(), "comercial", StringComparison.OrdinalIgnoreCase);
            var contractServiceCode = isCommercialFlow ? ServiceCodes.CommercialAddressContracting : ServiceCodes.FiscalAddressContracting;
            var resolvedContractServiceId = await _context.Services.AsNoTracking()
                .Where(s => s.Code == contractServiceCode)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!resolvedContractServiceId.HasValue)
            {
                TempData[ErrorMessages.TempDataKey] = "Configuração de serviço inválida. Contate o suporte.";
                return RedirectToAction(ActionNames.ChooseLocation);
            }
            try
            {
                var contract = await _contractService.CreateContractAsync(
                    client.Id,
                    coworkingAddressId,
                    monthlyValueForContract,
                    resolvedContractServiceId.Value,
                    assistedSaleAdminId: null,
                    initialPaymentMethod,
                    cardPlanType
                );
                var subscription = await _clientAppService.GetSubscriptionByContractIdAsync(contract.Id, cancellationToken);
                if (subscription != null)
                {
                    var payment = await _clientAppService.GetFirstPaymentBySubscriptionIdAsync(subscription.Id, cancellationToken);
                    if (payment != null)
                    {
                        var history = new PaymentHistory
                        {
                            PaymentId = payment.Id,
                            Action = PaymentHistoryActions.ContractCreated,
                            ActionBy = userId,
                            ActionAt = DateTimeHelper.GetBrazilianTime()
                        };
                        await _clientAppService.AddPaymentHistoryAsync(history, cancellationToken);
                        TempData[SuccessMessage.TempDataKey] = SuccessMessage.ContractCreated;
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = payment.Id });
                    }
                }
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ContractCreated;
                return RedirectToAction(ActionNames.ContractDetails, new { id = contract.Id });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CreateContractAndGoToPayment falhou. CoworkingAddressId: {CoworkingAddressId}", coworkingAddressId);
                TempData[ErrorMessages.TempDataKey] = $"Erro ao criar contrato: {ex.Message}";
                return RedirectToAction(ActionNames.ChooseLocation);
            }
        }
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateContract(int coworkingAddressId, string? paymentOption, CancellationToken cancellationToken = default)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(ActionNames.FiscalOnboarding, new { coworkingAddressId });
            }
            var isAjax = string.Equals(Request.Headers[HttpHeaderNames.XRequestedWith], HttpHeaderNames.XRequestedWithValue, StringComparison.OrdinalIgnoreCase);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    var msg = "Cliente não encontrado. Complete seu cadastro primeiro.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.RegisterClient) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.RegisterClient);
                }
                var addressEntity = await _clientAppService.GetCoworkingAddressWithDetailsAsync(coworkingAddressId, cancellationToken);
                if (addressEntity == null)
                {
                    var msg = "Endereço não encontrado.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.ChooseLocation) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.ChooseLocation);
                }
                if (!addressEntity.Coworking.IsActive)
                {
                    var msg = "O parceiro deste endereço está suspenso. Não é possível criar novos contratos.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }
                if (addressEntity.AddressStatus != AddressStatus.Active)
                {
                    var msg = "Este endereço não está ativo para novas vendas no momento.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }
                if (!addressEntity.IsAvailable)
                {
                    var msg = "Este endereço não está mais disponível.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }
                var existingContractForAddress = await _clientAppService.GetExistingContractForAddressAsync(client.Id, coworkingAddressId, cancellationToken);
                if (existingContractForAddress != null)
                {
                    var statusMsg = existingContractForAddress.Status.ToDisplayStringLower();
                    var msg = $"Você já possui um contrato {statusMsg} para este endereço. Número do contrato: {existingContractForAddress.ContractNumber}";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.ChooseLocation) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.ChooseLocation);
                }
                if (await _clientMenuService.ClientHasActiveContractAsync(userId))
                {
                    var activeContractIdVal = await _clientAppService.GetActiveContractIdByClientIdAsync(client.Id, cancellationToken);
                    var activeContract = activeContractIdVal ?? 0;
                    var contractDetailsUrl = activeContract != 0
                        ? Url.Action(ActionNames.ContractDetails, new { id = activeContract })
                        : Url.Action(ActionNames.MyContracts);
                    var msg = "Você já possui um endereço fiscal ativo. Para adicionar outro, cancele ou encerre o contrato atual no menu <strong><a href=\"" + contractDetailsUrl + "\" class=\"alert-link text-white text-decoration-underline\">Contrato de Endereço Fiscal</a></strong>.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.ChooseLocation) });
                    TempData[TempDataKeys.Info] = InfoMessages.Generic(msg);
                    return RedirectToAction(ActionNames.ChooseLocation);
                }
                var existingContractInLocation = await _clientAppService.GetExistingContractInLocationAsync(client.Id, addressEntity.LocationId, cancellationToken);
                if (existingContractInLocation != null)
                {
                    var locationInfo = existingContractInLocation.CoworkingAddress?.Location != null 
                        ? $"{existingContractInLocation.CoworkingAddress.Location.City}/{existingContractInLocation.CoworkingAddress.Location.State}"
                        : $"{addressEntity.City}/{addressEntity.State}";
                    var statusLower = existingContractInLocation.Status.ToDisplayStringLower();
                    var msg = $"Você já possui um contrato {statusLower} nesta localidade ({locationInfo}). Número do contrato: {existingContractInLocation.ContractNumber}";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.ChooseLocation) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.ChooseLocation);
                }
                var hiddenRaw = await _systemConfig.GetValueAsync(PaymentConfigKey.HiddenPaymentOptions.ToConfigKey());
                var hiddenSet = (hiddenRaw ?? "").Split(',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var option = (paymentOption ?? "PixAnnual").Trim();
                if (hiddenSet.Count > 0 && (string.IsNullOrEmpty(option) || hiddenSet.Contains(option)))
                {
                    TempData[ErrorMessages.TempDataKey] = "A forma de pagamento selecionada não está disponível. Escolha outra opção na tela anterior.";
                    return RedirectToAction(ActionNames.CreateContractAndGoToPayment, new { coworkingAddressId });
                }
                decimal monthlyValueForContract;
                PaymentMethod? initialPaymentMethod;
                ContractCardPlanType? cardPlanType;
                var surchargePercent = addressEntity.CardMonthlySurchargePercent;
                if (option == "Card1x" || option == "CardMonthly" || option == "PixAnnual" || option == "PIX")
                {
                    (monthlyValueForContract, initialPaymentMethod, cardPlanType) =
                        await ResolveInitialContractPricingAsync(addressEntity.BasePrice, option, surchargePercent, cancellationToken);
                }
                else
                {
                    (monthlyValueForContract, initialPaymentMethod, cardPlanType) =
                        await ResolveInitialContractPricingAsync(addressEntity.BasePrice, "PixAnnual", surchargePercent, cancellationToken);
                }
                var fiscalSvcId = await _context.Services.AsNoTracking()
                    .Where(s => s.Code == ServiceCodes.FiscalAddressContracting)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!fiscalSvcId.HasValue)
                {
                    var msg = "Configuração de serviço inválida. Contate o suporte.";
                    if (isAjax) return Json(new { success = false, message = msg });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.ChooseLocation);
                }
                var contract = await _contractService.CreateContractAsync(
                    client.Id,
                    coworkingAddressId,
                    monthlyValueForContract,
                    fiscalSvcId.Value,
                    assistedSaleAdminId: null,
                    initialPaymentMethod,
                    cardPlanType
                );
                var subscription = await _clientAppService.GetSubscriptionByContractIdAsync(contract.Id, cancellationToken);
                if (subscription != null)
                {
                    var payment = await _clientAppService.GetFirstPaymentBySubscriptionIdAsync(subscription.Id, cancellationToken);
                    if (payment != null)
                    {
                        var history = new PaymentHistory
                        {
                            PaymentId = payment.Id,
                            Action = PaymentHistoryActions.ContractCreated,
                            ActionBy = userId,
                            ActionAt = DateTimeHelper.GetBrazilianTime()
                        };
                        await _clientAppService.AddPaymentHistoryAsync(history, cancellationToken);
                        if (isAjax) return Json(new { success = true, redirectUrl = Url.Action(ActionNames.PaymentDetails, new { id = payment.Id }) });
                        TempData[SuccessMessage.TempDataKey] = SuccessMessage.ContractCreated;
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = payment.Id });
                    }
                }
                if (isAjax) return Json(new { success = true, redirectUrl = Url.Action(ActionNames.ContractDetails, new { id = contract.Id }) });
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ContractCreated;
                return RedirectToAction(ActionNames.ContractDetails, new { id = contract.Id });
            }
            catch (Exception ex)
            {
                var errMsg = $"Erro ao criar contrato: {ex.Message}";
                var addressEntity = await _clientAppService.GetCoworkingAddressByIdAsync(coworkingAddressId, cancellationToken);
                if (isAjax)
                {
                    var redirectUrl = addressEntity != null
                        ? Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId })
                        : Url.Action(ActionNames.ChooseLocation);
                    return Json(new { success = false, message = errMsg, redirectUrl });
                }
                TempData[ErrorMessages.TempDataKey] = errMsg;
                if (addressEntity != null)
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                return RedirectToAction(ActionNames.ChooseLocation);
            }
        }
        [HttpGet]
        public async Task<IActionResult> ContractDetails(int id, int timelinePage = 1, int timelinePageSize = 5, CancellationToken cancellationToken = default)
        {
            try
            {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null)
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (contract.Client?.UserId != userId && !User.IsInRole(Administrator))
            {
                return Forbid();
            }
            var hasPaymentAwaitingValidation = false;
            int? paymentAwaitingValidationId = null;
            if (contract.Status == ContractStatus.PendingPayment)
            {
                var subscriptionIds = await _clientAppService.GetSubscriptionIdsByContractIdAsync(id, cancellationToken);
                if (subscriptionIds.Count > 0)
                {
                    var paymentAwaiting = await _clientAppService.GetPaymentAwaitingValidationBySubscriptionIdsAsync(subscriptionIds, cancellationToken);
                    if (paymentAwaiting != null)
                    {
                        hasPaymentAwaitingValidation = true;
                        paymentAwaitingValidationId = paymentAwaiting.Id;
                    }
                }
            }
            ViewBag.HasPaymentAwaitingValidation = hasPaymentAwaitingValidation;
            ViewBag.PaymentAwaitingValidationId = paymentAwaitingValidationId;

            if (contract.Client != null)
                await _clientAppService.PromotePendingContractsToActiveIfPaidAsync(contract.Client.Id, cancellationToken);

            KlyonHubPlatform.Application.DTOs.ContractDocuments.ContractDocumentsEnsureResult documentsEnsureResult;
            if (contract.DocumentsApproved)
            {
                documentsEnsureResult = await _contractDocumentsEnsureService.GetStatusAsync(id, cancellationToken);
            }
            else
            {
                documentsEnsureResult = new KlyonHubPlatform.Application.DTOs.ContractDocuments.ContractDocumentsEnsureResult();
                if (contract.Status == ContractStatus.Active || contract.Status == ContractStatus.PendingCancellation)
                {
                    var onboarding = await _clientOnboardingDocumentService.GetPageAsync(
                        id,
                        contract.Client?.Id ?? 0,
                        User.IsInRole(Administrator),
                        cancellationToken);
                    if (onboarding.Success)
                    {
                        ViewBag.OnboardingBanner = onboarding.Banner;
                        ViewBag.OnboardingDocumentsUrl = Url.Action(ActionNames.UploadDocuments, new { contractId = id });
                    }
                }
            }

            contract = await _contractService.GetByIdAsync(id);
            if (contract == null)
                return NotFound();

            var declarations = await _clientAppService.GetAddressDeclarationsByContractIdAsync(id, cancellationToken);
            ViewBag.Declarations = declarations;
            var payments = new List<Payment>();
            var historiesByPaymentId = new Dictionary<int, List<PaymentHistory>>();
            if (contract.Subscription != null)
            {
                payments = await _clientAppService.GetPaymentsBySubscriptionIdAsync(contract.Subscription.Id, cancellationToken);
                var paymentIds = payments.Select(p => p.Id).ToList();
                if (paymentIds.Any())
                {
                    var paymentHistories = await _clientAppService.GetPaymentHistoriesByPaymentIdsAsync(paymentIds, cancellationToken);
                    historiesByPaymentId = paymentHistories
                        .GroupBy(ph => ph.PaymentId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
            }
            var timelinePageResult = await _contractTimelineService.BuildPagedAsync(
                contract,
                payments,
                historiesByPaymentId,
                declarations,
                timelinePage,
                timelinePageSize,
                cancellationToken);
            ViewBag.Timeline = timelinePageResult.Items;
            ViewBag.TimelineTotalItems = timelinePageResult.TotalItems;
            ViewBag.TimelineCurrentPage = timelinePageResult.CurrentPage;
            ViewBag.TimelineTotalPages = timelinePageResult.TotalPages;
            ViewBag.TimelinePageSize = timelinePageResult.PageSize;
            ViewBag.CanGenerateDeclaration = documentsEnsureResult.DeclarationManualRetryAvailable;
            ViewBag.CanGenerateContract = documentsEnsureResult.ContractManualRetryAvailable;
            ViewBag.DeclarationGenerationError = documentsEnsureResult.DeclarationErrorMessage;
            ViewBag.ContractGenerationError = documentsEnsureResult.ContractErrorMessage;
            ViewBag.PartnerDocuments = await _contractPartnerDocumentService.ListAvailableForContractAsync(
                id,
                contract.Client?.Id ?? 0,
                User.IsInRole(Administrator),
                cancellationToken);
            return View(contract);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar detalhes do contrato. ContractId: {ContractId}", id);
                TempData[ErrorMessages.TempDataKey] = $"Não foi possível carregar os detalhes do contrato. Tente novamente ou acesse Meus Contratos. (Erro: {ex.Message})";
                return RedirectToAction(ActionNames.MyContracts);
            }
        }
        [HttpGet]
        public async Task<IActionResult> MyContracts(string? search = null, string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            await _clientAppService.PromotePendingContractsToActiveIfPaidAsync(client.Id, cancellationToken);
            var (contracts, totalItems) = await _clientAppService.GetMyContractsAsync(client.Id, search, status, page, pageSize, cancellationToken);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.HasActiveContract = await _clientMenuService.ClientHasActiveContractAsync(userId);
            return View(contracts);
        }
        [HttpGet]
        public async Task<IActionResult> Correspondences(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
                return RedirectToAction(ActionNames.RegisterClient);
            var list = await _correspondenceService.GetByClientIdAsync(client.Id, cancellationToken);
            return View(list);
        }
        [HttpGet]
        public async Task<IActionResult> CorrespondenceDetail(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
                return RedirectToAction(ActionNames.RegisterClient);
            var correspondence = await _correspondenceService.GetByIdAsync(id, cancellationToken);
            if (correspondence == null || correspondence.Contract.ClientId != client.Id)
                return NotFound();
            return View(correspondence);
        }
        [HttpGet]
        public async Task<IActionResult> GetCorrespondencePhoto(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null) return NotFound();
            var photo = await _context.CorrespondencePhotos
                .Include(p => p.Correspondence)
                .ThenInclude(c => c!.Contract)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (photo?.Correspondence?.Contract == null || photo.Correspondence.Contract.ClientId != client.Id)
                return NotFound();
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var fileBytes = await storageService.GetFileAsync(photo.FilePath);
            if (fileBytes == null) return NotFound();
            var contentType = photo.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            return File(fileBytes, contentType, photo.FileName);
        }
        [HttpGet]
        public async Task<IActionResult> SchedulePickup(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
                return RedirectToAction(ActionNames.RegisterClient);
            var correspondence = await _correspondenceService.GetByIdAsync(id, cancellationToken);
            if (correspondence == null || correspondence.Contract?.ClientId != client.Id)
                return NotFound();
            if (correspondence.PickupStatus != Domain.Core.Enums.CorrespondencePickupStatus.AwaitingSchedule)
                return RedirectToAction(ActionNames.CorrespondenceDetail, new { id });
            var coworkingId = correspondence.Contract?.CoworkingAddress?.CoworkingId ?? 0;
            var operatingHoursList = coworkingId > 0
                ? await _operatingHoursRepository.GetByCoworkingAsync(coworkingId, cancellationToken)
                : new List<CoworkingOperatingHours>();
            var operatingHoursDict = operatingHoursList
                .Where(h => h.IsActive && h.OpeningTime.HasValue && h.ClosingTime.HasValue)
                .ToDictionary(h => h.DayOfWeek, h => new { open = h.OpeningTime!.Value.ToString(@"hh\:mm"), close = h.ClosingTime!.Value.ToString(@"hh\:mm") });
            ViewBag.OperatingHoursJson = System.Text.Json.JsonSerializer.Serialize(operatingHoursDict);
            return View(correspondence);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SchedulePickup(int id, DateTime scheduledDate, string scheduledTime, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
                return RedirectToAction(ActionNames.RegisterClient);
            if (!TimeSpan.TryParse(scheduledTime, out var timeSpan))
            {
                TempData[ErrorMessages.TempDataKey] = "Horário inválido. Use o formato HH:mm.";
                return RedirectToAction(ActionNames.SchedulePickup, new { id });
            }
            try
            {
                await _correspondenceService.SchedulePickupAsync(id, scheduledDate, timeSpan, client.Id, cancellationToken);
                TempData[SuccessMessage.TempDataKey] = "Retirada agendada com sucesso. Compareça no endereço na data e hora escolhidas.";
                return RedirectToAction(ActionNames.CorrespondenceDetail, new { id });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.SchedulePickup, new { id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestContractCancellation(int contractId, string reason, CancellationToken cancellationToken = default)
        {
            try
            {
                var contract = await _contractService.GetByIdAsync(contractId);
                if (contract == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ContractNotFound;
                    return RedirectToAction(ActionNames.MyContracts);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                if (contract.Client.UserId != userId)
                {
                    return Forbid();
                }
                if (contract.Status != ContractStatus.Active)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyActiveContractsCanBeCancelled;
                    return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
                }
                contract.Status = ContractStatus.PendingCancellation;
                contract.CancellationRequestedAt = DateTimeHelper.GetBrazilianTime();
                contract.CancellationRequestReason = reason;
                contract.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _clientAppService.UpdateContractAsync(contract, cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CancellationRequestSent;
                return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("solicitar cancelamento", ex.Message);
                return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
            }
        }
        [HttpPost]
        public async Task<IActionResult> CancelContract(int contractId, string reason, CancellationToken cancellationToken = default)
        {
            try
            {
                await _contractService.CancelContractAsync(contractId, reason);
                return RedirectToAction(ActionNames.MyContracts);
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
                return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
            }
        }
        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId, bool download = true, CancellationToken cancellationToken = default)
        {
            var fileBytes = await _documentService.GetDocumentFileAsync(documentId);
            if (fileBytes == null)
            {
                return NotFound();
            }
            var document = await _clientAppService.GetDocumentByIdAsync(documentId, cancellationToken);
            return FileResultWithDisposition(fileBytes, "application/pdf", document?.FileName ?? "document.pdf", download);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadContractPartnerDocument(int contractId, int documentId, bool download = true, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            var file = await _contractPartnerDocumentService.GetFileForContractAsync(
                contractId,
                documentId,
                client?.Id ?? 0,
                User.IsInRole(Administrator),
                cancellationToken);
            if (file == null)
                return NotFound();
            return FileResultWithDisposition(file.Content, file.ContentType, file.FileName, download);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadKlyonOnboardingDocument(string slug, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var resolved = await _klyonOnboardingPublicDocumentService.TryGetDocumentFileForClientAsync(userId, slug, _webHostEnvironment.WebRootPath, cancellationToken);
            if (resolved == null)
                return NotFound();
            var (fullPath, downloadName) = resolved.Value;
            return PhysicalFile(fullPath, "application/pdf", downloadName);
        }
        [HttpGet]
        public async Task<IActionResult> MyPayments(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                if (User.IsInRole(Administrator))
                    return RedirectToAction(ActionNames.Payments, ControllerNames.Admin);
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var payments = await _paymentService.GetPaymentsByClientIdAsync(client.Id);
            var totalItems = payments.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var paginatedPayments = payments
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            return View(paginatedPayments);
        }
        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(int id, CancellationToken cancellationToken = default)
        {
            var payment = await _clientAppService.GetPaymentByIdWithFullDetailsAsync(id, cancellationToken);
            if (payment == null)
                return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null && !User.IsInRole(Administrator))
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth);

            if (client != null)
            {
                var authorized = false;
                if (payment.SubscriptionId.HasValue && payment.Subscription != null)
                {
                    var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
                    if (contract?.ClientId == client.Id)
                        authorized = true;
                    if (authorized && payment.Status == PaymentStatus.Paid && contract != null)
                    {
                        var onboarding = await _clientOnboardingDocumentService.GetPageAsync(
                            contract.Id,
                            client.Id,
                            User.IsInRole(Administrator),
                            cancellationToken);
                        if (onboarding.Success)
                        {
                            ViewBag.OnboardingBanner = onboarding.Banner;
                            ViewBag.OnboardingDocumentsUrl = Url.Action(ActionNames.UploadDocuments, new { contractId = contract.Id });
                        }
                        var autoRedirectUrl = await ResolvePostPaymentOnboardingRedirectUrlAsync(
                            userId,
                            client.Id,
                            contract,
                            cancellationToken);
                        if (!string.IsNullOrEmpty(autoRedirectUrl))
                        {
                            ViewBag.AutoRedirectUrl = autoRedirectUrl;
                            ViewBag.AutoRedirectDelaySeconds = PaymentSuccessAutoRedirectDelaySeconds;
                        }
                    }
                }
                else if (payment.ReservationId.HasValue)
                {
                    var reservation = await _clientAppService.GetReservationByIdAsync(payment.ReservationId.Value, cancellationToken);
                    if (reservation?.ClientId == client.Id)
                        authorized = true;
                }
                if (!authorized && !User.IsInRole(Administrator))
                    return Forbid();
            }

            ViewBag.IsAwaitingValidation = payment.Status == PaymentStatus.AwaitingValidation;
            ViewBag.IsPaid = payment.Status == PaymentStatus.Paid;
            return View(payment);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentDetails(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await PaymentDetailsCoreAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentDetails falhou. PaymentId: {PaymentId}. Verifique se todas as migrações foram aplicadas (ex.: PaymentHistories.ActionBy nullable, CreditCards.AsaasToken).", id);
                throw;
            }
        }

        private async Task<IActionResult> PaymentDetailsCoreAsync(int id, CancellationToken cancellationToken = default)
        {
            var payment = await _clientAppService.GetPaymentByIdWithFullDetailsAsync(id, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null && !User.IsInRole(Administrator))
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            bool isAuthorized = false;
            if (client != null)
            {
                if (payment.SubscriptionId.HasValue && payment.Subscription != null)
                {
                    var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
                    if (contract != null && contract.ClientId == client.Id)
                        isAuthorized = true;
                }
                else if (payment.ReservationId.HasValue)
                {
                    var reservation = await _clientAppService.GetReservationByIdAsync(payment.ReservationId.Value, cancellationToken);
                    if (reservation != null && reservation.ClientId == client.Id)
                        isAuthorized = true;
                }
            }
            if (!isAuthorized && !User.IsInRole(Administrator))
            {
                return Forbid();
            }
            if (isAuthorized && (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.AwaitingValidation))
            {
                return RedirectToAction(ActionNames.PaymentSuccess, new { id = payment.Id });
            }
            ViewBag.IsPaymentOwner = isAuthorized;
            var asaasApiKey = await _systemConfig.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
            var isAsaasConfigured = !string.IsNullOrEmpty(asaasApiKey);
            if (!isAsaasConfigured)
            {
                TempData[TempDataKeys.Warning] = WarningMessages.PaymentSystemNotConfiguredConfigureAsaas;
            }
            var hiddenPaymentRaw = await _systemConfig.GetValueAsync(PaymentConfigKey.HiddenPaymentOptions.ToConfigKey());
            var hiddenPaymentSet = (hiddenPaymentRaw ?? "").Split(',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var paymentContract = payment.Subscription?.Contract;
            var snapshotListPrice = ResolveSnapshotListPrice(payment, paymentContract);
            var cardMonthlySurcharge = paymentContract != null
                ? await GetContractSurchargePercentAsync(paymentContract, cancellationToken)
                : null;
            var snapshotPrice1x = _addressPricingService.GetChargeAmount(snapshotListPrice, cardMonthlySurcharge, ContractCardPlanType.Card1x);
            var snapshotPriceMonthly = _addressPricingService.GetChargeAmount(snapshotListPrice, cardMonthlySurcharge, ContractCardPlanType.CardMonthly);
            var supportsCard = _addressPricingService.SupportsCardPayment(snapshotListPrice);
            if (isAuthorized && payment.Status == PaymentStatus.Pending && isAsaasConfigured)
            {
                var showPixInDropdownEarly = !hiddenPaymentSet.Contains("PixAnnual") || !hiddenPaymentSet.Contains("PIX");
                var showBoletoInDropdownEarly = !hiddenPaymentSet.Contains("Boleto");
                var hasCard1xEarly = supportsCard && !hiddenPaymentSet.Contains("Card1x");
                var hasCardMonthlyEarly = supportsCard && !hiddenPaymentSet.Contains("CardMonthly");
                var showCreditCardInDropdownEarly = (hasCard1xEarly || hasCardMonthlyEarly) && (!hiddenPaymentSet.Contains("Card1x") || !hiddenPaymentSet.Contains("CardMonthly"));
                var showPixCardCore = (payment.Method == PaymentMethod.PIX && showPixInDropdownEarly) ||
                    (payment.Method == PaymentMethod.BankTransfer && !showBoletoInDropdownEarly && showPixInDropdownEarly) ||
                    (payment.Method == PaymentMethod.CreditCard && !showCreditCardInDropdownEarly && showPixInDropdownEarly);
                if (showPixCardCore && payment.Method != PaymentMethod.PIX)
                {
                    var defaultPixPlan = !hiddenPaymentSet.Contains("PixAnnual") ? ContractCardPlanType.PixAnnual : ContractCardPlanType.None;
                    try
                    {
                        await _paymentService.UpdatePaymentMethodAsync(payment.Id, PaymentMethod.PIX, defaultPixPlan);
                        payment = await _clientAppService.GetPaymentByIdWithFullDetailsAsync(id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao alterar método para PIX (fallback) no PaymentDetails. PaymentId: {PaymentId}", id);
                    }
                }
                try
                {
                    if (payment.Method == PaymentMethod.PIX)
                    {
                        // Mesma lógica do PIX Automático: garantir 1 código válido (reaproveitar se válido, gerar 1 vez se não houver ou expirado). Único ponto que dispara geração ao exibir a tela; UpdatePaymentMethod só atualiza método/contrato.
                        var contract = payment.Subscription?.Contract;
                        var pixPlanType = contract?.CardPlanType ?? ContractCardPlanType.None;
                        await _paymentService.GeneratePixCodeAsync(payment.Id, pixPlanType);
                        payment = await _clientAppService.GetPaymentByIdWithFullDetailsAsync(id, cancellationToken);
                    }
                    else if (payment.Method == PaymentMethod.BankTransfer)
                    {
                        var boletoExpUtc = payment.BoletoExpiresAt.HasValue
                            ? (payment.BoletoExpiresAt.Value.Kind == DateTimeKind.Utc ? payment.BoletoExpiresAt.Value : DateTime.SpecifyKind(payment.BoletoExpiresAt.Value, DateTimeKind.Utc))
                            : (DateTime?)null;
                        var boletoMissingOrExpired = string.IsNullOrEmpty(payment.BoletoCode) ||
                            (boletoExpUtc.HasValue && boletoExpUtc.Value < DateTime.UtcNow);
                        if (boletoMissingOrExpired)
                        {
                            await _paymentService.GenerateBoletoCodeAsync(payment.Id);
                            payment = await _clientAppService.GetPaymentByIdWithFullDetailsAsync(id, cancellationToken);
                        }
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("API Key", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("não configurada", StringComparison.OrdinalIgnoreCase))
                {
                    TempData[TempDataKeys.Warning] = WarningMessages.PaymentSystemNotConfiguredConfigureAsaasShort;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("não informado") || ex.Message.Contains("inválido") || ex.Message.Contains("Complete seu cadastro"))
                {
                    TempData[ErrorMessages.TempDataKey] = ex.Message;
                    return RedirectToAction(ActionNames.Profile);
                }
                catch (InvalidOperationException ex)
                {
                    TempData[ErrorMessages.TempDataKey] = ex.Message;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao gerar código PIX/Boleto no PaymentDetails (Asaas configurado). PaymentId: {PaymentId}", id);
                    TempData[TempDataKeys.Warning] = WarningMessages.CouldNotGeneratePaymentCodePixOrCheckAsaas;
                }
            }
            ViewBag.HiddenPaymentOptions = hiddenPaymentSet;
            ViewBag.SnapshotListPrice = snapshotListPrice;
            ViewBag.SnapshotPrice1x = snapshotPrice1x;
            ViewBag.SnapshotPriceMonthly = snapshotPriceMonthly;
            ViewBag.SnapshotBoletoPrice = _addressPricingService.GetBoletoPrice(
                GetDiscountedBasePrice(snapshotListPrice, payment.SnapshotDiscountAmount));
            var showPixInDropdownCore = !hiddenPaymentSet.Contains("PixAnnual") || !hiddenPaymentSet.Contains("PIX");
            var showBoletoInDropdownCore = !hiddenPaymentSet.Contains("Boleto");
            var hasCard1x = supportsCard && !hiddenPaymentSet.Contains("Card1x");
            var hasCardMonthly = supportsCard && !hiddenPaymentSet.Contains("CardMonthly");
            var showCreditCardInDropdownCore = (hasCard1x || hasCardMonthly) && (!hiddenPaymentSet.Contains("Card1x") || !hiddenPaymentSet.Contains("CardMonthly"));
            var showCreditCardSectionCore = (payment.Method == PaymentMethod.CreditCard && showCreditCardInDropdownCore) ||
                (payment.Method == PaymentMethod.PIX && !showPixInDropdownCore && showCreditCardInDropdownCore) ||
                (payment.Method == PaymentMethod.BankTransfer && !showBoletoInDropdownCore && showCreditCardInDropdownCore);
            if (showCreditCardSectionCore)
            {
                var ownerClientId = payment.Subscription?.Contract?.ClientId ?? client?.Id;
                if (ownerClientId.HasValue)
                {
                    var creditCards = await _clientAppService.GetCreditCardsByClientIdAsync(ownerClientId.Value, cancellationToken);
                    ViewBag.CreditCards = creditCards;
                }
                var billingClient = client;
                if (billingClient == null && ownerClientId.HasValue)
                    billingClient = await _clientAppService.GetClientByIdAsync(ownerClientId.Value, cancellationToken);
                if (billingClient != null)
                {
                    ViewBag.CardHolderPostalCode = CreditCardHolderBilling.FormatMasked(billingClient.ZipCode) ?? "";
                    var isNoNumber = string.Equals((billingClient.AddressNumber ?? "").Trim(), CreditCardHolderBilling.NoNumberValue, StringComparison.OrdinalIgnoreCase);
                    ViewBag.CardHolderHasNoNumber = isNoNumber;
                    ViewBag.CardHolderAddressNumber = isNoNumber ? "" : (PaymentGatewayCustomer.RealAddressNumber(billingClient.AddressNumber) ?? "");
                }
            }
            string? creditCardPlanLabel = null;
            string? creditCardRecurrence = null;
            if (payment.Method == PaymentMethod.CreditCard && paymentContract != null)
            {
                var amount = payment.Amount;
                if (paymentContract.CardPlanType == ContractCardPlanType.Card1x)
                {
                    creditCardPlanLabel = $"À vista (1x no cartão) — R$ {amount:N2} — recorrência anual cobrada no cartão de crédito";
                    creditCardRecurrence = "annual";
                }
                else if (paymentContract.CardPlanType == ContractCardPlanType.CardMonthly)
                {
                    creditCardPlanLabel = $"Venda mensal — R$ {amount:N2} — recorrência mensal no cartão de crédito";
                    creditCardRecurrence = "monthly";
                }
            }
            ViewBag.CreditCardPlanLabel = creditCardPlanLabel;
            ViewBag.CreditCardRecurrence = creditCardRecurrence;
            decimal originalAmount = payment.Amount;
            decimal finalAmount = payment.Amount;
            decimal? feePercentage = null;
            if (payment.Method == PaymentMethod.CreditCard)
            {
                var feePercentageStr = await _systemConfig.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
                if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee))
                {
                    feePercentage = fee;
                    if (fee > 0)
                    {
                        finalAmount = originalAmount * (1 + fee / 100);
                    }
                }
            }
            ViewBag.OriginalAmount = originalAmount;
            ViewBag.FinalAmount = finalAmount;
            ViewBag.FeePercentage = feePercentage;
            int historyPage = 1;
            int.TryParse(Request.Query["historyPage"], out historyPage);
            if (historyPage < 1) historyPage = 1;
            int historyPageSize = 5;
            var totalHistoryItems = await _clientAppService.GetPaymentHistoryCountAsync(payment.Id, cancellationToken);
            var totalHistoryPages = (int)Math.Ceiling(totalHistoryItems / (double)historyPageSize);
            if (historyPage > totalHistoryPages && totalHistoryPages > 0) historyPage = totalHistoryPages;
            var (paymentHistory, _) = await _clientAppService.GetPaymentHistoriesByPaymentIdAsync(payment.Id, historyPage, historyPageSize, cancellationToken);
            ViewBag.PaymentHistory = paymentHistory;
            ViewBag.HistoryCurrentPage = historyPage;
            ViewBag.HistoryTotalPages = totalHistoryPages;
            ViewBag.HistoryTotalItems = totalHistoryItems;
            var receiptUploadedEntry = await _clientAppService.GetReceiptUploadedEntryAsync(payment.Id, cancellationToken);
            ViewBag.ReceiptUploadedAt = receiptUploadedEntry?.ActionAt;
            if (payment.Status == PaymentStatus.Cancelled && payment.Subscription?.Contract != null && !string.IsNullOrEmpty(payment.Subscription.Contract.CancellationReason))
            {
                ViewBag.ContractCancellationReason = payment.Subscription.Contract.CancellationReason;
            }
            if (payment.Method == PaymentMethod.BankTransfer && !string.IsNullOrEmpty(payment.BoletoCode))
            {
                if (payment.BoletoCode == payment.BoletoBarcode)
                {
                    var (barcode44, digitableFormatted) = BoletoHelper.NormalizeBoletoValues(payment.BoletoCode);
                    if (!string.IsNullOrEmpty(barcode44) && !string.IsNullOrEmpty(digitableFormatted))
                    {
                        payment.BoletoBarcode = barcode44;
                        payment.BoletoCode = digitableFormatted;
                    }
                }
                var boleto47Digits = BoletoHelper.OnlyDigits(payment.BoletoCode);
                if (boleto47Digits.Length == 47)
                {
                    var (validationOk, validationError) = BoletoHelper.ValidateBoletoFromIntegration(boleto47Digits);
                    ViewBag.BoletoValidationValid = validationOk;
                    ViewBag.BoletoValidationMessage = validationOk ? "Validação FEBRABAN: dígitos verificadores conferem." : (validationError ?? "Validação FEBRABAN não realizada.");
                }
            }
            return View(payment);
        }
        [HttpGet]
        public async Task<IActionResult> GetPaymentStatus(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _clientAppService.GetPaymentByIdAsync(paymentId, cancellationToken);
                if (payment == null)
                {
                    return NotFound(new { error = ErrorJson.PaymentNotFoundShort });
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return Unauthorized(new { error = ErrorJson.ClientNotFoundShort });
                }
                bool isAuthorized = false;
                if (payment.SubscriptionId.HasValue)
                {
                    var subscription = await _clientAppService.GetSubscriptionWithContractAsync(payment.SubscriptionId.Value, cancellationToken);
                    if (subscription?.Contract?.ClientId == client.Id)
                    {
                        isAuthorized = true;
                    }
                }
                else if (payment.ReservationId.HasValue)
                {
                    var reservation = await _clientAppService.GetReservationByIdAsync(payment.ReservationId.Value, cancellationToken);
                    if (reservation?.ClientId == client.Id)
                    {
                        isAuthorized = true;
                    }
                }
                if (!isAuthorized && !User.IsInRole(Administrator))
                {
                    return Forbid();
                }
                string? redirectUrl = null;
                if (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.AwaitingValidation)
                {
                    redirectUrl = Url.Action(ActionNames.PaymentSuccess, ControllerNames.Client, new { id = payment.Id });
                    if (payment.Status == PaymentStatus.Paid && payment.SubscriptionId.HasValue)
                    {
                        var subscription = await _clientAppService.GetSubscriptionWithContractAsync(payment.SubscriptionId.Value, cancellationToken);
                        if (subscription?.Contract != null && !subscription.Contract.DocumentsApproved)
                        {
                            var uploadUrl = Url.Action(ActionNames.UploadDocuments, ControllerNames.Client, new { contractId = subscription.Contract.Id });
                            var party = await _clientProfileService.GetContractPartyStatusByUserIdAsync(userId, cancellationToken);
                            redirectUrl = party.IsComplete
                                ? uploadUrl
                                : Url.Action(ActionNames.Profile, ControllerNames.Client, new { returnUrl = uploadUrl });
                        }
                    }
                }
                return Ok(new 
                { 
                    paymentId = payment.Id,
                    status = payment.Status.ToString(),
                    paidAt = payment.PaidAt,
                    updatedAt = payment.UpdatedAt,
                    redirectUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentMethod(int paymentId, string method, [FromForm(Name = "pixPlanType")] string? pixPlanSelection, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }
                var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
                if (contract == null || contract.ClientId != client.Id)
                {
                    return Forbid();
                }
                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyPendingPaymentsCanChangeMethod;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var hiddenUpdateRaw = await _systemConfig.GetValueAsync(PaymentConfigKey.HiddenPaymentOptions.ToConfigKey());
                var hiddenUpdateSet = (hiddenUpdateRaw ?? "").Split(',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                ContractCardPlanType? pixPlanType = null;
                PaymentMethod paymentMethod;
                if (string.Equals(method, "PixAnnual", StringComparison.OrdinalIgnoreCase))
                {
                    if (hiddenUpdateSet.Contains("PixAnnual"))
                    {
                        TempData[ErrorMessages.TempDataKey] = "A forma de pagamento PIX recorrente anual não está disponível.";
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                    }
                    paymentMethod = PaymentMethod.PIX;
                    pixPlanType = ContractCardPlanType.PixAnnual;
                }
                else if (string.Equals(method, "PIX", StringComparison.OrdinalIgnoreCase))
                {
                    paymentMethod = PaymentMethod.PIX;
                    // Forma de cobrança: explícito no form (pixPlanType) ou do contrato; PixAnnual -> /pix/automatic/authorizations, único -> /payments
                    ContractCardPlanType requestedPixPlan;
                    if (string.Equals(pixPlanSelection, "PixAnnual", StringComparison.OrdinalIgnoreCase))
                        requestedPixPlan = ContractCardPlanType.PixAnnual;
                    else if (string.Equals(pixPlanSelection, "PIX", StringComparison.OrdinalIgnoreCase))
                        requestedPixPlan = ContractCardPlanType.None; // PIX (pagamento único) -> POST /payments
                    else
                        requestedPixPlan = contract?.CardPlanType ?? ContractCardPlanType.PixAnnual; // Sem tipo no form: usar contrato ou padrão anual
                    if (requestedPixPlan == ContractCardPlanType.PixAnnual)
                    {
                        if (hiddenUpdateSet.Contains("PixAnnual"))
                        {
                            TempData[ErrorMessages.TempDataKey] = "A forma de pagamento PIX recorrente anual não está disponível.";
                            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                        }
                    }
                    else
                    {
                        if (hiddenUpdateSet.Contains("PIX"))
                        {
                            TempData[ErrorMessages.TempDataKey] = "A forma de pagamento PIX (pagamento único) não está disponível.";
                            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                        }
                    }
                    pixPlanType = requestedPixPlan;
                }
                else if (!Enum.TryParse<PaymentMethod>(method, out paymentMethod))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidPaymentMethod;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                else
                {
                    if (paymentMethod == PaymentMethod.PIX && hiddenUpdateSet.Contains("PIX"))
                    {
                        TempData[ErrorMessages.TempDataKey] = "A forma de pagamento PIX não está disponível.";
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                    }
                    if (paymentMethod == PaymentMethod.CreditCard && hiddenUpdateSet.Contains("Card1x") && hiddenUpdateSet.Contains("CardMonthly"))
                    {
                        TempData[ErrorMessages.TempDataKey] = "A forma de pagamento com cartão não está disponível.";
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                    }
                }
                if (paymentMethod == PaymentMethod.CreditCard && contract != null)
                {
                    var listPrice = contract.SnapshotListPrice > 0 ? contract.SnapshotListPrice : contract.MonthlyValue;
                    if (!_addressPricingService.SupportsCardPayment(listPrice))
                    {
                        var msg = "Pagamento no cartão não disponível para este contrato. Utilize PIX ou boleto.";
                        TempData[ErrorMessages.TempDataKey] = msg;
                        if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                            return StatusCode(400, new { error = msg });
                        return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                    }
                }
                if (paymentMethod == PaymentMethod.BankTransfer && hiddenUpdateSet.Contains("Boleto"))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.BoletoNoLongerAvailable;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                ContractCardPlanType? checkoutCardPlan = paymentMethod switch
                {
                    PaymentMethod.CreditCard => contract.CardPlanType ?? ContractCardPlanType.Card1x,
                    PaymentMethod.PIX => pixPlanType ?? contract.CardPlanType,
                    _ => contract.CardPlanType
                };
                var trackedPayment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
                var trackedContract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contract.Id, cancellationToken);
                if (trackedPayment != null && trackedContract != null)
                    await SyncCheckoutAmountFromSnapshotAsync(trackedPayment, trackedContract, paymentMethod, checkoutCardPlan, cancellationToken);

                await _paymentService.UpdatePaymentMethodAsync(paymentId, paymentMethod, pixPlanType);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentMethodUpdated;
                if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                    return Ok();
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("atualizar método de pagamento", ex.Message);
                if (Request.Headers[HttpHeaderNames.XRequestedWith] == HttpHeaderNames.XRequestedWithValue)
                    return StatusCode(400, new { error = ex.Message });
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePixCode(int paymentId, [FromForm(Name = "pixPlanType")] string? pixPlanSelection, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }
                var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
                if (contract == null || contract.ClientId != client.Id)
                {
                    return Forbid();
                }
                if (payment.Method != PaymentMethod.PIX)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ThisPaymentIsNotPix;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                if (payment.Status == PaymentStatus.Cancelled)
                {
                    await _paymentService.ReopenCancelledPixPaymentAsync(paymentId);
                }
                else if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyPendingPaymentsCanGeneratePix;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                // Forma de cobrança escolhida pelo usuário na tela (PixAnnual ou PIX = único); se não enviada, usa a do contrato
                ContractCardPlanType? requestedPixPlan = null;
                if (string.Equals(pixPlanSelection, "PixAnnual", StringComparison.OrdinalIgnoreCase))
                    requestedPixPlan = ContractCardPlanType.PixAnnual;
                else if (string.Equals(pixPlanSelection, "PIX", StringComparison.OrdinalIgnoreCase))
                    requestedPixPlan = ContractCardPlanType.None;
                if (!requestedPixPlan.HasValue)
                    requestedPixPlan = payment.Subscription?.Contract?.CardPlanType;
                await _paymentService.GeneratePixCodeAsync(paymentId, requestedPixPlan);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PixCodeGenerated;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("API Key", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("não configurada", StringComparison.OrdinalIgnoreCase))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentSystemNotConfigured;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (InvalidOperationException ex)
            {
                TempData[ErrorMessages.TempDataKey] = ex.Message;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("gerar código PIX", ex.Message);
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int paymentId, IFormFile? receiptFile, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }
                var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
                if (contract == null || contract.ClientId != client.Id)
                {
                    return Forbid();
                }
                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentAlreadyProcessed;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                if (receiptFile == null || receiptFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SendPaymentReceipt;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var fileExtension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidFileFormatImagesOrPdf;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
                var storageService = await storageFactory.GetStorageServiceAsync();
                using var stream = new MemoryStream();
                await receiptFile.CopyToAsync(stream);
                var fileBytes = stream.ToArray();
                var fileName = $"receipt_{paymentId}_{DateTimeHelper.GetBrazilianTime():yyyyMMddHHmmss}{fileExtension}";
                var receiptPath = await storageService.SaveFileAsync(fileBytes, fileName, $"payments/{paymentId}/receipts");
                payment.Status = PaymentStatus.AwaitingValidation;
                payment.ReceiptFilePath = receiptPath;
                payment.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                var history = new PaymentHistory
                {
                    PaymentId = paymentId,
                    Action = PaymentHistoryActions.ReceiptUploaded,
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                };
                await _clientAppService.AddPaymentHistoryAsync(history, cancellationToken);
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("confirmar pagamento", ex.Message);
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
        }
        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var contract = await _clientAppService.GetContractByIdAsync(payment.Subscription.ContractId, cancellationToken);
            if (contract == null || contract.ClientId != client.Id)
            {
                if (!User.IsInRole(Administrator) && !User.IsInRole(RoleNames.CoworkingPartner))
                {
                    return Forbid();
                }
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
            return File(fileBytes, contentType, fileName);
        }
        [HttpGet]
        public async Task<IActionResult> ViewReceipt(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            bool isAuthorized = false;
            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _clientAppService.GetSubscriptionWithContractAsync(payment.SubscriptionId.Value, cancellationToken);
                if (subscription?.Contract != null && subscription.Contract.ClientId == client.Id) isAuthorized = true;
            }
            else if (payment.ReservationId.HasValue)
            {
                var reservation = await _clientAppService.GetReservationByIdAsync(payment.ReservationId.Value, cancellationToken);
                if (reservation != null && reservation.ClientId == client.Id) isAuthorized = true;
            }
            if (!isAuthorized && !User.IsInRole(Administrator) && !User.IsInRole(RoleNames.CoworkingPartner))
            {
                return Forbid();
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
                Response.Headers.Append(HttpHeaderNames.CacheControl, "no-cache, no-store, must-revalidate");
                Response.Headers.Append(HttpHeaderNames.Pragma, "no-cache");
                Response.Headers.Append(HttpHeaderNames.Expires, "0");
                return File(fileBytes, contentType);
            }
            Response.Headers.Append(HttpHeaderNames.ContentDisposition, $"inline; filename=\"{fileName}\"");
            return File(fileBytes, contentType);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCreditCard(int paymentId, string cardNumber, string cardHolderName, 
            string expiryMonth, string expiryYear, string cvv, string? isDefault, string? nickname,
            string? postalCode, string? addressNumber, string? addressHasNoNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var cleanCardNumber = cardNumber.Replace(" ", "").Replace("-", "");
                if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidCardNumber;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var lastFourDigits = cleanCardNumber.Substring(cleanCardNumber.Length - 4);
                var brand = "Cartão";
                var firstDigit = cleanCardNumber[0];
                if (firstDigit == '4')
                    brand = "Visa";
                else if (firstDigit == '5' || firstDigit == '2')
                    brand = "Mastercard";
                else if (cleanCardNumber.StartsWith("34") || cleanCardNumber.StartsWith("37"))
                    brand = "American Express";
                else if (cleanCardNumber.StartsWith("60") || cleanCardNumber.StartsWith("65"))
                    brand = "Discover";
                var setAsDefault = StringBooleanValue.IsTrue(isDefault);
                var existingCardsCount = await _clientAppService.CountActiveCreditCardsAsync(client.Id, cancellationToken);
                if (existingCardsCount == 0)
                    setAsDefault = true;
                if (setAsDefault)
                {
                    await _clientAppService.ClearDefaultCreditCardsAsync(client.Id, cancellationToken);
                }
                var creditCard = new CreditCard
                {
                    ClientId = client.Id,
                    LastFourDigits = lastFourDigits,
                    CardHolderName = cardHolderName.ToUpper(),
                    ExpiryMonth = expiryMonth,
                    ExpiryYear = expiryYear,
                    Brand = brand,
                    Nickname = string.IsNullOrWhiteSpace(nickname) ? null : (nickname.Trim().Length > 100 ? nickname.Trim().Substring(0, 100) : nickname.Trim()),
                    IsDefault = setAsDefault,
                    IsActive = true
                };
                try
                {
                    var remoteIp = HttpContext.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    var tokenize = await _paymentService.TokenizeCreditCardForClientAsync(
                        client.Id,
                        cleanCardNumber,
                        expiryMonth,
                        expiryYear,
                        cvv,
                        cardHolderName.Trim(),
                        postalCode,
                        addressNumber,
                        StringBooleanValue.IsTrue(addressHasNoNumber),
                        remoteIp,
                        cancellationToken);
                    creditCard.AsaasToken = tokenize.Token;
                    creditCard.PostalCode = tokenize.PostalCode;
                    creditCard.AddressNumber = tokenize.AddressNumber;
                }
                catch (Exception exToken)
                {
                    _logger.LogWarning(exToken, "Tokenização Asaas ao adicionar cartão falhou. ClientId={ClientId}", client.Id);
                    TempData[ErrorMessages.TempDataKey] = exToken.Message;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                await _clientAppService.AddCreditCardAsync(creditCard, cancellationToken);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardAdded;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("adicionar cartão", ex.Message);
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCreditCard(int paymentId, int cardId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            var card = await _clientAppService.GetCreditCardByIdAndClientIdAsync(cardId, client.Id, cancellationToken);
            if (card == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CreditCardNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            await _clientAppService.RemoveCreditCardAsync(cardId, client.Id, cancellationToken);
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardRemoved;
            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultCreditCard(int paymentId, int cardId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            var card = await _clientAppService.GetCreditCardByIdAndClientIdAsync(cardId, client.Id, cancellationToken);
            if (card == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CreditCardNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            await _clientAppService.SetDefaultCreditCardAsync(client.Id, cardId, cancellationToken);
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardSetAsDefault;
            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
        }
        [HttpPost]
        public async Task<IActionResult> ProcessPaymentWithCard([FromBody] ProcessPaymentWithCardRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, error = ErrorJson.CardDataRequired });
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return Json(new { success = false, error = ErrorJson.ClientNotFound });
                }
                var payment = await _clientAppService.GetPaymentWithSubscriptionAndContractAsync(request.PaymentId, cancellationToken);
                if (payment == null)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNotFound });
                }
                if (payment.Status != PaymentStatus.Pending)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNoLongerPending });
                }
                if (payment.Method != PaymentMethod.CreditCard)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNotCreditCard });
                }
                if (!payment.SubscriptionId.HasValue || payment.Subscription == null)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNoSubscription });
                }
                var contract = payment.Subscription.Contract;
                if (contract == null || contract.ClientId != client.Id)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNotBelongToProfile });
                }
                var cardPlanOption = (request.CardPlanOption ?? "").Trim().ToLowerInvariant();
                var newCardPlanType = cardPlanOption == "monthly"
                    ? ContractCardPlanType.CardMonthly
                    : ContractCardPlanType.Card1x;
                var snapshotListPrice = ResolveSnapshotListPrice(payment, contract);
                var surcharge = await GetContractSurchargePercentAsync(contract, cancellationToken);
                var newAmount = CalculateCheckoutAmount(
                    snapshotListPrice,
                    payment.SnapshotDiscountAmount,
                    surcharge,
                    PaymentMethod.CreditCard,
                    newCardPlanType);
                if (contract.CardPlanType != newCardPlanType || payment.Amount != newAmount)
                {
                    contract.CardPlanType = newCardPlanType;
                    payment.Amount = newAmount;
                    contract.MonthlyValue = newAmount;
                    contract.SnapshotMonthlyValue = newAmount;
                    if (payment.Subscription != null)
                        payment.Subscription.MonthlyAmount = newAmount;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                const int installmentsForGateway = 1;
                var remoteIpForPayment = HttpContext.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var billingPostalCode = request.PostalCode;
                var billingAddressNumber = request.AddressNumber;
                var billingHasNoNumber = request.AddressHasNoNumber;
                if (request.CreditCardId.HasValue && request.CreditCardId.Value > 0)
                {
                    var savedCard = await _clientAppService.GetCreditCardByIdAndClientIdAsync(request.CreditCardId.Value, client.Id, cancellationToken);
                    if (savedCard != null && !string.IsNullOrEmpty(savedCard.AsaasToken))
                    {
                        try
                        {
                            var savedCardResult = await _paymentService.ProcessPaymentWithSavedCardAsync(payment.Id, savedCard.Id, installmentsForGateway, remoteIpForPayment, billingPostalCode, billingAddressNumber, billingHasNoNumber);
                            if (savedCardResult.Status == PaymentStatus.Paid)
                                return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoAprovadoContratoAtivado));
                            if (savedCardResult.Status == PaymentStatus.Failed)
                                return Json(new { success = false, error = ErrorJson.PaymentDeclinedTryAnotherCard });
                            return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoSendoProcessadoAtualizePagina));
                        }
                        catch (Exception ex) when (ex.Message.Contains("CreditCardToken", StringComparison.OrdinalIgnoreCase) && (ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase)))
                        {
                            var creditCardEntity = await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == request.CreditCardId.Value && c.ClientId == client.Id, cancellationToken);
                            if (creditCardEntity != null)
                            {
                                creditCardEntity.AsaasToken = null;
                                await _context.SaveChangesAsync(cancellationToken);
                                _logger.LogWarning("ProcessPaymentWithCard: token do cartão Id={CreditCardId} invalidado pelo Asaas. Token removido para revalidação.", request.CreditCardId.Value);
                            }
                            return Json(new { success = false, error = ErrorJson.CardTokenMissingUseForm });
                        }
                    }
                    if (savedCard != null && string.IsNullOrEmpty(savedCard.AsaasToken))
                    {
                        if (request.Card == null)
                        {
                            _logger.LogWarning("ProcessPaymentWithCard: cartão Id={CreditCardId} do cliente Id={ClientId} não possui AsaasToken. Solicitar dados no formulário.", request.CreditCardId.Value, client.Id);
                            return Json(new { success = false, error = ErrorJson.CardTokenMissingUseForm });
                        }
                        var numberReval = (request.Card.Number ?? "").Replace(" ", "").Replace("-", "").Trim();
                        var monthReval = (request.Card.ExpirationMonth ?? "").Trim();
                        var yearReval = (request.Card.ExpirationYear ?? "").Trim();
                        if (yearReval.Length == 2) yearReval = "20" + yearReval;
                        var cvvReval = (request.Card.SecurityCode ?? "").Trim();
                        var holderReval = (request.Card.HolderName ?? "").Trim();
                        if (numberReval.Length < 13 || numberReval.Length > 19)
                        {
                            return Json(new { success = false, error = ErrorJson.CardNumberInvalid });
                        }
                        if (string.IsNullOrEmpty(monthReval) || string.IsNullOrEmpty(yearReval) || monthReval.Length > 2 || yearReval.Length != 4)
                        {
                            return Json(new { success = false, error = ErrorJson.ExpirationDateInvalid });
                        }
                        if (string.IsNullOrEmpty(cvvReval) || cvvReval.Length < 3)
                        {
                            return Json(new { success = false, error = ErrorJson.SecurityCodeInvalid });
                        }
                        if (string.IsNullOrEmpty(holderReval) || holderReval.Length < 2 || !holderReval.Any(char.IsLetter))
                        {
                            return Json(new { success = false, error = ErrorJson.CardHolderNameInvalid });
                        }
                        try
                        {
                            var tokenizeReval = await _paymentService.TokenizeCreditCardForClientAsync(
                                client.Id,
                                numberReval,
                                monthReval,
                                yearReval,
                                cvvReval,
                                holderReval,
                                billingPostalCode,
                                billingAddressNumber,
                                billingHasNoNumber,
                                remoteIpForPayment,
                                cancellationToken);
                            await _clientAppService.SetCreditCardAsaasTokenAsync(
                                request.CreditCardId.Value,
                                client.Id,
                                tokenizeReval.Token,
                                tokenizeReval.PostalCode,
                                tokenizeReval.AddressNumber,
                                cancellationToken);
                            var savedCardResult = await _paymentService.ProcessPaymentWithSavedCardAsync(payment.Id, savedCard.Id, installmentsForGateway, remoteIpForPayment, billingPostalCode, billingAddressNumber, billingHasNoNumber);
                            if (savedCardResult.Status == PaymentStatus.Paid)
                                return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoAprovadoContratoAtivado));
                            if (savedCardResult.Status == PaymentStatus.Failed)
                                return Json(new { success = false, error = ErrorJson.PaymentDeclinedTryAnotherCard });
                            return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoSendoProcessadoAtualizePagina));
                        }
                        catch (Exception exToken)
                        {
                            _logger.LogWarning(exToken, "ProcessPaymentWithCard: revalidação de token para cartão Id={CreditCardId} falhou.", request.CreditCardId.Value);
                            var revalError = !string.IsNullOrWhiteSpace(exToken.Message) ? exToken.Message : ErrorJson.CardTokenMissingUseForm;
                            return Json(new { success = false, error = revalError });
                        }
                    }
                }
                if (request.Card == null)
                {
                    return Json(new { success = false, error = ErrorJson.CardDataRequired });
                }
                var number = (request.Card.Number ?? "").Replace(" ", "").Replace("-", "").Trim();
                if (number.Length < 13 || number.Length > 19)
                {
                    return Json(new { success = false, error = ErrorJson.CardNumberInvalid });
                }
                var month = (request.Card.ExpirationMonth ?? "").Trim();
                var year = (request.Card.ExpirationYear ?? "").Trim();
                if (year.Length == 2) year = "20" + year;
                if (string.IsNullOrEmpty(month) || string.IsNullOrEmpty(year) || month.Length > 2 || year.Length != 4)
                {
                    return Json(new { success = false, error = ErrorJson.ExpirationDateInvalid });
                }
                var expDate = $"{year}-{month.PadLeft(2, '0')}";
                var cvv = (request.Card.SecurityCode ?? "").Trim();
                if (string.IsNullOrEmpty(cvv) || cvv.Length < 3)
                {
                    return Json(new { success = false, error = ErrorJson.SecurityCodeInvalid });
                }
                var holderName = (request.Card.HolderName ?? "").Trim();
                if (string.IsNullOrEmpty(holderName))
                {
                    return Json(new { success = false, error = ErrorJson.CardHolderNameRequired });
                }
                if (holderName.Length < 2)
                {
                    return Json(new { success = false, error = ErrorJson.CardHolderNameInvalid });
                }
                if (!holderName.Any(char.IsLetter))
                {
                    return Json(new { success = false, error = ErrorJson.CardHolderNameInvalid });
                }

                if (request.SaveCard)
                {
                    var lastFourDigits = number.Substring(number.Length - 4);
                    var brand = "Cartão";
                    var firstDigit = number[0];
                    if (firstDigit == '4') brand = "Visa";
                    else if (firstDigit == '5' || firstDigit == '2') brand = "Mastercard";
                    else if (number.StartsWith("34") || number.StartsWith("37")) brand = "American Express";
                    else if (number.StartsWith("60") || number.StartsWith("65")) brand = "Discover";
                    var setAsDefault = request.SetAsDefault;
                    var existingCardsCount = await _clientAppService.CountActiveCreditCardsAsync(client.Id, cancellationToken);
                    if (existingCardsCount == 0)
                        setAsDefault = true;
                    if (setAsDefault)
                        await _clientAppService.ClearDefaultCreditCardsAsync(client.Id, cancellationToken);
                    var creditCard = new CreditCard
                    {
                        ClientId = client.Id,
                        LastFourDigits = lastFourDigits,
                        CardHolderName = holderName.ToUpper(),
                        ExpiryMonth = month,
                        ExpiryYear = year,
                        Brand = brand,
                        Nickname = string.IsNullOrWhiteSpace(request.Card.Nickname) ? null : (request.Card.Nickname.Trim().Length > 100 ? request.Card.Nickname.Trim().Substring(0, 100) : request.Card.Nickname.Trim()),
                        IsDefault = setAsDefault,
                        IsActive = true
                    };
                    try
                    {
                        var tokenize = await _paymentService.TokenizeCreditCardForClientAsync(
                            client.Id,
                            number,
                            month,
                            year,
                            cvv,
                            holderName,
                            billingPostalCode,
                            billingAddressNumber,
                            billingHasNoNumber,
                            remoteIpForPayment,
                            cancellationToken);
                        creditCard.AsaasToken = tokenize.Token;
                        creditCard.PostalCode = tokenize.PostalCode;
                        creditCard.AddressNumber = tokenize.AddressNumber;
                    }
                    catch (Exception exToken)
                    {
                        _logger.LogWarning(exToken, "Tokenização Asaas ao salvar cartão no pagamento falhou. ClientId={ClientId}", client.Id);
                        var tokenError = !string.IsNullOrWhiteSpace(exToken.Message) ? exToken.Message : ErrorJson.CardTokenMissingUseForm;
                        return Json(new { success = false, error = tokenError });
                    }
                    await _clientAppService.AddCreditCardAsync(creditCard, cancellationToken);
                    var savedResult = await _paymentService.ProcessPaymentWithSavedCardAsync(payment.Id, creditCard.Id, installmentsForGateway, remoteIpForPayment, billingPostalCode, billingAddressNumber, billingHasNoNumber);
                    if (savedResult.Status == PaymentStatus.Paid)
                        return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoAprovadoContratoAtivado));
                    if (savedResult.Status == PaymentStatus.Failed)
                        return Json(new { success = false, error = ErrorJson.PaymentDeclinedTryAnotherCard });
                    return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoSendoProcessadoAtualizePagina));
                }

                var card = new Application.DTOs.PagBank.PagBankCard
                {
                    Number = number,
                    ExpirationDate = expDate,
                    SecurityCode = cvv,
                    Holder = new Application.DTOs.PagBank.PagBankCardHolder { Name = holderName.ToUpper() },
                    Store = false
                };
                var result = await _paymentService.ProcessPaymentWithCreditCardAsync(payment.Id, card, installmentsForGateway, remoteIpForPayment, billingPostalCode, billingAddressNumber, billingHasNoNumber);
                if (result.Status == PaymentStatus.Paid)
                    return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoAprovadoContratoAtivado));
                if (result.Status == PaymentStatus.Failed)
                    return Json(new { success = false, error = ErrorJson.PaymentDeclinedTryAnotherCard });
                return Json(CardPaymentSuccessJson(payment, JsonMessage.PagamentoSendoProcessadoAtualizePagina));
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pagamento com cartão. PaymentId: {PaymentId}", request?.PaymentId);
                var userMessage = ex is InvalidOperationException || (ex.Message?.Contains("recusad", StringComparison.OrdinalIgnoreCase) == true) || (ex.Message?.Contains("cartão", StringComparison.OrdinalIgnoreCase) == true) || (ex.Message?.Contains("token", StringComparison.OrdinalIgnoreCase) == true)
                    ? ex.Message
                    : ErrorJson.ErrorProcessingPayment;
                return Json(new { success = false, error = userMessage });
            }
        }
        [HttpGet]
        public async Task<IActionResult> Declarations(
            string? search = null,
            int? contractId = null,
            string? status = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1,
            int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFoundCompleteRegistration;
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var allContracts = await _clientAppService.GetActiveContractsWithAddressAndLocationAsync(client.Id, cancellationToken);
            var contractIds = allContracts.Select(c => c.Id).ToList();
            var allDeclarations = await _clientAppService.GetAddressDeclarationsByContractIdsWithContractAsync(contractIds, cancellationToken);
            var contractsDict = allContracts.ToDictionary(c => c.Id, c => c);
            var contractsWithPaidPayments = await _clientAppService.GetContractIdsWithPaidPaymentsAsync(contractIds, cancellationToken);
            ViewBag.ContractsWithPaidPayments = contractsWithPaidPayments;
            var contractsCanGenerateDeclaration = new List<int>();
            foreach (var cId in contractIds)
            {
                if (await _addressDeclarationService.CanGenerateDeclarationAsync(cId, cancellationToken))
                    contractsCanGenerateDeclaration.Add(cId);
            }
            ViewBag.ContractsCanGenerateDeclaration = contractsCanGenerateDeclaration;
            var filteredDeclarations = allDeclarations.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                filteredDeclarations = filteredDeclarations.Where(d => 
                    d.DeclarationNumber.ToLower().Contains(searchLower) ||
                    (contractsDict.ContainsKey(d.ContractId) && contractsDict[d.ContractId].ContractNumber.ToLower().Contains(searchLower)));
            }
            if (contractId.HasValue && contractId.Value > 0)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.ContractId == contractId.Value);
            }
            if (!string.IsNullOrEmpty(status))
            {
                var now = DateTimeHelper.GetBrazilianTime();
                switch (status.ToLower())
                {
                    case "valid":
                        filteredDeclarations = filteredDeclarations.Where(d => d.IsValid && d.EndDate >= now);
                        break;
                    case "expired":
                        filteredDeclarations = filteredDeclarations.Where(d => d.EndDate < now);
                        break;
                    case "invalid":
                        filteredDeclarations = filteredDeclarations.Where(d => !d.IsValid);
                        break;
                }
            }
            if (dateFrom.HasValue)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.CreatedAt >= dateFrom.Value);
            }
            if (dateTo.HasValue)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.CreatedAt <= dateTo.Value.AddDays(1).AddSeconds(-1));
            }
            var declarationsList = filteredDeclarations.ToList();
            var declarationsByContract = new Dictionary<int, List<AddressDeclaration>>();
            foreach (var contract in allContracts)
            {
                var contractDeclarations = declarationsList.Where(d => d.ContractId == contract.Id).ToList();
                if (contractDeclarations.Any() || !contractId.HasValue)
                {
                    declarationsByContract[contract.Id] = contractDeclarations;
                }
            }
            var contractsToDisplay = allContracts;
            if (contractId.HasValue)
            {
                contractsToDisplay = allContracts.Where(c => c.Id == contractId.Value).ToList();
            }
            var totalContracts = contractsToDisplay.Count;
            var totalPages = totalContracts > 0 ? (int)Math.Ceiling(totalContracts / (double)pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));
            var pagedContracts = contractsToDisplay
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.Contracts = pagedContracts;
            ViewBag.AllContracts = allContracts;
            ViewBag.DeclarationsByContract = declarationsByContract;
            ViewBag.Search = search;
            ViewBag.ContractId = contractId;
            ViewBag.Status = status;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalContracts;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMultipleContracts = allContracts.Count > 1;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateContract(int contractId, bool returnToContract = true, CancellationToken cancellationToken = default)
        {
            IActionResult RedirectAfterGenerate()
            {
                if (returnToContract)
                    return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
                return RedirectToAction(ActionNames.MyContracts);
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectAfterGenerate();
                }
                var contract = await _clientAppService.GetContractByIdAndClientIdAsync(contractId, client.Id, cancellationToken);
                if (contract == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ContractNotFoundOrNotYours;
                    return RedirectAfterGenerate();
                }
                if (!await _documentService.CanGenerateContractDocumentAsync(contractId))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ContractGenerationNotAllowed;
                    return RedirectAfterGenerate();
                }
                var party = await _clientProfileService.GetContractPartyStatusByContractIdAsync(contractId, cancellationToken);
                if (!party.IsComplete)
                {
                    TempData[ErrorMessages.TempDataKey] = party.ErrorMessage ?? ClientContractPartyRules.IncompleteMessage;
                    return RedirectToAction(ActionNames.Profile, new { returnUrl = Url.Action(ActionNames.ContractDetails, new { id = contractId }) });
                }
                await _documentService.GenerateContractDocumentAsync(contractId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ContractGenerated(contract.ContractNumber);
                return RedirectAfterGenerate();
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("gerar contrato", ex.Message);
                return RedirectAfterGenerate();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDeclaration(int contractId, bool returnToContract = false, CancellationToken cancellationToken = default)
        {
            IActionResult RedirectAfterGenerate()
            {
                if (returnToContract)
                    return RedirectToAction(ActionNames.ContractDetails, new { id = contractId });
                return RedirectToAction(ActionNames.Declarations);
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectAfterGenerate();
                }
                var contract = await _clientAppService.GetContractByIdAndClientIdAsync(contractId, client.Id, cancellationToken);
                if (contract == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ContractNotFoundOrNotYours;
                    return RedirectAfterGenerate();
                }
                if (!await _addressDeclarationService.CanGenerateDeclarationAsync(contractId, cancellationToken))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.DeclarationGenerationNotAllowed;
                    return RedirectAfterGenerate();
                }
                var party = await _clientProfileService.GetContractPartyStatusByContractIdAsync(contractId, cancellationToken);
                if (!party.IsComplete)
                {
                    TempData[ErrorMessages.TempDataKey] = party.ErrorMessage ?? ClientContractPartyRules.IncompleteMessage;
                    return RedirectToAction(ActionNames.Profile, new { returnUrl = Url.Action(ActionNames.ContractDetails, new { id = contractId }) });
                }
                var declaration = await _addressDeclarationService.GenerateDeclarationAsync(contractId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.DeclarationGenerated(declaration.DeclarationNumber);
                return RedirectAfterGenerate();
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("gerar declaração", ex.Message);
                return RedirectAfterGenerate();
            }
        }
        [HttpGet]
        public async Task<IActionResult> DownloadDeclaration(int declarationId, bool download = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    return Forbid();
                }
                var declaration = await _clientAppService.GetAddressDeclarationByIdAsync(declarationId, cancellationToken);
                if (declaration == null || declaration.Contract.ClientId != client.Id)
                {
                    return Forbid();
                }
                if (!declaration.IsValid)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.DeclarationInvalidOrExpired;
                    return RedirectToAction(ActionNames.Declarations);
                }
                var pdfBytes = await _addressDeclarationService.GetDeclarationPdfAsync(declarationId);
                if (pdfBytes == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorDownloadingDeclarationFileNotFound;
                    return RedirectToAction(ActionNames.Declarations);
                }
                var fileName = $"{AddressDeclarationLabels.DownloadFilePrefix(declaration.Contract?.Service?.Code)}_{declaration.DeclarationNumber}.pdf";
                return FileResultWithDisposition(pdfBytes, "application/pdf", fileName, download);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("baixar declaração", ex.Message);
                return RedirectToAction(ActionNames.Declarations);
            }
        }
        [HttpGet]
        public Task<IActionResult> BuyCredits()
        {
            TempData[TempDataKeys.Info] = InfoMessages.SystemNoLongerUsesCredits;
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.DashboardDetails));
        }
        [HttpGet]
        public Task<IActionResult> CheckoutCredits(int creditPlanId)
        {
            TempData[TempDataKeys.Info] = InfoMessages.SystemNoLongerUsesCredits;
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.DashboardDetails));
        }
        [HttpPost]
        public Task<IActionResult> GenerateCreditPurchasePix([FromBody] CreditPlanIdRequest req)
        {
            return Task.FromResult<IActionResult>(Json(new { success = false, error = ErrorJson.CreditsNoLongerUsed }));
        }
        [HttpPost]
        public Task<IActionResult> GenerateCreditPurchaseBoleto([FromBody] CreditPlanIdRequest req)
        {
            return Task.FromResult<IActionResult>(Json(new { success = false, error = ErrorJson.CreditsNoLongerUsed }));
        }
        [HttpPost]
        public async Task<IActionResult> CreateCreditCardPurchase([FromBody] CreditPlanIdRequest req, CancellationToken cancellationToken = default)
        {
            try
            {
                if (req == null || req.CreditPlanId <= 0)
                    return Json(new { success = false, error = ErrorJson.InvalidPlan });
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                    return Json(new { success = false, error = ErrorJson.ClientNotFound });
                var plan = await _context.CreditPlans.FirstOrDefaultAsync(p => p.Id == req.CreditPlanId && p.IsActive);
                if (plan == null)
                    return Json(new { success = false, error = ErrorJson.PlanNotFoundOrUnavailable });
                var purchase = new CreditPurchase
                {
                    ClientId = client.Id,
                    CreditPlanId = plan.Id,
                    AmountPaid = plan.Price,
                    CreditsReceived = plan.Credits,
                    BonusCredits = plan.BonusCredits,
                    Status = CreditPurchaseStatus.Processing,
                    PaymentMethod = PaymentMethod.CreditCard,
                    ExternalPaymentId = Guid.NewGuid().ToString()
                };
                _context.CreditPurchases.Add(purchase);
                await _context.SaveChangesAsync();
                return Json(new { success = true, purchaseId = purchase.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurchaseCredits(int creditPlanId, string paymentMethod, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.BuyCredits);
                }
                var plan = await _context.CreditPlans.FirstOrDefaultAsync(p => p.Id == creditPlanId && p.IsActive);
                if (plan == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PlanNotFoundOrUnavailable;
                    return RedirectToAction(ActionNames.BuyCredits);
                }
                if (string.IsNullOrEmpty(paymentMethod))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SelectPaymentMethod;
                    return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
                }
                PaymentMethod method;
                if (!Enum.TryParse<PaymentMethod>(paymentMethod, out method))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidPaymentMethod;
                    return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
                }
                if (method == PaymentMethod.BankTransfer)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.BoletoNoLongerAvailableChoosePixOrCard;
                    return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
                }
                var purchase = new CreditPurchase
                {
                    ClientId = client.Id,
                    CreditPlanId = plan.Id,
                    AmountPaid = plan.Price,
                    CreditsReceived = plan.Credits,
                    BonusCredits = plan.BonusCredits,
                    Status = CreditPurchaseStatus.Pending,
                    PaymentMethod = method,
                    ExternalPaymentId = Guid.NewGuid().ToString()
                };
                _context.CreditPurchases.Add(purchase);
                await _context.SaveChangesAsync();
                var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
                var clientEmail = client.ContactEmail ?? client.User.Email;
                var clientTaxId = client.CNPJ ?? client.CPF ?? "";
                var clientPhone = client.ContactPhone ?? client.User.Phone;
                var clientAddress = new Application.DTOs.PagBank.PagBankAddress
                {
                    Street = client.Address ?? "Não informado",
                    Number = "S/N",
                    Locality = "Não informado",
                    City = client.City ?? "Não informado",
                    RegionCode = client.State ?? "SP",
                    Country = "BRA",
                    PostalCode = client.ZipCode?.Replace("-", "").Replace(".", "") ?? "00000000"
                };
                if (method == PaymentMethod.PIX)
                {
                    var orderResponse = await _asaasService.CreateOrderAsync(
                        referenceId: purchase.ExternalPaymentId,
                        amount: plan.Price,
                        method: PaymentMethod.PIX,
                        clientName: clientName,
                        clientEmail: clientEmail,
                        clientTaxId: clientTaxId,
                        clientPhone: clientPhone,
                        clientAddress: clientAddress,
                        customerExternalReference: "client_" + client.Id
                    );
                    purchase.ExternalPaymentId = orderResponse.Id;
                    if (orderResponse.QrCodes != null && orderResponse.QrCodes.Any())
                    {
                        var qrCode = orderResponse.QrCodes.First();
                        purchase.PixCode = qrCode.Text;
                        purchase.PixExpiresAt = qrCode.ExpirationDate;
                    }
                    purchase.Status = CreditPurchaseStatus.Processing;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(ActionNames.CreditPurchasePayment, new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.BankTransfer)
                {
                    var orderResponse = await _asaasService.CreateOrderAsync(
                        referenceId: purchase.ExternalPaymentId,
                        amount: plan.Price,
                        method: PaymentMethod.BankTransfer,
                        clientName: clientName,
                        clientEmail: clientEmail,
                        clientTaxId: clientTaxId,
                        clientPhone: clientPhone,
                        clientAddress: clientAddress,
                        customerExternalReference: "client_" + client.Id
                    );
                    purchase.ExternalPaymentId = orderResponse.Id;
                    if (orderResponse.Charges != null && orderResponse.Charges.Any())
                    {
                        var charge = orderResponse.Charges.First();
                        if (charge.PaymentMethod?.Boleto != null)
                        {
                            purchase.BoletoBarcode = charge.PaymentMethod.Boleto.Barcode;
                            purchase.BoletoCode = charge.PaymentMethod.Boleto.FormattedBarcode;
                            purchase.BoletoExpiresAt = charge.PaymentMethod.Boleto.DueDate;
                        }
                    }
                    purchase.Status = CreditPurchaseStatus.Processing;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(ActionNames.CreditPurchasePayment, new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.CreditCard)
                {
                    purchase.Status = CreditPurchaseStatus.Processing;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(ActionNames.CreditPurchasePayment, new { purchaseId = purchase.Id });
                }
                else
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentMethodNotSupported;
                    return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("API Key", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("não configurada", StringComparison.OrdinalIgnoreCase))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentSystemNotConfigured;
                return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("processar pagamento", ex.Message);
                return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
            }
        }
        [HttpGet]
        public async Task<IActionResult> CreditPurchasePayment(int purchaseId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.BuyCredits);
            }
            var purchase = await _context.CreditPurchases
                .Include(p => p.CreditPlan)
                .FirstOrDefaultAsync(p => p.Id == purchaseId && p.ClientId == client.Id);
            if (purchase == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PurchaseNotFound;
                return RedirectToAction(ActionNames.BuyCredits);
            }
            ViewBag.Purchase = purchase;
            ViewBag.Client = client;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ProcessCreditCardPayment([FromBody] ProcessCreditCardRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, error = ErrorJson.InvalidData });
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return Json(new { success = false, error = ErrorJson.ClientNotFound });
                }
                var purchase = await _context.CreditPurchases
                    .Include(p => p.CreditPlan)
                    .FirstOrDefaultAsync(p => p.Id == request.PurchaseId && p.ClientId == client.Id);
                if (purchase == null)
                {
                    return Json(new { success = false, error = ErrorJson.PurchaseNotFound });
                }
                if (purchase.PaymentMethod != PaymentMethod.CreditCard)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentNotCreditCard });
                }
                var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
                var clientEmail = client.ContactEmail ?? client.User.Email;
                var clientTaxId = client.CNPJ ?? client.CPF ?? "";
                var clientPhone = client.ContactPhone ?? client.User.Phone;
                var billingPostal = request.BillingAddress?.PostalCode;
                var billingNumber = request.BillingAddress?.Number;
                var billingNoNumber = string.Equals((billingNumber ?? "").Trim(), CreditCardHolderBilling.NoNumberValue, StringComparison.OrdinalIgnoreCase);
                var (holderPostal, holderNumber) = CreditCardHolderBilling.NormalizeOrThrow(billingPostal, billingNumber, billingNoNumber);
                var clientAddress = new Application.DTOs.PagBank.PagBankAddress
                {
                    Street = request.BillingAddress?.Street ?? string.Empty,
                    Number = holderNumber,
                    Locality = request.BillingAddress?.Locality ?? string.Empty,
                    City = request.BillingAddress?.City ?? string.Empty,
                    RegionCode = request.BillingAddress?.RegionCode ?? string.Empty,
                    Country = request.BillingAddress?.Country ?? "BRA",
                    PostalCode = holderPostal
                };
                var orderResponse = await _asaasService.CreateOrderAsync(
                    referenceId: purchase.ExternalPaymentId,
                    amount: purchase.AmountPaid,
                    method: PaymentMethod.CreditCard,
                    clientName: clientName,
                    clientEmail: clientEmail,
                    clientTaxId: clientTaxId,
                    clientPhone: clientPhone,
                    card: request.Card,
                    installments: request.Installments,
                    clientAddress: clientAddress,
                    remoteIp: HttpContext.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1"
                );
                purchase.ExternalPaymentId = orderResponse.Id;
                if (orderResponse.Charges != null && orderResponse.Charges.Any())
                {
                    var charge = orderResponse.Charges.First();
                    purchase.TransactionId = charge.Id;
                    var status = charge.Status?.ToUpper();
                    if (status == "PAID")
                    {
                        purchase.Status = CreditPurchaseStatus.Paid;
                        purchase.PaidAt = charge.PaidAt ?? DateTimeHelper.GetBrazilianTime();
                        var expiresAt = DateTimeHelper.GetBrazilianTime().AddMonths(purchase.CreditPlan.ValidityMonths);
                        var clientCredit = new ClientCredit
                        {
                            ClientId = client.Id,
                            Credits = purchase.CreditsReceived + purchase.BonusCredits,
                            ExpiresAt = expiresAt
                        };
                        _context.ClientCredits.Add(clientCredit);
                        try
                        {
                            var total = purchase.CreditsReceived + purchase.BonusCredits;
                            var plat = await _context.PlatformCreditBalances.FirstOrDefaultAsync();
                            if (plat == null)
                            {
                                plat = new PlatformCreditBalance { TotalCredits = 0 };
                                _context.PlatformCreditBalances.Add(plat);
                            }
                            plat.TotalCredits += total;
                            plat.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else if (status == "DECLINED" || status == "CANCELLED")
                    {
                        purchase.Status = CreditPurchaseStatus.Cancelled;
                    }
                    else
                    {
                        purchase.Status = CreditPurchaseStatus.Processing;
                    }
                }
                await _context.SaveChangesAsync();
                if (purchase.Status == CreditPurchaseStatus.Paid)
                {
                    return Json(new { success = true, message = JsonMessage.PagamentoAprovadoCreditosCreditados });
                }
                else if (purchase.Status == CreditPurchaseStatus.Cancelled)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentDeclinedVerifyData });
                }
                else
                {
                    return Json(new { success = true, message = JsonMessage.PagamentoEmProcessamentoSeraNotificado });
                }
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pagamento com cartão (PurchaseId: {PurchaseId})", request?.PurchaseId);
                return Json(new { success = false, error = ErrorJson.CouldNotProcessPayment });
            }
        }
        [HttpGet]
        public async Task<IActionResult> BookRoom(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.Marketplace, ControllerNames.Home);
            }
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.Marketplace, ControllerNames.Home);
            }
            var offersRoomRental = address.Coworking?.Products?.Any(p => p.ProductType == "RoomRental" && p.IsActive) ?? false;
            if (!offersRoomRental)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNoRoomRental;
                return RedirectToAction(ActionNames.MarketplaceDetails, ControllerNames.Home, new { id });
            }
            var availableCredits = await _context.ClientCredits
                .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTimeHelper.GetBrazilianTime())
                .SumAsync(c => c.Credits);
            ViewBag.Address = address;
            ViewBag.Coworking = address.Coworking;
            ViewBag.OffersSharedRoom = address.OffersSharedRoom;
            ViewBag.OffersPrivateRoom = address.OffersPrivateRoom;
            ViewBag.SharedRoomPricePerHour = address.SharedRoomPricePerHour ?? 0;
            ViewBag.PrivateRoomPricePerHour = address.PrivateRoomPricePerHour ?? 0;
            ViewBag.GetAvailabilityForMonthUrl = Url.Action(ActionNames.GetAvailabilityForMonth, ControllerNames.Home);
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservations(int CoworkingAddressId, string RoomType, string slotsJson, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.Marketplace, ControllerNames.Home);
            }
            List<ReservationSlotDto>? slots;
            try
            {
                slots = System.Text.Json.JsonSerializer.Deserialize<List<ReservationSlotDto>>(slotsJson);
            }
            catch
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidReservationData;
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            if (slots == null || !slots.Any())
            {
                TempData[ErrorMessages.TempDataKey] = "Selecione ao menos uma data e horário.";
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(a => a.Id == CoworkingAddressId);
            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.Marketplace, ControllerNames.Home);
            }
            var offersRoomRental = await _context.CoworkingProducts
                .AnyAsync(p => p.CoworkingId == address.CoworkingId && p.ProductType == "RoomRental" && p.IsActive);
            if (!offersRoomRental)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNoRoomRental;
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            if (RoomType != "Shared" && RoomType != "Private")
            {
                TempData[ErrorMessages.TempDataKey] = "Tipo de sala inválido.";
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            if (RoomType == "Shared" && !address.OffersSharedRoom) { TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNoSharedRoom; return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId }); }
            if (RoomType == "Private" && !address.OffersPrivateRoom) { TempData[ErrorMessages.TempDataKey] = "Este endereço não oferece sala privativa."; return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId }); }
            decimal pricePerHour = RoomType == "Shared" ? (address.SharedRoomPricePerHour ?? 0) : (address.PrivateRoomPricePerHour ?? 0);
            int maxSeats = RoomType == "Shared" ? (address.SharedRoomMaxSeats ?? 0) : (address.PrivateRoomMaxSeats ?? 0);
            if (pricePerHour <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = "Preço por hora não configurado para este tipo de sala.";
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            var toCreate = new List<(DateTime Date, TimeSpan Start, TimeSpan End)>();
            foreach (var s in slots)
            {
                if (!DateTime.TryParse(s.Date, out var date) || !TimeSpan.TryParse(s.StartTime, out var start) || !TimeSpan.TryParse(s.EndTime, out var end))
                    continue;
                if (date < DateTime.Today) continue;
                if (end <= start) continue;
                toCreate.Add((date.Date, start, end));
            }
            toCreate = toCreate
                .GroupBy(x => (x.Date, x.Start, x.End))
                .Select(g => g.First())
                .ToList();
            if (!toCreate.Any())
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.NoValidTimeSlots;
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            int totalCredits = 0;
            foreach (var (Date, Start, End) in toCreate)
            {
                var duration = End - Start;
                var hours = (decimal)Math.Max(1, Math.Ceiling(duration.TotalHours));
                totalCredits += (int)Math.Ceiling(pricePerHour * hours);
            }
            var availableCredits = await _context.ClientCredits
                .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTimeHelper.GetBrazilianTime())
                .SumAsync(c => c.Credits);
            if (availableCredits < totalCredits)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InsufficientCredits(totalCredits, availableCredits);
                return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
            }
            var addedInRequest = new List<(DateTime Date, TimeSpan Start, TimeSpan End)>();
            foreach (var (Date, Start, End) in toCreate)
            {
                var duration = End - Start;
                var hours = (decimal)Math.Max(1, Math.Ceiling(duration.TotalHours));
                var totalValue = pricePerHour * hours;
                var creditsNeeded = (int)Math.Ceiling(totalValue);
                var hasConflict = false;
                hasConflict = addedInRequest.Any(t => t.Date == Date && !(t.End <= Start || t.Start >= End));
                if (!hasConflict)
                {
                    hasConflict = await _context.Reservations
                        .AnyAsync(r => r.ClientId == client.Id && r.CoworkingAddressId == CoworkingAddressId &&
                            r.ReservationDate.Date == Date && r.RoomType == RoomType && r.Status != ReservationStatus.Cancelled &&
                            r.StartTime.HasValue && r.EndTime.HasValue && !(r.EndTime <= Start || r.StartTime >= End));
                }
                if (hasConflict)
                {
                    TempData[ErrorMessages.TempDataKey] = "Você já possui uma reserva neste horário ou há sobreposição entre os horários selecionados. Não é permitida duplicidade.";
                    return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
                }
                if (RoomType == "Private")
                {
                    hasConflict = await _context.Reservations
                        .AnyAsync(r => r.CoworkingAddressId == CoworkingAddressId && r.ReservationDate.Date == Date &&
                            r.RoomType == "Private" && r.Status != ReservationStatus.Cancelled && r.StartTime.HasValue && r.EndTime.HasValue &&
                            !(r.EndTime <= Start || r.StartTime >= End));
                }
                else
                {
                    var countDb = await _context.Reservations
                        .CountAsync(r => r.CoworkingAddressId == CoworkingAddressId && r.ReservationDate.Date == Date &&
                            r.RoomType == "Shared" && r.Status != ReservationStatus.Cancelled && r.StartTime.HasValue && r.EndTime.HasValue &&
                            !(r.EndTime <= Start || r.StartTime >= End));
                    var countAdded = addedInRequest.Count(t => t.Date == Date && !(t.End <= Start || t.Start >= End));
                    hasConflict = maxSeats > 0 && (countDb + countAdded >= maxSeats);
                }
                if (hasConflict)
                {
                    TempData[ErrorMessages.TempDataKey] = $"O horário {Date:dd/MM/yyyy} {Start:hh\\:mm}-{End:hh\\:mm} já está reservado. Escolha outro horário.";
                    return RedirectToAction(ActionNames.BookRoom, new { id = CoworkingAddressId });
                }
                var reservation = new Reservation
                {
                    ClientId = client.Id,
                    CoworkingAddressId = CoworkingAddressId,
                    ReservationDate = Date,
                    StartTime = Start,
                    EndTime = End,
                    RoomType = RoomType,
                    TotalValue = totalValue,
                    Status = ReservationStatus.Confirmed,
                    CreatedAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.Reservations.Add(reservation);
            }
            var creditsToDeduct = totalCredits;
            var credits = await _context.ClientCredits
                .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTimeHelper.GetBrazilianTime() && c.Credits > 0)
                .OrderBy(c => c.ExpiresAt)
                .ToListAsync();
            foreach (var credit in credits)
            {
                if (creditsToDeduct <= 0) break;
                if (credit.Credits >= creditsToDeduct) { credit.Credits -= creditsToDeduct; creditsToDeduct = 0; }
                else { creditsToDeduct -= credit.Credits; credit.Credits = 0; }
            }
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = toCreate.Count == 1
                ? SuccessMessage.SingleReservationConfirmed(toCreate[0].Date)
                : SuccessMessage.MultipleReservationsConfirmed(toCreate.Count);
            return RedirectToAction(ActionNames.MyReservations);
        }
        [HttpGet]
        public async Task<IActionResult> MyReservations(string? sortBy = "date_asc", string? status = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string? search = null, string? roomType = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.DashboardDetails);
                }
                var query = _context.Reservations
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Location)
                    .Where(r => r.ClientId == client.Id);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var s = status.Trim();
                    if (s == nameof(ReservationStatus.Confirmed) || s == nameof(ReservationStatus.Completed) || s == nameof(ReservationStatus.Cancelled))
                        if (Enum.TryParse<ReservationStatus>(s, true, out var resStatus))
                            query = query.Where(r => r.Status == resStatus);
                }
                if (dateFrom.HasValue)
                    query = query.Where(r => r.ReservationDate.Date >= dateFrom.Value.Date);
                if (dateTo.HasValue)
                    query = query.Where(r => r.ReservationDate.Date <= dateTo.Value.Date);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var t = search.Trim().ToLower();
                    query = query.Where(r =>
                        (r.CoworkingAddress.Coworking.Name != null && r.CoworkingAddress.Coworking.Name.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.Address != null && r.CoworkingAddress.Address.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.City != null && r.CoworkingAddress.City.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.Location != null && r.CoworkingAddress.Location.City != null && r.CoworkingAddress.Location.City.ToLower().Contains(t)));
                }
                if (!string.IsNullOrWhiteSpace(roomType))
                {
                    var rt = roomType.Trim();
                    if (rt == "Shared" || rt == "Private")
                        query = query.Where(r => r.RoomType == rt);
                }
                var sort = (sortBy ?? "date_asc").ToLower();
                if (sort == "date_desc")
                    query = query.OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.StartTime ?? TimeSpan.Zero);
                else if (sort == "time_desc")
                    query = query.OrderBy(r => r.ReservationDate).ThenByDescending(r => r.StartTime ?? TimeSpan.Zero);
                else if (sort == "time_asc")
                    query = query.OrderBy(r => r.ReservationDate).ThenBy(r => r.StartTime ?? TimeSpan.Zero);
                else
                    query = query.OrderBy(r => r.ReservationDate).ThenBy(r => r.StartTime ?? TimeSpan.Zero);
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;
                var reservations = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
                var allReservations = await query.ToListAsync();
                ViewBag.TotalReservations = allReservations.Count;
                ViewBag.TotalConfirmed = allReservations.Count(r => r.Status == ReservationStatus.Confirmed);
                ViewBag.TotalCompleted = allReservations.Count(r => r.Status == ReservationStatus.Completed);
                ViewBag.TotalCancelled = allReservations.Count(r => r.Status == ReservationStatus.Cancelled);
                ViewBag.SortBy = sortBy ?? "date_asc";
                ViewBag.Status = status;
                ViewBag.DateFrom = dateFrom;
                ViewBag.DateTo = dateTo;
                ViewBag.Search = search;
                ViewBag.RoomType = roomType;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = pageSize;
                return View(reservations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = $"Erro ao carregar reservas: {ex.Message}";
                return RedirectToAction(ActionNames.DashboardDetails);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservationUsed(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null) { TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound; return RedirectToAction(ActionNames.MyReservations); }
            var r = await _context.Reservations.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == client.Id);
            if (r == null) { TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationNotFound; return RedirectToAction(ActionNames.MyReservations); }
            if (r.Status == ReservationStatus.Cancelled) { TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationCancelled; return RedirectToAction(ActionNames.MyReservations); }
            if (r.Status == ReservationStatus.Completed) { TempData[SuccessMessage.TempDataKey] = SuccessMessage.ReservationAlreadyCompleted; return RedirectToAction(ActionNames.MyReservations); }
            if (r.ClientConfirmedAt == null)
            {
                r.ClientConfirmedAt = DateTimeHelper.GetBrazilianTime();
                r.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
            }
            await _reservationTransferService.ExecuteTransferIfBothConfirmedAsync(r);
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.ConfirmationRecorded;
            return RedirectToAction(ActionNames.MyReservations);
        }
        [HttpGet]
        public async Task<IActionResult> FiscalNotes(int page = 1, int pageSize = 10, string? status = null, string? type = null, 
            string? search = null, int? coworkingId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
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
                .Include(fn => fn.Payment)
                .Where(fn => fn.ClientId == client.Id 
                    && (fn.Type == FiscalNoteType.Intermediacao || fn.Type == FiscalNoteType.Servico)
                    && fn.Status == FiscalNoteStatus.Emitida)
                .AsQueryable();
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(fn => fn.Type == type);
            }
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(fn => 
                    (fn.Number != null && fn.Number.Contains(search)) ||
                    (fn.AccessKey != null && fn.AccessKey.Contains(search)));
            }
            if (coworkingId.HasValue)
            {
                query = query.Where(fn => fn.CoworkingId == coworkingId.Value);
            }
            if (startDate.HasValue)
            {
                query = query.Where(fn => fn.IssueDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(fn => fn.IssueDate <= endDate.Value.AddDays(1).AddSeconds(-1));
            }
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            var fiscalNotes = await query
                .OrderByDescending(fn => fn.IssueDate)
                .ThenByDescending(fn => fn.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var coworkings = await _context.FiscalNotes
                .Include(fn => fn.Coworking)
                .Where(fn => fn.ClientId == client.Id 
                    && (fn.Type == FiscalNoteType.Intermediacao || fn.Type == FiscalNoteType.Servico)
                    && fn.Status == FiscalNoteStatus.Emitida
                    && fn.CoworkingId != null
                    && fn.Coworking != null)
                .Select(fn => fn.Coworking)
                .Distinct()
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewBag.Type = type;
            ViewBag.Search = search;
            ViewBag.CoworkingId = coworkingId;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Coworkings = coworkings;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            return View(fiscalNotes);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadFiscalNotePdf(int fiscalNoteId, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var fn = await _context.FiscalNotes
                .FirstOrDefaultAsync(f => f.Id == fiscalNoteId 
                    && f.ClientId == client.Id 
                    && (f.Type == FiscalNoteType.Intermediacao || f.Type == FiscalNoteType.Servico)
                    && f.Status == FiscalNoteStatus.Emitida);
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
            Response.Headers.Append(HttpHeaderNames.ContentDisposition, $"inline; filename=\"{fileName}\"");
            return File(fileBytes, "application/pdf");
        }
        [HttpGet]
        public Task<IActionResult> CommissionFiscalNotes(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.FiscalNotes));
        }
        [HttpGet]
        public async Task<IActionResult> Coworkings(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var query = _context.Coworkings
                .Where(c => c.IsActive && c.IsPartner)
                .Include(c => c.Addresses)
                .Include(c => c.User)
                .AsQueryable();
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            var coworkings = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            return View(coworkings);
        }
        [HttpGet]
        public async Task<IActionResult> ServiceRemovalRequests(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }
            var clientCoworkingIds = await _context.Contracts
                .Where(c => c.ClientId == client.Id)
                .Include(c => c.CoworkingAddress)
                .Select(c => c.CoworkingAddress.CoworkingId)
                .Distinct()
                .ToListAsync();
            var query = _context.PartnerServices
                .Include(ps => ps.Service)
                .Include(ps => ps.Coworking)
                .Where(ps => ps.RemovalRequestStatus != RemovalRequestStatus.None && 
                           clientCoworkingIds.Contains(ps.CoworkingId))
                .AsQueryable();
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;
            var removalRequests = await query
                .OrderByDescending(ps => ps.RemovalRequestedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            return View(removalRequests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyPromoCode(int paymentId, string promoCode, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _clientAppService.GetClientByUserIdAsync(userId, cancellationToken);
                if (client == null)
                    return RedirectToAction(ActionNames.RegisterClient);
                var contract = payment.SubscriptionId.HasValue
                    ? await _clientAppService.GetContractByIdAsync(
                        (await _context.Subscriptions.AsNoTracking()
                            .Where(s => s.Id == payment.SubscriptionId.Value)
                            .Select(s => s.ContractId)
                            .FirstOrDefaultAsync(cancellationToken)),
                        cancellationToken)
                    : null;
                if (contract == null || contract.ClientId != client.Id)
                    return Forbid();
                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = "Cupom só pode ser aplicado em pagamentos pendentes.";
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var listPrice = ResolveSnapshotListPrice(payment, contract);
                var surcharge = await GetContractSurchargePercentAsync(contract, cancellationToken);
                var promoBaseAmount = ResolvePromoBaseAmount(
                    listPrice,
                    surcharge,
                    payment.Method,
                    payment.Method == PaymentMethod.CreditCard ? contract.CardPlanType : null);
                var address = await _context.CoworkingAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == contract.CoworkingAddressId, cancellationToken);
                if (address == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                var result = await _promoCodeService.ValidateAndApplyAsync(
                    promoCode,
                    address.CoworkingId,
                    address.Id,
                    payment.Method,
                    promoBaseAmount,
                    cancellationToken);
                if (!result.IsValid)
                {
                    TempData[ErrorMessages.TempDataKey] = result.ErrorMessage ?? "Cupom inválido.";
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }
                payment.PromoCodeId = result.PromoCodeId;
                payment.PromoCodeSnapshot = result.PromoCodeSnapshot;
                payment.SnapshotDiscountAmount = result.DiscountAmount;
                payment.SnapshotListPrice = listPrice;
                var chargeAmount = CalculateCheckoutAmount(
                    listPrice,
                    result.DiscountAmount,
                    surcharge,
                    payment.Method,
                    payment.Method == PaymentMethod.CreditCard ? contract.CardPlanType : null);
                payment.Amount = chargeAmount;
                payment.UpdatedAt = DateTime.UtcNow;
                contract.MonthlyValue = chargeAmount;
                contract.SnapshotMonthlyValue = chargeAmount;
                if (payment.SubscriptionId.HasValue)
                {
                    var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value, cancellationToken);
                    if (subscription != null)
                        subscription.MonthlyAmount = chargeAmount;
                }
                await _context.SaveChangesAsync(cancellationToken);
                TempData[SuccessMessage.TempDataKey] = $"Cupom aplicado. Desconto de R$ {result.DiscountAmount:N2}.";
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("aplicar cupom", ex.Message);
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
        }

        /// <summary>
        /// Returns a file as attachment (download) or inline (browser preview).
        /// </summary>
        private IActionResult FileResultWithDisposition(byte[] content, string contentType, string fileName, bool download)
        {
            if (download)
                return File(content, contentType, fileName);

            Response.Headers.Append(HttpHeaderNames.ContentDisposition, $"inline; filename=\"{fileName}\"");
            return File(content, contentType);
        }
    }
}