using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using KlyonHubPlatform.Api.Constants;
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
        private readonly ApplicationDbContext _context;
        private readonly IReservationTransferService _reservationTransferService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClientController> _logger;
        private readonly ISystemConfigurationService _systemConfig;
        private readonly IClientMenuService _clientMenuService;
        private readonly IServiceCatalogService _serviceCatalogService;
        private readonly IClientDashboardService _clientDashboardService;

        public ClientController(
            IContractService contractService,
            ICoworkingService coworkingService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IDocumentService documentService,
            IAddressDeclarationService addressDeclarationService,
            IAsaasService asaasService,
            ApplicationDbContext context,
            IReservationTransferService reservationTransferService,
            IEmailService emailService,
            ILogger<ClientController> logger,
            ISystemConfigurationService systemConfig,
            IClientMenuService clientMenuService,
            IServiceCatalogService serviceCatalogService,
            IClientDashboardService clientDashboardService)
        {
            _contractService = contractService;
            _coworkingService = coworkingService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _documentService = documentService;
            _addressDeclarationService = addressDeclarationService;
            _asaasService = asaasService;
            _context = context;
            _reservationTransferService = reservationTransferService;
            _emailService = emailService;
            _logger = logger;
            _systemConfig = systemConfig;
            _clientMenuService = clientMenuService;
            _serviceCatalogService = serviceCatalogService;
            _clientDashboardService = clientDashboardService;
        }

        private async Task<long> GetMaxPhotoSizeBytesAsync()
        {
            var mb = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mb, out var n) && n > 0) return n * 1024L * 1024L;
            return 5 * 1024L * 1024L;
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
            // Redirecionar para a página de Serviços (página principal do cliente)
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
            return View(ViewNames.Dashboard);
        }

        [HttpGet]
        public async Task<IActionResult> Profile(CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFoundCompleteRegistration;
                return RedirectToAction(ActionNames.RegisterClient);
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;

            var maxPhotoMb = 5;
            var mbStr = await _systemConfig.GetValueAsync(UploadConfigKey.MaxPhotoSizeMB.ToConfigKey());
            if (int.TryParse(mbStr, out var n) && n > 0) maxPhotoMb = n;
            ViewBag.MaxPhotoSizeMB = maxPhotoMb;
            
            return View(client);
        }

        [HttpGet]
        public async Task<IActionResult> GetLogo(int clientId, CancellationToken cancellationToken = default)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            
            if (client == null || string.IsNullOrEmpty(client.LogoPath))
            {
                return NotFound();
            }

            // Verificar se o usuário logado tem permissão para ver a logo
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var userClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            
            // Permitir acesso se:
            // 1. É o próprio cliente
            // 2. É administrador
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

            // Detectar tipo de conteúdo baseado na extensão
            var contentType = "image/jpeg"; // padrão
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
                new Claim(ClaimTypes.Name, user.FullName ?? "")
            };
            foreach (var roleClaim in User.FindAll(ClaimTypes.Role))
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            var claimsIdentity = new ClaimsIdentity(claims, AuthConstants.CookieScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(AuthConstants.CookieScheme, claimsPrincipal);
            if (!string.IsNullOrEmpty(user.FullName))
                HttpContext.Session.SetString("UserName", user.FullName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Client client, IFormFile? Logo, CancellationToken cancellationToken = default)
        {
            var isAjaxRequest = Request.Headers[HttpHeaderNames.XRequestedWith].ToString() == HttpHeaderNames.XRequestedWithValue;
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (existingClient == null)
                {
                    var err = "Cliente não encontrado.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // Validar CPF e CNPJ usando DocumentValidator
                var cnpjInput = string.IsNullOrWhiteSpace(client.CNPJ) ? null : client.CNPJ;
                var cpfInput = string.IsNullOrWhiteSpace(client.CPF) ? null : client.CPF;

                // Validar CNPJ se informado
                if (!string.IsNullOrWhiteSpace(cnpjInput))
                {
                    var cnpjValidation = DocumentValidator.ValidateCPForCNPJ(cnpjInput);
                    if (!cnpjValidation.isValid || cnpjValidation.type != TipoDocumentoCliente.CNPJ)
                    {
                        TempData[ErrorMessages.TempDataKey] = cnpjValidation.message;
                        if (isAjaxRequest) return Json(new { success = false, error = cnpjValidation.message });
                        return RedirectToAction(ActionNames.Profile);
                    }

                    // Verificar se outro cliente já possui este CNPJ
                    var cnpjClean = cnpjInput.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    var allClients = await _context.Clients
                        .Where(c => c.CNPJ != null && c.Id != existingClient.Id)
                        .ToListAsync();
                    var existingCNPJ = allClients.FirstOrDefault(c => 
                        c.CNPJ != null && 
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                    
                    if (existingCNPJ != null)
                    {
                        var err = $"O CNPJ {client.CNPJ} já está cadastrado no sistema.";
                        TempData[ErrorMessages.TempDataKey] = err;
                        if (isAjaxRequest) return Json(new { success = false, error = err });
                        return RedirectToAction(ActionNames.Profile);
                    }
                    
                    // Salvar CNPJ com máscara
                    existingClient.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    _context.Entry(existingClient).Property(c => c.CNPJ).IsModified = true;
                }
                else
                {
                    existingClient.CNPJ = null;
                    _context.Entry(existingClient).Property(c => c.CNPJ).IsModified = true;
                }

                // Validar CPF se informado
                if (!string.IsNullOrWhiteSpace(cpfInput))
                {
                    var cpfValidation = DocumentValidator.ValidateCPForCNPJ(cpfInput);
                    if (!cpfValidation.isValid || cpfValidation.type != TipoDocumentoCliente.CPF)
                    {
                        TempData[ErrorMessages.TempDataKey] = cpfValidation.message;
                        if (isAjaxRequest) return Json(new { success = false, error = cpfValidation.message });
                        return RedirectToAction(ActionNames.Profile);
                    }

                    // Verificar se outro cliente já possui este CPF
                    var cpfClean = cpfInput.Replace(".", "").Replace("-", "").Trim();
                    var allClients = await _context.Clients
                        .Where(c => c.CPF != null && c.Id != existingClient.Id)
                        .ToListAsync();
                    var existingCPF = allClients.FirstOrDefault(c => 
                        c.CPF != null && 
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                    
                    if (existingCPF != null)
                    {
                        var err = $"O CPF {client.CPF} já está cadastrado no sistema.";
                        TempData[ErrorMessages.TempDataKey] = err;
                        if (isAjaxRequest) return Json(new { success = false, error = err });
                        return RedirectToAction(ActionNames.Profile);
                    }
                    
                    // Salvar CPF com máscara
                    existingClient.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    _context.Entry(existingClient).Property(c => c.CPF).IsModified = true;
                }
                else
                {
                    existingClient.CPF = null;
                    _context.Entry(existingClient).Property(c => c.CPF).IsModified = true;
                }

                // CPF ou CNPJ é obrigatório: pessoa jurídica = CNPJ, pessoa física = CPF
                var cnpjPreenchido = !string.IsNullOrWhiteSpace(cnpjInput);
                var cpfPreenchido = !string.IsNullOrWhiteSpace(cpfInput);
                if (!cnpjPreenchido && !cpfPreenchido)
                {
                    var err = "Informe o CPF (pessoa física) ou o CNPJ (pessoa jurídica).";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // Estado é obrigatório (mesmo padrão da tela RegisterClient)
                if (string.IsNullOrWhiteSpace(client.State))
                {
                    var err = "O Estado é obrigatório.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // Telefone é obrigatório (mesmo padrão da tela RegisterClient)
                if (string.IsNullOrWhiteSpace(client.ContactPhone))
                {
                    var err = "O Telefone é obrigatório.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // Nome Completo é obrigatório
                if (string.IsNullOrWhiteSpace(client.ContactName))
                {
                    var err = "O Nome Completo é obrigatório.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // CEP é obrigatório (exigido pelo Asaas para pagamento com cartão - titular do cartão)
                var zipCodeRaw = (client.ZipCode ?? "").Replace("-", "").Replace(" ", "").Trim();
                if (string.IsNullOrWhiteSpace(zipCodeRaw))
                {
                    var err = "O CEP é obrigatório.";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }
                if (zipCodeRaw.Length != 8 || !zipCodeRaw.All(char.IsDigit))
                {
                    var err = "Informe um CEP válido (8 dígitos).";
                    TempData[ErrorMessages.TempDataKey] = err;
                    if (isAjaxRequest) return Json(new { success = false, error = err });
                    return RedirectToAction(ActionNames.Profile);
                }

                // Atualizar campos (exceto ContactEmail que sempre vem do usuário)
                existingClient.RazaoSocial = client.RazaoSocial;
                existingClient.NomeFantasia = client.NomeFantasia;
                existingClient.ContactName = client.ContactName;
                existingClient.ContactEmail = userEmail; // Sempre usar o email do usuário logado

                // Sincronizar FullName na tabela Users (persistido junto com o Client em um único SaveChanges no final)
                var userToUpdate = await _context.Users.FindAsync(userId);
                if (userToUpdate != null && !string.IsNullOrWhiteSpace(client.ContactName))
                {
                    userToUpdate.FullName = client.ContactName.Trim();
                    userToUpdate.UpdatedAt = DateTime.UtcNow;
                }
                
                // Remover máscaras do telefone e CEP antes de salvar (mantendo consistência com RegisterClient)
                if (!string.IsNullOrEmpty(client.ContactPhone))
                {
                    existingClient.ContactPhone = client.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                }
                else
                {
                    existingClient.ContactPhone = null;
                }
                
                if (!string.IsNullOrEmpty(client.ZipCode))
                {
                    existingClient.ZipCode = client.ZipCode.Replace("-", "");
                }
                else
                {
                    existingClient.ZipCode = null;
                }
                
                existingClient.Address = client.Address;
                existingClient.AddressNumber = client.AddressNumber;
                existingClient.Neighborhood = client.Neighborhood;
                existingClient.Complement = client.Complement;
                existingClient.City = client.City;
                existingClient.State = client.State;
                
                // Upload da logo se fornecida
                if (Logo != null && Logo.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(Logo.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        var err = "Formato de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.";
                        TempData[ErrorMessages.TempDataKey] = err;
                        if (isAjaxRequest) return Json(new { success = false, error = err });
                        return RedirectToAction(ActionNames.Profile);
                    }
                    
                    var maxBytes = await GetMaxPhotoSizeBytesAsync();
                    var maxMb = (int)(maxBytes / (1024 * 1024));
                    if (Logo.Length > maxBytes)
                    {
                        var err = $"O arquivo é muito grande. O tamanho máximo é {maxMb}MB. (Configurável em Admin > Configurações > Upload.MaxPhotoSizeMB)";
                        TempData[ErrorMessages.TempDataKey] = err;
                        if (isAjaxRequest) return Json(new { success = false, error = err });
                        return RedirectToAction(ActionNames.Profile);
                    }
                    
                    try
                    {
                        var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
                        var storageService = await storageFactory.GetStorageServiceAsync();
                        
                        using var stream = new MemoryStream();
                        await Logo.CopyToAsync(stream);
                        var fileBytes = stream.ToArray();
                        
                        var fileName = $"logo_{existingClient.Id}_{DateTimeHelper.GetBrazilianTime():yyyyMMddHHmmss}{fileExtension}";
                        var folder = $"clients/{existingClient.Id}";
                        var filePath = await storageService.SaveFileAsync(fileBytes, fileName, folder);
                        
                        existingClient.LogoPath = filePath;
                    }
                    catch (Exception ex)
                    {
                        var err = $"Erro ao fazer upload da logo: {ex.Message}";
                        TempData[ErrorMessages.TempDataKey] = err;
                        if (isAjaxRequest) return Json(new { success = false, error = err });
                        return RedirectToAction(ActionNames.Profile);
                    }
                }
                
                existingClient.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                if (userToUpdate != null)
                    await RefreshUserPrincipalAsync(userToUpdate);

                if (isAjaxRequest)
                    return Json(new { success = true, message = JsonMessage.PerfilAtualizadoComSucesso, contactName = existingClient.ContactName });
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.ProfileUpdated;
                return RedirectToAction(ActionNames.Profile);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                var err = "Este CNPJ ou CPF já está cadastrado no sistema. Verifique os dados informados.";
                TempData[ErrorMessages.TempDataKey] = err;
                if (isAjaxRequest) return Json(new { success = false, error = err });
                return RedirectToAction(ActionNames.Profile);
            }
            catch (Exception ex)
            {
                var err = $"Erro ao atualizar perfil: {ex.Message}";
                TempData[ErrorMessages.TempDataKey] = err;
                if (isAjaxRequest) return Json(new { success = false, error = err });
                return RedirectToAction(ActionNames.Profile);
            }
        }

        [HttpGet]
        public async Task<IActionResult> RegisterClient(int? id = null, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
            ViewBag.ReturnUrl = returnUrl;
            
            // Se houver um ID, carregar o cliente para edição
            if (id.HasValue)
            {
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == id.Value);
                
                if (client != null)
                {
                    // Se for admin ou o próprio cliente, permitir edição
                    if (User.IsInRole(Administrator) || client.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"))
                    {
                        ViewBag.PersonType = string.IsNullOrWhiteSpace(client.CNPJ) ? "Fisica" : "Juridica";
                        return View(client);
                    }
                }
            }
            
            return View();
        }

        /// <summary>
        /// Verifica se CPF ou CNPJ já está cadastrado para outro cliente. Usado para validação em tempo real no formulário.
        /// </summary>
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
                var query = _context.Clients.Where(c => c.CPF != null);
                if (excludeClientId.HasValue && excludeClientId.Value > 0)
                    query = query.Where(c => c.Id != excludeClientId.Value);
                var allClients = await query.ToListAsync(cancellationToken);
                var existing = allClients.FirstOrDefault(c =>
                    c.CPF != null &&
                    c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                if (existing != null)
                {
                    return Json(new { valid = true, exists = true, message = string.Format(JsonMessage.CpfJaCadastradoNoSistema, document) });
                }
            }
            else
            {
                var cnpjClean = document.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var query = _context.Clients.Where(c => c.CNPJ != null);
                if (excludeClientId.HasValue && excludeClientId.Value > 0)
                    query = query.Where(c => c.Id != excludeClientId.Value);
                var allClients = await query.ToListAsync(cancellationToken);
                var existing = allClients.FirstOrDefault(c =>
                    c.CNPJ != null &&
                    c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                if (existing != null)
                {
                    return Json(new { valid = true, exists = true, message = string.Format(JsonMessage.CnpjJaCadastradoNoSistema, document) });
                }
            }

            return Json(new { valid = true, exists = false, message = JsonMessage.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClient(Client client, string? returnUrl = null, string? PersonType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var isAdmin = User.IsInRole(Administrator);
                var personType = (PersonType ?? "").Trim();
                ViewBag.PersonType = string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) ? "Juridica" : "Fisica";

                if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase))
                {
                    client.CPF = null;
                }
                else
                {
                    client.CNPJ = null;
                    client.RazaoSocial = null;
                    client.NomeFantasia = null;
                }

                // Se houver ID, é uma edição
                if (client.Id > 0)
                {
                    var existingClient = await _context.Clients
                        .Include(c => c.User)
                        .FirstOrDefaultAsync(c => c.Id == client.Id);
                    
                    if (existingClient == null)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    
                    // Verificar permissão: admin ou o próprio cliente
                    if (!isAdmin && existingClient.UserId != userId)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.NoPermissionToEditClient;
                        return RedirectToAction(ActionNames.DashboardDetails);
                    }
                    
                    // Nome Completo é obrigatório apenas para Pessoa Física
                    if (!string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.FullNameRequired;
                        ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                        return View(existingClient);
                    }
                    if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                        client.ContactName = client.RazaoSocial ?? client.NomeFantasia ?? existingClient.ContactName;
                    
                    // Telefone é obrigatório
                    if (string.IsNullOrWhiteSpace(client.ContactPhone))
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.PhoneRequired;
                        ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                        return View(existingClient);
                    }
                    
                    // Atualizar campos
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
                    
                    // Se for admin, permitir alterar email
                    if (isAdmin && !string.IsNullOrEmpty(client.ContactEmail))
                    {
                        existingClient.ContactEmail = client.ContactEmail;
                    }
                    
                    // Validar e atualizar CNPJ/CPF usando DocumentValidator
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
                        // Verificar se outro cliente já possui este CNPJ
                        var allClients = await _context.Clients
                            .Where(c => c.CNPJ != null && c.Id != existingClient.Id)
                            .ToListAsync();
                        var existingCNPJ = allClients.FirstOrDefault(c => 
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
                        // Verificar se outro cliente já possui este CPF
                        var allClients = await _context.Clients
                            .Where(c => c.CPF != null && c.Id != existingClient.Id)
                            .ToListAsync();
                        var existingCPF = allClients.FirstOrDefault(c => 
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
                    
                    // Remover máscaras do telefone e CEP
                    if (!string.IsNullOrEmpty(existingClient.ContactPhone))
                    {
                        existingClient.ContactPhone = existingClient.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                    }
                    if (!string.IsNullOrEmpty(existingClient.ZipCode))
                    {
                        existingClient.ZipCode = existingClient.ZipCode.Replace("-", "");
                    }
                    
                    // Sincronizar Nome Completo na tabela Users e atualizar a sessão (mesmo comportamento do Profile)
                    if (existingClient.User != null)
                        existingClient.User.FullName = existingClient.ContactName ?? "";
                    
                    await _context.SaveChangesAsync();
                    if (!isAdmin && existingClient.User != null)
                        await RefreshUserPrincipalAsync(existingClient.User);
                    TempData[SuccessMessage.TempDataKey] = SuccessMessage.ClientUpdated;
                    
                    if (isAdmin)
                    {
                        return RedirectToAction(ActionNames.Contracts, ControllerNames.Admin);
                    }
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction(ActionNames.DashboardDetails);
                }
                
                // Se não houver ID, é um novo cadastro
                // Verificar se o usuário já possui um cliente cadastrado
                var userClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (userClient != null && !isAdmin)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientAlreadyHasRegistration;
                    return RedirectToAction(ActionNames.DashboardDetails);
                }

                // Nome Completo é obrigatório apenas para Pessoa Física
                if (!string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.FullNameRequired;
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }
                if (string.Equals(personType, "Juridica", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(client.ContactName))
                    client.ContactName = client.RazaoSocial?.Trim() ?? client.NomeFantasia?.Trim() ?? "";

                // Validar CNPJ e CPF usando DocumentValidator
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
                    // Buscar todos os clientes e comparar CNPJ sem máscara
                    var allClients = await _context.Clients.Where(c => c.CNPJ != null).ToListAsync();
                    var existingCNPJ = allClients.FirstOrDefault(c => 
                        c.CNPJ != null && 
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjCleanNew);
                    
                    if (existingCNPJ != null)
                    {
                        // Se o CNPJ "duplicado" é do próprio cliente do usuário (ex.: edição sem id no form), redirecionar para edição
                        if (!isAdmin && existingCNPJ.UserId == userId)
                        {
                            return RedirectToAction(ActionNames.RegisterClient, new { id = existingCNPJ.Id });
                        }
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CnpjAlreadyRegisteredInSystem(client.CNPJ);
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CNPJ com máscara
                    client.CNPJ = $"{cnpjCleanNew.Substring(0, 2)}.{cnpjCleanNew.Substring(2, 3)}.{cnpjCleanNew.Substring(5, 3)}/{cnpjCleanNew.Substring(8, 4)}-{cnpjCleanNew.Substring(12, 2)}";
                }
                else
                {
                    client.CNPJ = null;
                }

                // Validar CPF se informado
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
                    // Buscar todos os clientes e comparar CPF sem máscara
                    var allClients = await _context.Clients.Where(c => c.CPF != null).ToListAsync();
                    var existingCPF = allClients.FirstOrDefault(c => 
                        c.CPF != null && 
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfCleanNew);
                    
                    if (existingCPF != null)
                    {
                        // Se o CPF "duplicado" é do próprio cliente do usuário (ex.: edição sem id no form), redirecionar para edição
                        if (!isAdmin && existingCPF.UserId == userId)
                        {
                            return RedirectToAction(ActionNames.RegisterClient, new { id = existingCPF.Id });
                        }
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.CpfAlreadyRegisteredInSystem(client.CPF);
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CPF com máscara
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

                // Telefone é obrigatório no cadastro de cliente
                if (string.IsNullOrWhiteSpace(client.ContactPhone))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PhoneRequired;
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }

                // Sempre usar o email do usuário logado
                client.ContactEmail = userEmail;
                client.UserId = userId;
                client.IsActive = true;

                // Remover máscaras do telefone e CEP antes de salvar (opcional - pode salvar com máscara também)
                if (!string.IsNullOrEmpty(client.ContactPhone))
                {
                    client.ContactPhone = client.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
                }
                if (!string.IsNullOrEmpty(client.ZipCode))
                {
                    client.ZipCode = client.ZipCode.Replace("-", "");
                }

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Sincronizar Nome Completo na tabela Users e atualizar a sessão (mesmo comportamento do Profile)
                var userForNewClient = await _context.Users.FindAsync(userId);
                if (userForNewClient != null && !string.IsNullOrWhiteSpace(client.ContactName))
                {
                    userForNewClient.FullName = client.ContactName;
                    await _context.SaveChangesAsync();
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
        public async Task<IActionResult> ChooseLocation(string? region = null, string? city = null, string? state = null, int page = 1, CancellationToken cancellationToken = default)
        {
            if (User.IsInRole(RoleNames.Client) && !User.IsInRole(Administrator))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistration;
                    return RedirectToAction(ActionNames.RegisterClient);
                }
                var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
                if (!hasDocument || string.IsNullOrWhiteSpace(client.ContactName) || string.IsNullOrWhiteSpace(client.State))
                {
                    TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationWithFieldsShort;
                    return RedirectToAction(ActionNames.RegisterClient);
                }
            }

            const int pageSize = 10;

            // Buscar todos os endereços fiscais disponíveis (mesmo critério de AvailableAddresses)
            var fullAddressList = await _coworkingService.GetAvailableAddressesAsync();

            // Regiões e estados para filtros (a partir da lista completa)
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

            // Aplicar filtros por região, cidade e estado (via Location do endereço)
            var filteredAddresses = fullAddressList.AsEnumerable();
            if (!string.IsNullOrEmpty(region))
                filteredAddresses = filteredAddresses.Where(a => a.Location != null && a.Location.Region == region);
            if (!string.IsNullOrEmpty(city))
                filteredAddresses = filteredAddresses.Where(a => (a.Location?.City ?? a.City) == city);
            if (!string.IsNullOrEmpty(state))
                filteredAddresses = filteredAddresses.Where(a => a.Location != null && a.Location.State == state);

            // Ordenar: Estado, Cidade, Endereço
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

            // IDs de endereços que o cliente já possui contrato (igual AvailableAddresses)
            var contractedAddressIds = new HashSet<int>();
            int? activeContractId = null;
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
                if (client != null)
                {
                    var ids = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id
                            && c.Status != ContractStatus.Cancelled
                            && c.Status != ContractStatus.Expired
                            && (c.Status == ContractStatus.Active
                                || c.Status == ContractStatus.Suspended
                                || c.Status == ContractStatus.PendingCancellation))
                        .Select(c => c.CoworkingAddressId)
                        .ToListAsync();
                    contractedAddressIds = new HashSet<int>(ids);
                    var activeId = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                        .OrderByDescending(c => c.StartDate)
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync();
                    if (activeId != 0) activeContractId = activeId;
                }
            }

            // Endereços com contrato em processo de pagamento: CoworkingAddressId -> PaymentId e status (para badge alinhado a MyPayments)
            var pendingPaymentByAddressId = new Dictionary<int, int>();
            var pendingPaymentStatusByAddressId = new Dictionary<int, PaymentStatus>();
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
                if (client != null)
                {
                    var pendingContracts = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id && c.Status == ContractStatus.PendingPayment)
                        .Include(c => c.Subscription)
                            .ThenInclude(s => s!.Payments)
                        .ToListAsync();
                    foreach (var c in pendingContracts)
                    {
                        var payment = c.Subscription?.Payments?.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
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

            // Foto principal de cada coworking (igual AvailableAddresses)
            var coworkingIds = pagedAddresses.Select(a => a.CoworkingId).Distinct().ToList();
            var mainPhotos = new Dictionary<int, CoworkingPhoto>();
            foreach (var coworkingId in coworkingIds)
            {
                var mainPhoto = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.CoworkingId == coworkingId && p.IsMain);
                if (mainPhoto == null)
                    mainPhoto = await _context.CoworkingPhotos
                        .Where(p => p.CoworkingId == coworkingId)
                        .OrderBy(p => p.DisplayOrder)
                        .ThenBy(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
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

        [HttpGet]
        public async Task<IActionResult> AvailableAddresses(int locationId, CancellationToken cancellationToken = default)
        {
            // Validar se o locationId é válido
            if (locationId <= 0)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidLocation;
                return RedirectToAction(ActionNames.ChooseLocation);
            }

            // Verificar se a localidade existe
            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.LocationNotFound;
                return RedirectToAction(ActionNames.ChooseLocation);
            }

            var addresses = await _coworkingService.GetAvailableAddressesAsync(locationId);
            
            // IDs de endereços que o cliente já possui contrato (ativo, suspenso, pendente de pagamento ou pendente de cancelamento)
            var contractedAddressIds = new HashSet<int>();
            int? activeContractId = null;
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
                if (client != null)
                {
                    var ids = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id
                            && c.Status != ContractStatus.Cancelled
                            && c.Status != ContractStatus.Expired
                            && (c.Status == ContractStatus.Active
                                || c.Status == ContractStatus.Suspended
                                || c.Status == ContractStatus.PendingCancellation))
                        .Select(c => c.CoworkingAddressId)
                        .ToListAsync();
                    contractedAddressIds = new HashSet<int>(ids);
                    // ID do contrato ativo/suspenso (para link "Contrato de Endereço Fiscal" -> detalhes do contrato atual)
                    var activeId = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                        .OrderByDescending(c => c.StartDate)
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync();
                    if (activeId != 0) activeContractId = activeId;
                }
            }
            ViewBag.ContractedAddressIds = contractedAddressIds;
            ViewBag.ActiveContractId = activeContractId;
            
            // Buscar foto principal de cada coworking
            // Se não houver foto principal, buscar a primeira foto disponível
            var coworkingIds = addresses.Select(a => a.CoworkingId).Distinct().ToList();
            var mainPhotos = new Dictionary<int, CoworkingPhoto>();
            
            foreach (var coworkingId in coworkingIds)
            {
                // Primeiro, tentar buscar a foto principal
                var mainPhoto = await _context.CoworkingPhotos
                    .FirstOrDefaultAsync(p => p.CoworkingId == coworkingId && p.IsMain);
                
                // Se não houver foto principal, buscar a primeira foto disponível
                if (mainPhoto == null)
                {
                    mainPhoto = await _context.CoworkingPhotos
                        .Where(p => p.CoworkingId == coworkingId)
                        .OrderBy(p => p.DisplayOrder)
                        .ThenBy(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
                }
                
                if (mainPhoto != null)
                {
                    mainPhotos[coworkingId] = mainPhoto;
                }
            }
            
            ViewBag.LocationId = locationId;
            ViewBag.Location = location;
            ViewBag.MainPhotos = mainPhotos;
            
            return View(addresses);
        }

        /// <summary>
        /// Página intermediária após login/cadastro: completa o cadastro se necessário e em seguida executa a contratação do endereço, redirecionando para pagamento.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CompleteContractAfterLogin(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth, new { returnUrl = Url.Action(ActionNames.CompleteContractAfterLogin, ControllerNames.Client, new { coworkingAddressId }) });
            }
            var userId = int.Parse(userIdStr);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            var returnUrlComplete = Url.Action(ActionNames.CompleteContractAfterLogin, ControllerNames.Client, new { coworkingAddressId });

            if (client == null)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForFiscalAddress;
                return RedirectToAction(ActionNames.RegisterClient, new { returnUrl = returnUrlComplete });
            }

            var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
            var missingRequired = !hasDocument || string.IsNullOrWhiteSpace(client.ContactName)
                || string.IsNullOrWhiteSpace(client.ContactPhone) || string.IsNullOrWhiteSpace(client.State);
            if (missingRequired)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForContract;
                return RedirectToAction(ActionNames.RegisterClient, new { id = client.Id, returnUrl = returnUrlComplete });
            }

            return RedirectToAction(ActionNames.SelectPaymentPlan, new { coworkingAddressId });
        }

        /// <summary>
        /// Página para escolher a forma de pagamento (PIX ou opções de cartão configuradas pelo parceiro) antes de criar o contrato.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SelectPaymentPlan(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth, new { returnUrl = Url.Action(ActionNames.SelectPaymentPlan, ControllerNames.Client, new { coworkingAddressId }) });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForFiscalAddress;
                return RedirectToAction(ActionNames.RegisterClient, new { returnUrl = Url.Action(ActionNames.SelectPaymentPlan, ControllerNames.Client, new { coworkingAddressId }) });
            }

            var hasDocument = !string.IsNullOrWhiteSpace(client.CPF) || !string.IsNullOrWhiteSpace(client.CNPJ);
            var missingRequired = !hasDocument || string.IsNullOrWhiteSpace(client.ContactName)
                || string.IsNullOrWhiteSpace(client.ContactPhone) || string.IsNullOrWhiteSpace(client.State);
            if (missingRequired)
            {
                TempData[TempDataKeys.Info] = InfoMessages.CompleteRegistrationForContract;
                return RedirectToAction(ActionNames.RegisterClient, new { id = client.Id, returnUrl = Url.Action(ActionNames.SelectPaymentPlan, ControllerNames.Client, new { coworkingAddressId }) });
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);

            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.ChooseLocation);
            }

            if (!address.Coworking.IsActive || address.AddressStatus != AddressStatus.Active || !address.IsAvailable || address.CurrentContracts >= address.MaxContracts)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotAvailableForContract;
                return RedirectToAction(ActionNames.ChooseLocation);
            }

            return View(address);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateContract(int coworkingAddressId, string? paymentOption, CancellationToken cancellationToken = default)
        {
            // Se não estiver autenticado, redirecionar para Login com returnUrl para continuar na escolha do plano e pagamento
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(ActionNames.Login, ControllerNames.Auth, new { returnUrl = Url.Action(ActionNames.SelectPaymentPlan, ControllerNames.Client, new { coworkingAddressId }) });
            }

            var isAjax = string.Equals(Request.Headers[HttpHeaderNames.XRequestedWith], HttpHeaderNames.XRequestedWithValue, StringComparison.OrdinalIgnoreCase);
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    var msg = "Cliente não encontrado. Complete seu cadastro primeiro.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.RegisterClient) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.RegisterClient);
                }

                var addressEntity = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);

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

                if (addressEntity.CurrentContracts >= addressEntity.MaxContracts)
                {
                    var msg = "Este endereço atingiu o limite máximo de contratos.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }

                // Verificar se o cliente já tem um contrato ativo ou pendente de pagamento para este endereço
                var existingContractForAddress = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .Where(c => c.ClientId == client.Id 
                        && c.CoworkingAddressId == coworkingAddressId 
                        && c.Status != ContractStatus.Cancelled
                        && (c.Status == ContractStatus.Active 
                            || c.Status == ContractStatus.Suspended 
                            || c.Status == ContractStatus.PendingPayment))
                    .FirstOrDefaultAsync();

                if (existingContractForAddress != null)
                {
                    var statusMsg = existingContractForAddress.Status.ToDisplayStringLower();
                    var msg = $"Você já possui um contrato {statusMsg} para este endereço. Número do contrato: {existingContractForAddress.ContractNumber}";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }

                // Cliente já possui contrato ativo em outro endereço — não pode adicionar outro sem cancelar/encerrar o atual
                if (await _clientMenuService.ClientHasActiveContractAsync(userId))
                {
                    var activeContract = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.ClientId == client.Id && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                        .OrderByDescending(c => c.StartDate)
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync();
                    var contractDetailsUrl = activeContract != 0
                        ? Url.Action(ActionNames.ContractDetails, new { id = activeContract })
                        : Url.Action(ActionNames.MyContracts);
                    var msg = "Você já possui um endereço fiscal ativo. Para adicionar outro, cancele ou encerre o contrato atual no menu <strong><a href=\"" + contractDetailsUrl + "\" class=\"alert-link text-white text-decoration-underline\">Contrato de Endereço Fiscal</a></strong>.";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[TempDataKeys.Info] = InfoMessages.Generic(msg);
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }

                // Verificar se o cliente já tem um contrato ativo na mesma localidade (exceto cancelados e pendentes de pagamento)
                var existingContractInLocation = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                        .ThenInclude(a => a.Location)
                    .Where(c => c.ClientId == client.Id 
                        && c.CoworkingAddress.LocationId == addressEntity.LocationId 
                        && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                    .FirstOrDefaultAsync();

                if (existingContractInLocation != null)
                {
                    var locationInfo = existingContractInLocation.CoworkingAddress?.Location != null 
                        ? $"{existingContractInLocation.CoworkingAddress.Location.City}/{existingContractInLocation.CoworkingAddress.Location.State}"
                        : $"{addressEntity.City}/{addressEntity.State}";
                    var statusLower = existingContractInLocation.Status.ToDisplayStringLower();
                    var msg = $"Você já possui um contrato {statusLower} nesta localidade ({locationInfo}). Número do contrato: {existingContractInLocation.ContractNumber}";
                    if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId }) });
                    TempData[ErrorMessages.TempDataKey] = msg;
                    return RedirectToAction(ActionNames.AvailableAddresses, new { locationId = addressEntity.LocationId });
                }

                // Definir valor mensal e método de pagamento conforme opção escolhida (SelectPaymentPlan)
                decimal monthlyValueForContract;
                PaymentMethod? initialPaymentMethod = null;
                var option = (paymentOption ?? "PIX").Trim();
                if (option == "Card1x" && addressEntity.CardPrice1x.HasValue && addressEntity.CardPrice1x.Value > 0)
                {
                    monthlyValueForContract = addressEntity.CardPrice1x.Value;
                    initialPaymentMethod = PaymentMethod.CreditCard;
                }
                else if (option == "CardMonthly" && addressEntity.CardPriceMonthly.HasValue && addressEntity.CardPriceMonthly.Value > 0)
                {
                    monthlyValueForContract = addressEntity.CardPriceMonthly.Value;
                    initialPaymentMethod = PaymentMethod.CreditCard;
                }
                else if (option == "Card12x" && addressEntity.CardPrice12x.HasValue && addressEntity.CardPrice12x.Value > 0)
                {
                    monthlyValueForContract = addressEntity.CardPrice12x.Value;
                    initialPaymentMethod = PaymentMethod.CreditCard;
                }
                else
                {
                    monthlyValueForContract = addressEntity.BasePrice;
                    initialPaymentMethod = null; // PIX
                }

                var contract = await _contractService.CreateContractAsync(
                    client.Id,
                    coworkingAddressId,
                    monthlyValueForContract,
                    assistedSaleAdminId: null,
                    initialPaymentMethod
                );

                // O ContractService já cria a subscription e o primeiro pagamento
                // Redirecionar para a página de pagamento
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.ContractId == contract.Id);
                
                if (subscription != null)
                {
                    var payment = await _context.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync(p => p.SubscriptionId == subscription.Id);
                    
                    if (payment != null)
                    {
                        // Registrar no histórico que o contrato foi criado
                        var history = new PaymentHistory
                        {
                            PaymentId = payment.Id,
                            Action = PaymentHistoryActions.ContractCreated,
                            ActionBy = userId, // ID do cliente que criou o contrato
                            ActionAt = DateTimeHelper.GetBrazilianTime()
                        };
                        _context.PaymentHistories.Add(history);
                        await _context.SaveChangesAsync();
                        
                        // Validar dados do cliente necessários para gerar PIX/Boleto antes de redirecionar para PaymentDetails.
                        // Evita o fluxo "Contrato criado!" -> abre PaymentDetails -> erro "Dados do cliente inválidos" -> Profile.
                        var clientForPayment = await _context.Clients
                            .Include(c => c.User)
                            .FirstOrDefaultAsync(c => c.Id == client.Id);
                        if (clientForPayment != null)
                        {
                            var clientName = clientForPayment.RazaoSocial ?? clientForPayment.NomeFantasia ?? clientForPayment.ContactName ?? clientForPayment.User?.FullName;
                            var clientEmail = clientForPayment.ContactEmail ?? clientForPayment.User?.Email;
                            var clientTaxId = (clientForPayment.CNPJ ?? clientForPayment.CPF ?? "").Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                            if (clientTaxId == "00000000000000" || clientTaxId == "00000000000" || clientTaxId == "99999999999" || clientTaxId == "99999999999999" ||
                                clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                                clientTaxId = "";

                            if (string.IsNullOrWhiteSpace(clientName))
                            {
                                var msg = "Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome Completo. Seu contrato já foi criado.";
                                if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.Profile) });
                                TempData[ErrorMessages.TempDataKey] = msg;
                                return RedirectToAction(ActionNames.Profile);
                            }
                            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
                            {
                                var msg = "Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido. Seu contrato já foi criado.";
                                if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.Profile) });
                                TempData[ErrorMessages.TempDataKey] = msg;
                                return RedirectToAction(ActionNames.Profile);
                            }
                            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
                            {
                                var msg = "CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido. Seu contrato já foi criado.";
                                if (isAjax) return Json(new { success = false, message = msg, redirectUrl = Url.Action(ActionNames.Profile) });
                                TempData[ErrorMessages.TempDataKey] = msg;
                                return RedirectToAction(ActionNames.Profile);
                            }
                        }

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
                // Se houver qualquer erro, o contrato NÃO foi criado (transação foi revertida)
                var errMsg = $"Erro ao criar contrato: {ex.Message}";
                var addressEntity = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);
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

            // Sincronizar exibição com status do pagamento: se o contrato está "Aguardando Pagamento" mas há pagamento "Aguardando Validação", exibir "Aguardando Validação" na visão do cliente
            var hasPaymentAwaitingValidation = false;
            int? paymentAwaitingValidationId = null;
            if (contract.Status == ContractStatus.PendingPayment)
            {
                var subscriptionIds = await _context.Subscriptions
                    .Where(s => s.ContractId == id)
                    .Select(s => s.Id)
                    .ToListAsync();
                if (subscriptionIds.Any())
                {
                    var paymentAwaiting = await _context.Payments
                        .Where(p => p.SubscriptionId.HasValue && subscriptionIds.Contains(p.SubscriptionId.Value) && p.Status == PaymentStatus.AwaitingValidation)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
                    if (paymentAwaiting != null)
                    {
                        hasPaymentAwaitingValidation = true;
                        paymentAwaitingValidationId = paymentAwaiting.Id;
                    }
                }
            }
            ViewBag.HasPaymentAwaitingValidation = hasPaymentAwaitingValidation;
            ViewBag.PaymentAwaitingValidationId = paymentAwaitingValidationId;

            // Declarações de endereço (mesmo que na tela do parceiro)
            var declarations = await _context.AddressDeclarations
                .Where(d => d.ContractId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
            ViewBag.Declarations = declarations;

            // Histórico do contrato (mesmo padrão do Parceiro/ContractDetails - Timeline)
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
                // ContractCreated não entra na timeline para não duplicar "Contrato Criado". "Pagamento Reprovado" só quando Action == "Rejected".
                if (historiesByPaymentId.ContainsKey(payment.Id))
                {
                    foreach (var history in historiesByPaymentId[payment.Id].OrderBy(h => h.ActionAt))
                    {
                        if (history.Action == PaymentHistoryActions.ContractCreated)
                            continue;

                        string title, description, icon, color;
                        if (history.Action == PaymentHistoryActions.Approved)
                        {
                            title = "Pagamento Aprovado";
                            description = $"Aprovado por {history.ActionByUser?.FullName ?? "Admin"}";
                            icon = "bi-check-circle";
                            color = "success";
                        }
                        else if (history.Action == PaymentHistoryActions.Cancelled)
                        {
                            title = "Pagamento Cancelado";
                            description = history.RejectionReason ?? "Cancelado";
                            icon = "bi-x-circle";
                            color = "secondary";
                        }
                        else if (history.Action == PaymentHistoryActions.ReceiptUploaded)
                        {
                            title = "Comprovante enviado";
                            description = "Cliente enviou o comprovante de pagamento. Aguardando validação.";
                            icon = "bi-file-earmark-arrow-up";
                            color = BootstrapBadgeCss.Info;
                        }
                        else if (history.Action == PaymentHistoryActions.Rejected)
                        {
                            title = "Pagamento Reprovado";
                            description = $"Reprovado por {history.ActionByUser?.FullName ?? "Admin"}: {history.RejectionReason}";
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

            timeline = timeline.OrderByDescending(t => ((dynamic)t).Date).ToList<object>();
            var timelineTotal = timeline.Count;
            var timelineTotalPages = timelinePageSize > 0 ? (int)Math.Ceiling(timelineTotal / (double)timelinePageSize) : 1;
            if (timelinePage < 1) timelinePage = 1;
            if (timelinePage > timelineTotalPages) timelinePage = Math.Max(1, timelineTotalPages);
            var timelinePaged = timeline
                .Skip((timelinePage - 1) * timelinePageSize)
                .Take(timelinePageSize)
                .ToList<object>();
            ViewBag.Timeline = timelinePaged;
            ViewBag.TimelineTotalItems = timelineTotal;
            ViewBag.TimelineCurrentPage = timelinePage;
            ViewBag.TimelineTotalPages = timelineTotalPages;
            ViewBag.TimelinePageSize = timelinePageSize;

            // Exibir o contrato em qualquer status (incl. PendingCancellation, Cancelled, Expired) para o dono visualizar detalhes e documentos
            return View(contract);
        }

        [HttpGet]
        public async Task<IActionResult> MyContracts(string? search = null, string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            var query = _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(c => c.ClientId == client.Id)
                .AsQueryable();

            // Filtro de busca (número do contrato ou endereço)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    c.ContractNumber.Contains(search) || 
                    c.CoworkingAddress.Address.Contains(search) ||
                    c.CoworkingAddress.Location.City.Contains(search));
            }

            // Filtro por status
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ContractStatus>(status, out var contractStatus))
                {
                    query = query.Where(c => c.Status == contractStatus);
                }
            }

            // Ordenação
            query = query.OrderByDescending(c => c.StartDate);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var contracts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Sincronizar status dos contratos baseado nos pagamentos
            // Verificar se há pagamentos pagos para cada contrato e atualizar status se necessário
            var pendingPaymentContracts = contracts.Where(c => c.Status == ContractStatus.PendingPayment).ToList();
            
            if (pendingPaymentContracts.Any())
            {
                var contractIds = pendingPaymentContracts.Select(c => c.Id).ToList();
                
                // Buscar todas as subscriptions dos contratos pendentes em uma única query
                var subscriptions = await _context.Subscriptions
                    .Where(s => contractIds.Contains(s.ContractId))
                    .ToListAsync();
                
                var subscriptionIds = subscriptions.Select(s => s.Id).ToList();
                
                if (subscriptionIds.Any())
                {
                    // Buscar todos os pagamentos pagos para essas subscriptions em uma única query
                    var paidPayments = await _context.Payments
                        .Where(p => subscriptionIds.Contains(p.SubscriptionId ?? 0) && p.Status == PaymentStatus.Paid)
                        .GroupBy(p => p.SubscriptionId)
                        .Select(g => new { 
                            SubscriptionId = g.Key, 
                            FirstPaidAt = g.Min(p => p.PaidAt ?? p.CreatedAt) 
                        })
                        .ToListAsync();
                    
                    // Criar dicionários para busca rápida
                    var subscriptionToContract = subscriptions.ToDictionary(s => s.Id, s => s.ContractId);
                    var subscriptionToFirstPaidDate = paidPayments
                        .Where(p => p.SubscriptionId.HasValue)
                        .ToDictionary(p => p.SubscriptionId.Value, p => p.FirstPaidAt);
                    
                    var contractsToUpdate = new List<Contract>();
                    
                    foreach (var subscription in subscriptions)
                    {
                        if (subscriptionToFirstPaidDate.ContainsKey(subscription.Id))
                        {
                            var contract = pendingPaymentContracts.FirstOrDefault(c => c.Id == subscription.ContractId);
                            if (contract != null)
                            {
                                // Atualizar status do contrato para Active
                                contract.Status = ContractStatus.Active;
                                
                                // Se não tiver ExpiryDate, definir baseado na data do primeiro pagamento
                                if (!contract.ExpiryDate.HasValue)
                                {
                                    var firstPaidDate = subscriptionToFirstPaidDate[subscription.Id];
                                    contract.ExpiryDate = firstPaidDate.AddYears(1);
                                }
                                
                                contract.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                                contractsToUpdate.Add(contract);
                            }
                        }
                    }
                    
                    // Salvar todas as alterações de uma vez
                    if (contractsToUpdate.Any())
                    {
                        // Marcar explicitamente como modificado para garantir que o EF detecte as mudanças
                        foreach (var contract in contractsToUpdate)
                        {
                            _context.Entry(contract).Property(c => c.Status).IsModified = true;
                            _context.Entry(contract).Property(c => c.UpdatedAt).IsModified = true;
                            if (contract.ExpiryDate.HasValue)
                            {
                                _context.Entry(contract).Property(c => c.ExpiryDate).IsModified = true;
                            }
                        }
                        
                        await _context.SaveChangesAsync();
                    }
                }
            }

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.HasActiveContract = await _clientMenuService.ClientHasActiveContractAsync(userId);

            return View(contracts);
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

                // Atualizar contrato com solicitação de cancelamento
                contract.Status = ContractStatus.PendingCancellation;
                contract.CancellationRequestedAt = DateTimeHelper.GetBrazilianTime();
                contract.CancellationRequestReason = reason;
                contract.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                _context.Contracts.Update(contract);
                await _context.SaveChangesAsync();

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
        public async Task<IActionResult> DownloadDocument(int documentId, CancellationToken cancellationToken = default)
        {
            var fileBytes = await _documentService.GetDocumentFileAsync(documentId);
            if (fileBytes == null)
            {
                return NotFound();
            }

            var document = await _context.Documents.FindAsync(documentId);
            return File(fileBytes, "application/pdf", document?.FileName ?? "document.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> MyPayments(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
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
        public async Task<IActionResult> PaymentDetails(int id, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Coworking)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (payment == null)
            {
                return NotFound();
            }

            // Verificar se o pagamento pertence ao cliente logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            bool isAuthorized = false;
            
            // Verificar se é pagamento de subscription (contrato)
            if (payment.SubscriptionId.HasValue && payment.Subscription != null)
            {
                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract != null && contract.ClientId == client.Id)
                {
                    isAuthorized = true;
                }
            }
            // Verificar se é pagamento de reserva
            else if (payment.ReservationId.HasValue)
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == payment.ReservationId.Value);
                
                if (reservation != null && reservation.ClientId == client.Id)
                {
                    isAuthorized = true;
                }
            }
            
            if (!isAuthorized && !User.IsInRole(Administrator))
            {
                return Forbid();
            }
            
            // Verificar se o Asaas está configurado antes de tentar gerar códigos
            var asaasApiKey = await _systemConfig.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
            var isAsaasConfigured = !string.IsNullOrEmpty(asaasApiKey);

            if (!isAsaasConfigured)
            {
                TempData[TempDataKeys.Warning] = WarningMessages.PaymentSystemNotConfiguredConfigureAsaas;
            }

            // Usar o serviço para gerar códigos se necessário (apenas se Asaas estiver configurado)
            if (payment.Status == PaymentStatus.Pending && isAsaasConfigured)
            {
                try
                {
                    if (payment.Method == PaymentMethod.PIX && string.IsNullOrEmpty(payment.PixCode))
                    {
                        await _paymentService.GeneratePixCodeAsync(payment.Id);
                        // Recarregar o pagamento
                        payment = await _context.Payments
                            .Include(p => p.Subscription)
                                .ThenInclude(s => s.Contract)
                                    .ThenInclude(c => c.CoworkingAddress)
                                        .ThenInclude(a => a.Coworking)
                            .Include(p => p.Reservation)
                                .ThenInclude(r => r.CoworkingAddress)
                                    .ThenInclude(a => a.Coworking)
                            .FirstOrDefaultAsync(p => p.Id == id);
                    }
                    else if (payment.Method == PaymentMethod.BankTransfer && string.IsNullOrEmpty(payment.BoletoCode))
                    {
                        await _paymentService.GenerateBoletoCodeAsync(payment.Id);
                        // Recarregar o pagamento
                        payment = await _context.Payments
                            .Include(p => p.Subscription)
                                .ThenInclude(s => s.Contract)
                                    .ThenInclude(c => c.CoworkingAddress)
                                        .ThenInclude(a => a.Coworking)
                            .Include(p => p.Reservation)
                                .ThenInclude(r => r.CoworkingAddress)
                                    .ThenInclude(a => a.Coworking)
                            .FirstOrDefaultAsync(p => p.Id == id);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Asaas") || ex.Message.Contains("API Key"))
                {
                    // Asaas não configurado ou chave inválida - mostrar mensagem amigável mas permitir visualizar o pagamento
                    TempData[TempDataKeys.Warning] = WarningMessages.PaymentSystemNotConfiguredConfigureAsaasShort;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("não informado") || ex.Message.Contains("inválido") || ex.Message.Contains("Complete seu cadastro"))
                {
                    // Erro na API de pagamento (dados do cliente ou Asaas): redirecionar para Profile.
                    // Manter TempData[SuccessMessage.TempDataKey] para exibir ContractCreated quando o contrato foi criado.
                    TempData[ErrorMessages.TempDataKey] = ex.Message;
                    return RedirectToAction(ActionNames.Profile);
                }
                catch (InvalidOperationException ex)
                {
                    // Outros erros de operação inválida
                    TempData[ErrorMessages.TempDataKey] = ex.Message;
                }
            }

            // Gerar código PIX ou Boleto se necessário
            if (payment.Status == PaymentStatus.Pending)
            {
                try
                {
                    if (payment.Method == PaymentMethod.PIX && string.IsNullOrEmpty(payment.PixCode))
                    {
                        await _paymentService.GeneratePixCodeAsync(payment.Id);
                        // Recarregar o pagamento para obter o código PIX gerado
                        payment = await _paymentService.GetPaymentByIdAsync(id);
                    }
                    else if (payment.Method == PaymentMethod.BankTransfer && string.IsNullOrEmpty(payment.BoletoCode))
                    {
                        await _paymentService.GenerateBoletoCodeAsync(payment.Id);
                        // Recarregar o pagamento para obter o código Boleto gerado
                        payment = await _paymentService.GetPaymentByIdAsync(id);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Asaas") || ex.Message.Contains("API Key"))
                {
                    // Asaas não configurado - mostrar mensagem amigável mas permitir visualizar o pagamento
                    TempData[TempDataKeys.Warning] = WarningMessages.PaymentSystemNotConfiguredConfigureAsaasShort;
                    // Continuar para mostrar a página mesmo sem o código gerado
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("não informado") || ex.Message.Contains("inválido") || ex.Message.Contains("Complete seu cadastro"))
                {
                    // Erro na API de pagamento: redirecionar para Profile. Manter Success se o contrato foi criado.
                    TempData[ErrorMessages.TempDataKey] = ex.Message;
                    return RedirectToAction(ActionNames.Profile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao gerar código PIX/Boleto automaticamente no PaymentDetails. PaymentId: {PaymentId}", id);
                    TempData[TempDataKeys.Warning] = WarningMessages.CouldNotGeneratePaymentCodePixOrCheckAsaas;
                    // Continuar para mostrar a página mesmo sem o código gerado
                }
            }

            // Buscar cartões cadastrados do cliente se o método for cartão
            if (payment.Method == PaymentMethod.CreditCard && client != null)
            {
                var creditCards = await _context.CreditCards
                    .Where(c => c.ClientId == client.Id && c.IsActive)
                    .OrderByDescending(c => c.IsDefault)
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync();
                ViewBag.CreditCards = creditCards;
            }

            // Rótulo do plano de cartão (À vista 1x, 12x, Mensal) para exibir em PaymentDetails
            string? creditCardPlanLabel = null;
            if (payment.Method == PaymentMethod.CreditCard && payment.Subscription?.Contract?.CoworkingAddress != null)
            {
                var address = payment.Subscription.Contract.CoworkingAddress;
                var amount = payment.Amount;
                const decimal tolerance = 0.01m;
                if (address.CardPrice1x.HasValue && Math.Abs(amount - address.CardPrice1x.Value) <= tolerance)
                    creditCardPlanLabel = "À vista (1x)";
                else if (address.CardPrice12x.HasValue && Math.Abs(amount - address.CardPrice12x.Value) <= tolerance)
                    creditCardPlanLabel = $"12x de R$ {address.CardPrice12x.Value:N2}";
                else if (address.CardPriceMonthly.HasValue && Math.Abs(amount - address.CardPriceMonthly.Value) <= tolerance)
                    creditCardPlanLabel = "Mensal";
            }
            ViewBag.CreditCardPlanLabel = creditCardPlanLabel;

            // Calcular valor com percentual de cartão de crédito se aplicável
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

            // Buscar histórico do pagamento com paginação
            int historyPage = 1;
            int.TryParse(Request.Query["historyPage"], out historyPage);
            if (historyPage < 1) historyPage = 1;
            
            int historyPageSize = 5; // 5 itens por página
            
            var totalHistoryItems = await _context.PaymentHistories
                .Where(h => h.PaymentId == payment.Id)
                .CountAsync();
            
            var totalHistoryPages = (int)Math.Ceiling(totalHistoryItems / (double)historyPageSize);
            if (historyPage > totalHistoryPages && totalHistoryPages > 0) historyPage = totalHistoryPages;
            
            var paymentHistory = await _context.PaymentHistories
                .Include(h => h.ActionByUser)
                .Where(h => h.PaymentId == payment.Id)
                .OrderByDescending(h => h.ActionAt) // Sempre ordenar por última interação (mais recente primeiro)
                .Skip((historyPage - 1) * historyPageSize)
                .Take(historyPageSize)
                .ToListAsync();
            
            ViewBag.PaymentHistory = paymentHistory;
            ViewBag.HistoryCurrentPage = historyPage;
            ViewBag.HistoryTotalPages = totalHistoryPages;
            ViewBag.HistoryTotalItems = totalHistoryItems;

            // Data/hora de envio do comprovante (para exibir no modal "Comprovante Enviado")
            var receiptUploadedEntry = await _context.PaymentHistories
                .Where(h => h.PaymentId == payment.Id && h.Action == PaymentHistoryActions.ReceiptUploaded)
                .OrderByDescending(h => h.ActionAt)
                .FirstOrDefaultAsync();
            ViewBag.ReceiptUploadedAt = receiptUploadedEntry?.ActionAt;

            // Quando o pagamento está cancelado, passar o motivo do contrato para exibir na tela (ex.: cancelamento automático por outro contrato aprovado)
            if (payment.Status == PaymentStatus.Cancelled && payment.Subscription?.Contract != null && !string.IsNullOrEmpty(payment.Subscription.Contract.CancellationReason))
            {
                ViewBag.ContractCancellationReason = payment.Subscription.Contract.CancellationReason;
            }

            return View(payment);
        }

        /// <summary>
        /// Endpoint para polling de status do pagamento (usado pelo JavaScript para atualização automática)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaymentStatus(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _context.Payments.FindAsync(paymentId);
                if (payment == null)
                {
                    return NotFound(new { error = ErrorJson.PaymentNotFoundShort });
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    return Unauthorized(new { error = ErrorJson.ClientNotFoundShort });
                }

                // Verificar autorização
                bool isAuthorized = false;
                
                if (payment.SubscriptionId.HasValue)
                {
                    var subscription = await _context.Subscriptions
                        .Include(s => s.Contract)
                        .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value);
                    
                    if (subscription?.Contract?.ClientId == client.Id)
                    {
                        isAuthorized = true;
                    }
                }
                else if (payment.ReservationId.HasValue)
                {
                    var reservation = await _context.Reservations
                        .FirstOrDefaultAsync(r => r.Id == payment.ReservationId.Value);
                    
                    if (reservation?.ClientId == client.Id)
                    {
                        isAuthorized = true;
                    }
                }

                if (!isAuthorized && !User.IsInRole(Administrator))
                {
                    return Forbid();
                }

                return Ok(new 
                { 
                    paymentId = payment.Id,
                    status = payment.Status.ToString(),
                    paidAt = payment.PaidAt,
                    updatedAt = payment.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentMethod(int paymentId, string method, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole(Administrator))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyPendingPaymentsCanChangeMethod;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Converter string para enum
                if (!Enum.TryParse<PaymentMethod>(method, out var paymentMethod))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidPaymentMethod;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                if (paymentMethod == PaymentMethod.BankTransfer)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.BoletoNoLongerAvailable;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                await _paymentService.UpdatePaymentMethodAsync(paymentId, paymentMethod);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PaymentMethodUpdated;

                // Requisição AJAX: retornar 200 para o JS atualizar as seções (card do cartão + botão "Pagar com cartão")
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
        public async Task<IActionResult> GeneratePixCode(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole(Administrator))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OnlyPendingPaymentsCanGeneratePix;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                if (payment.Method != PaymentMethod.PIX)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ThisPaymentIsNotPix;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Gerar código PIX
                await _paymentService.GeneratePixCodeAsync(paymentId);
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.PixCodeGenerated;
                
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Asaas") || ex.Message.Contains("API Key"))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentSystemNotConfigured;
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
        public async Task<IActionResult> ConfirmPayment(int paymentId, IFormFile? receiptFile)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentNotFound;
                    return RedirectToAction(ActionNames.MyPayments);
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction(ActionNames.RegisterClient);
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole(Administrator))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PaymentAlreadyProcessed;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Validar se o comprovante foi enviado
                if (receiptFile == null || receiptFile.Length == 0)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SendPaymentReceipt;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Validar tipo de arquivo (apenas imagens e PDFs)
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var fileExtension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidFileFormatImagesOrPdf;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Salvar comprovante usando o storage service
                var storageFactory = HttpContext.RequestServices.GetRequiredService<StorageServiceFactory>();
                var storageService = await storageFactory.GetStorageServiceAsync();
                
                using var stream = new MemoryStream();
                await receiptFile.CopyToAsync(stream);
                var fileBytes = stream.ToArray();
                var fileName = $"receipt_{paymentId}_{DateTimeHelper.GetBrazilianTime():yyyyMMddHHmmss}{fileExtension}";
                var receiptPath = await storageService.SaveFileAsync(fileBytes, fileName, $"payments/{paymentId}/receipts");

                // Atualizar pagamento para aguardando validação
                payment.Status = PaymentStatus.AwaitingValidation;
                payment.ReceiptFilePath = receiptPath;
                payment.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                // Registrar no histórico que o comprovante foi enviado
                var history = new PaymentHistory
                {
                    PaymentId = paymentId,
                    Action = PaymentHistoryActions.ReceiptUploaded,
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.PaymentHistories.Add(history);

                await _context.SaveChangesAsync();

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

            // Verificar se o pagamento pertence ao cliente logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
            
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

        /// <summary>
        /// Exibe o comprovante de pagamento inline (para visualização no modal). Mesmo critério do Admin/ViewReceipt.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewReceipt(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }

            // Verificar se o pagamento pertence ao cliente logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            bool isAuthorized = false;
            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value);
                if (subscription != null)
                {
                    var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == subscription.ContractId);
                    if (contract != null && contract.ClientId == client.Id) isAuthorized = true;
                }
            }
            else if (payment.ReservationId.HasValue)
            {
                var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == payment.ReservationId.Value);
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

            // Retornar inline para exibição no modal (como Admin/ViewReceipt)
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
            string expiryMonth, string expiryYear, string cvv, string? isDefault)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Remover espaços e caracteres não numéricos do número do cartão
                var cleanCardNumber = cardNumber.Replace(" ", "").Replace("-", "");
                if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidCardNumber;
                    return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
                }

                // Obter últimos 4 dígitos
                var lastFourDigits = cleanCardNumber.Substring(cleanCardNumber.Length - 4);

                // Detectar bandeira do cartão (simplificado)
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

                // Se for definir como padrão, remover padrão dos outros cartões
                var setAsDefault = StringBooleanValue.IsTrue(isDefault);
                if (setAsDefault)
                {
                    var defaultCards = await _context.CreditCards
                        .Where(c => c.ClientId == client.Id && c.IsDefault)
                        .ToListAsync();
                    foreach (var card in defaultCards)
                    {
                        card.IsDefault = false;
                    }
                }

                var creditCard = new CreditCard
                {
                    ClientId = client.Id,
                    LastFourDigits = lastFourDigits,
                    CardHolderName = cardHolderName.ToUpper(),
                    ExpiryMonth = expiryMonth,
                    ExpiryYear = expiryYear,
                    Brand = brand,
                    IsDefault = setAsDefault,
                    IsActive = true
                };

                _context.CreditCards.Add(creditCard);
                await _context.SaveChangesAsync();

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
        public async Task<IActionResult> RemoveCreditCard(int paymentId, int cardId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            var card = await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.ClientId == client.Id);
            if (card == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CreditCardNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            _context.CreditCards.Remove(card);
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardRemoved;
            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultCreditCard(int paymentId, int cardId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            var card = await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.ClientId == client.Id);
            if (card == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.CreditCardNotFound;
                return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
            }
            var defaultCards = await _context.CreditCards
                .Where(c => c.ClientId == client.Id && c.IsDefault)
                .ToListAsync();
            foreach (var c in defaultCards)
                c.IsDefault = false;
            card.IsDefault = true;
            await _context.SaveChangesAsync();
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.CardSetAsDefault;
            return RedirectToAction(ActionNames.PaymentDetails, new { id = paymentId });
        }

        /// <summary>Processa o pagamento do endereço fiscal (subscription) com cartão de crédito, enviando ao Asaas.</summary>
        [HttpPost]
        public async Task<IActionResult> ProcessPaymentWithCard([FromBody] ProcessPaymentWithCardRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null || request.Card == null)
                {
                    return Json(new { success = false, error = ErrorJson.CardDataRequired });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return Json(new { success = false, error = ErrorJson.ClientNotFound });
                }

                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                    .FirstOrDefaultAsync(p => p.Id == request.PaymentId);
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

                var card = new Application.DTOs.PagBank.PagBankCard
                {
                    Number = number,
                    ExpirationDate = expDate,
                    SecurityCode = cvv,
                    Holder = new Application.DTOs.PagBank.PagBankCardHolder { Name = holderName.ToUpper() },
                    Store = false
                };

                var installments = request.Installments.HasValue && request.Installments.Value >= 1 && request.Installments.Value <= 12
                    ? request.Installments.Value
                    : (int?)1;

                var result = await _paymentService.ProcessPaymentWithCreditCardAsync(payment.Id, card, installments);

                if (result.Status == PaymentStatus.Paid)
                {
                    return Json(new { success = true, message = JsonMessage.PagamentoAprovadoContratoAtivado });
                }
                if (result.Status == PaymentStatus.Failed)
                {
                    return Json(new { success = false, error = ErrorJson.PaymentDeclinedTryAnotherCard });
                }

                return Json(new { success = true, message = JsonMessage.PagamentoSendoProcessadoAtualizePagina });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pagamento com cartão. PaymentId: {PaymentId}", request?.PaymentId);
                return Json(new { success = false, error = ErrorJson.ErrorProcessingPayment });
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
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (client == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFoundCompleteRegistration;
                return RedirectToAction(ActionNames.RegisterClient);
            }

            // Buscar todos os contratos ativos do cliente (para o dropdown de filtros)
            var allContracts = await _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Where(c => c.ClientId == client.Id && c.Status == ContractStatus.Active)
                .OrderByDescending(c => c.StartDate) // Mais recentes primeiro
                .ToListAsync();

            // Buscar todas as declarações do cliente com os contratos incluídos
            var allDeclarations = await _context.AddressDeclarations
                .Include(d => d.Contract)
                .Where(d => allContracts.Select(c => c.Id).Contains(d.ContractId))
                .ToListAsync();

            // Criar dicionário de contratos para busca rápida
            var contractsDict = allContracts.ToDictionary(c => c.Id, c => c);

            // Verificar quais contratos têm pagamentos pagos
            var contractsWithPaidPayments = await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => allContracts.Select(c => c.Id).Contains(p.Subscription.ContractId) 
                    && p.Status == PaymentStatus.Paid)
                .Select(p => p.Subscription.ContractId)
                .Distinct()
                .ToListAsync();

            ViewBag.ContractsWithPaidPayments = contractsWithPaidPayments;

            // Aplicar filtros
            var filteredDeclarations = allDeclarations.AsQueryable();

            // Filtro de busca (número da declaração ou número do contrato)
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                filteredDeclarations = filteredDeclarations.Where(d => 
                    d.DeclarationNumber.ToLower().Contains(searchLower) ||
                    (contractsDict.ContainsKey(d.ContractId) && contractsDict[d.ContractId].ContractNumber.ToLower().Contains(searchLower)));
            }

            // Filtro por contrato
            if (contractId.HasValue && contractId.Value > 0)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.ContractId == contractId.Value);
            }

            // Filtro por status
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

            // Filtro por data de geração (de)
            if (dateFrom.HasValue)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.CreatedAt >= dateFrom.Value);
            }

            // Filtro por data de geração (até)
            if (dateTo.HasValue)
            {
                filteredDeclarations = filteredDeclarations.Where(d => d.CreatedAt <= dateTo.Value.AddDays(1).AddSeconds(-1));
            }

            var declarationsList = filteredDeclarations.ToList();

            // Agrupar declarações por contrato para exibição
            var declarationsByContract = new Dictionary<int, List<AddressDeclaration>>();
            foreach (var contract in allContracts)
            {
                var contractDeclarations = declarationsList.Where(d => d.ContractId == contract.Id).ToList();
                if (contractDeclarations.Any() || !contractId.HasValue)
                {
                    declarationsByContract[contract.Id] = contractDeclarations;
                }
            }

            // Se houver filtro de contrato, mostrar apenas esse contrato
            var contractsToDisplay = allContracts;
            if (contractId.HasValue)
            {
                contractsToDisplay = allContracts.Where(c => c.Id == contractId.Value).ToList();
            }

            // Paginação dos contratos
            var totalContracts = contractsToDisplay.Count;
            var totalPages = totalContracts > 0 ? (int)Math.Ceiling(totalContracts / (double)pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));
            
            var pagedContracts = contractsToDisplay
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Contracts = pagedContracts;
            ViewBag.AllContracts = allContracts; // Para o dropdown de filtros
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
            ViewBag.HasMultipleContracts = allContracts.Count > 1; // Para determinar se deve usar accordion

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDeclaration(int contractId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.Declarations);
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientId == client.Id);

                if (contract == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ContractNotFoundOrNotYours;
                    return RedirectToAction(ActionNames.Declarations);
                }

                var declaration = await _addressDeclarationService.GenerateDeclarationAsync(contractId);
                
                TempData[SuccessMessage.TempDataKey] = SuccessMessage.DeclarationGenerated(declaration.DeclarationNumber);
                return RedirectToAction(ActionNames.Declarations);
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("gerar declaração", ex.Message);
                return RedirectToAction(ActionNames.Declarations);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDeclaration(int declarationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    return Forbid();
                }

                var declaration = await _context.AddressDeclarations
                    .Include(d => d.Contract)
                    .FirstOrDefaultAsync(d => d.Id == declarationId);

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

                var fileName = $"Declaracao_{declaration.DeclarationNumber}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
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
            // Sistema de créditos removido - redirecionar para dashboard
            TempData[TempDataKeys.Info] = InfoMessages.SystemNoLongerUsesCredits;
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.DashboardDetails));
        }

        [HttpGet]
        public Task<IActionResult> CheckoutCredits(int creditPlanId)
        {
            // Sistema de créditos removido
            TempData[TempDataKeys.Info] = InfoMessages.SystemNoLongerUsesCredits;
            return Task.FromResult<IActionResult>(RedirectToAction(ActionNames.DashboardDetails));
        }

        [HttpPost]
        public Task<IActionResult> GenerateCreditPurchasePix([FromBody] CreditPlanIdRequest req)
        {
            // Sistema de créditos removido
            return Task.FromResult<IActionResult>(Json(new { success = false, error = ErrorJson.CreditsNoLongerUsed }));
        }

        [HttpPost]
        public Task<IActionResult> GenerateCreditPurchaseBoleto([FromBody] CreditPlanIdRequest req)
        {
            // Sistema de créditos removido
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

                // Validar método de pagamento
                if (string.IsNullOrEmpty(paymentMethod))
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.SelectPaymentMethod;
                    return RedirectToAction(ActionNames.CheckoutCredits, new { creditPlanId });
                }

                // Converter string para enum
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

                // Criar compra de créditos
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

                // Preparar dados do cliente
                var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
                var clientEmail = client.ContactEmail ?? client.User.Email;
                var clientTaxId = client.CNPJ ?? client.CPF ?? "";
                var clientPhone = client.ContactPhone ?? client.User.Phone;

                // Preparar endereço para boleto/cartão
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

                // Processar pagamento baseado no método
                if (method == PaymentMethod.PIX)
                {
                    // Criar pedido PIX no Asaas
                    var orderResponse = await _asaasService.CreateOrderAsync(
                        referenceId: purchase.ExternalPaymentId,
                        amount: plan.Price,
                        method: PaymentMethod.PIX,
                        clientName: clientName,
                        clientEmail: clientEmail,
                        clientTaxId: clientTaxId,
                        clientPhone: clientPhone
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

                    // Redirecionar para página de pagamento PIX
                    return RedirectToAction(ActionNames.CreditPurchasePayment, new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.BankTransfer)
                {
                    // Criar pedido Boleto no Asaas
                    var orderResponse = await _asaasService.CreateOrderAsync(
                        referenceId: purchase.ExternalPaymentId,
                        amount: plan.Price,
                        method: PaymentMethod.BankTransfer,
                        clientName: clientName,
                        clientEmail: clientEmail,
                        clientTaxId: clientTaxId,
                        clientPhone: clientPhone,
                        clientAddress: clientAddress
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

                    // Redirecionar para página de pagamento Boleto
                    return RedirectToAction(ActionNames.CreditPurchasePayment, new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.CreditCard)
                {
                    // Para cartão, redirecionar para página com modal de cartão
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("Asaas") || ex.Message.Contains("API Key"))
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

                // Preparar dados do cliente
                var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
                var clientEmail = client.ContactEmail ?? client.User.Email;
                var clientTaxId = client.CNPJ ?? client.CPF ?? "";
                var clientPhone = client.ContactPhone ?? client.User.Phone;

                // Preparar endereço do request ou usar do cliente
                var clientAddress = new Application.DTOs.PagBank.PagBankAddress
                {
                    Street = request.BillingAddress?.Street ?? client.Address ?? "Não informado",
                    Number = request.BillingAddress?.Number ?? "S/N",
                    Locality = request.BillingAddress?.Locality ?? "Não informado",
                    City = request.BillingAddress?.City ?? client.City ?? "Não informado",
                    RegionCode = request.BillingAddress?.RegionCode ?? client.State ?? "SP",
                    Country = request.BillingAddress?.Country ?? "BRA",
                    PostalCode = request.BillingAddress?.PostalCode ?? client.ZipCode?.Replace("-", "").Replace(".", "") ?? "00000000"
                };

                // Criar pedido no Asaas
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
                    clientAddress: clientAddress
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
                        
                        // Adicionar créditos ao cliente
                        var expiresAt = DateTimeHelper.GetBrazilianTime().AddMonths(purchase.CreditPlan.ValidityMonths);
                        var clientCredit = new ClientCredit
                        {
                            ClientId = client.Id,
                            Credits = purchase.CreditsReceived + purchase.BonusCredits,
                            ExpiresAt = expiresAt
                        };

                        _context.ClientCredits.Add(clientCredit);

                        // Creditar também na conta do administrador (conforme regra de negócio)
                        // Se a tabela PlatformCreditBalances existir
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
                            // Tabela PlatformCreditBalances pode não existir ainda; ignorar e continuar
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
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
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
            // ViewBag.AvailableCredits removido - sistema não usa mais créditos
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
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
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
            // Remover duplicatas exatas (mesmo dia e mesmo horário)
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
                // (1) Sobreposição com outro slot desta mesma solicitação
                hasConflict = addedInRequest.Any(t => t.Date == Date && !(t.End <= Start || t.Start >= End));
                // (2) Duplicidade: o próprio usuário já tem reserva neste horário
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
                // (3) Conflito com outras reservas: sala privativa = qualquer sobreposição; compartilhada = lotação
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
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

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

                // Filtro por status
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var s = status.Trim();
                    if (s == nameof(ReservationStatus.Confirmed) || s == nameof(ReservationStatus.Completed) || s == nameof(ReservationStatus.Cancelled))
                        if (Enum.TryParse<ReservationStatus>(s, true, out var resStatus))
                            query = query.Where(r => r.Status == resStatus);
                }

                // Filtro por data (de/até)
                if (dateFrom.HasValue)
                    query = query.Where(r => r.ReservationDate.Date >= dateFrom.Value.Date);
                if (dateTo.HasValue)
                    query = query.Where(r => r.ReservationDate.Date <= dateTo.Value.Date);

                // Filtro por pesquisa (coworking, endereço, cidade)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var t = search.Trim().ToLower();
                    query = query.Where(r =>
                        (r.CoworkingAddress.Coworking.Name != null && r.CoworkingAddress.Coworking.Name.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.Address != null && r.CoworkingAddress.Address.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.City != null && r.CoworkingAddress.City.ToLower().Contains(t)) ||
                        (r.CoworkingAddress.Location != null && r.CoworkingAddress.Location.City != null && r.CoworkingAddress.Location.City.ToLower().Contains(t)));
                }

                // Filtro por tipo de sala
                if (!string.IsNullOrWhiteSpace(roomType))
                {
                    var rt = roomType.Trim();
                    if (rt == "Shared" || rt == "Private")
                        query = query.Where(r => r.RoomType == rt);
                }

                // Ordenação: datas mais próximas e horários por padrão
                var sort = (sortBy ?? "date_asc").ToLower();
                if (sort == "date_desc")
                    query = query.OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.StartTime ?? TimeSpan.Zero);
                else if (sort == "time_desc")
                    query = query.OrderBy(r => r.ReservationDate).ThenByDescending(r => r.StartTime ?? TimeSpan.Zero);
                else if (sort == "time_asc")
                    query = query.OrderBy(r => r.ReservationDate).ThenBy(r => r.StartTime ?? TimeSpan.Zero);
                else
                    // date_asc (padrão): datas mais próximas, depois horário mais cedo
                    query = query.OrderBy(r => r.ReservationDate).ThenBy(r => r.StartTime ?? TimeSpan.Zero);

                // Contagem total para paginação
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;

                // Paginação
                var reservations = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Calcular estatísticas do total (antes da paginação)
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

        /// <summary>
        /// Cliente confirma que realizou/utilizou a reserva. Após dupla confirmação (cliente + coworking),
        /// o sistema repassa os créditos ao coworking e aplica a comissão da plataforma.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservationUsed(int id, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
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
            string? search = null, int? coworkingId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            pageSize = 10;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            // Clientes visualizam apenas notas fiscais aprovadas (status "Emitida")
            // de tipo "Intermediação" (serviços que pagaram) 
            // e também notas fiscais de tipo "Servico" emitidas pelo parceiro para o cliente
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
                    && fn.Status == FiscalNoteStatus.Emitida) // Apenas notas aprovadas/emitidas
                .AsQueryable();

            // Filtro por tipo
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(fn => fn.Type == type);
            }

            // Filtro por busca (número da NF ou chave de acesso)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(fn => 
                    (fn.Number != null && fn.Number.Contains(search)) ||
                    (fn.AccessKey != null && fn.AccessKey.Contains(search)));
            }

            // Filtro por parceiro (coworking)
            if (coworkingId.HasValue)
            {
                query = query.Where(fn => fn.CoworkingId == coworkingId.Value);
            }

            // Filtro por data de emissão
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

            // Buscar lista de parceiros para o filtro
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
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            var fn = await _context.FiscalNotes
                .FirstOrDefaultAsync(f => f.Id == fiscalNoteId 
                    && f.ClientId == client.Id 
                    && (f.Type == FiscalNoteType.Intermediacao || f.Type == FiscalNoteType.Servico)
                    && f.Status == FiscalNoteStatus.Emitida); // Apenas notas aprovadas/emitidas

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
            // Clientes não emitem notas fiscais de comissão, apenas visualizam as notas dos serviços que pagaram
            // Redirecionar para a página de visualização de notas fiscais
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
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction(ActionNames.RegisterClient);
            }

            // Buscar os CoworkingIds dos contratos do cliente
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

    }
}
