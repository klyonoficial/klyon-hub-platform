using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Http;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using CreditPurchase = KlyonHubPlatform.Domain.Entities.CreditPurchase;
using ClientCredit = KlyonHubPlatform.Domain.Entities.ClientCredit;
using CreditPlan = KlyonHubPlatform.Domain.Entities.CreditPlan;

namespace KlyonHubPlatform.Api.Controllers
{
    public class ProcessCreditCardRequest
    {
        public int PurchaseId { get; set; }
        public Application.DTOs.PagBank.PagBankCard? Card { get; set; }
        public Application.DTOs.PagBank.PagBankAddress? BillingAddress { get; set; }
        public int? Installments { get; set; }
    }

    public class CreditPlanIdRequest
    {
        public int CreditPlanId { get; set; }
    }

    public class ReservationSlotDto
    {
        public string Date { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int Participants { get; set; } = 1;
    }

    [Authorize(Roles = "Client,Administrator")]
    public class ClientController : Controller
    {
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentService _paymentService;
        private readonly IDocumentService _documentService;
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly IPagBankService _pagBankService;
        private readonly ApplicationDbContext _context;
        private readonly IReservationTransferService _reservationTransferService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClientController> _logger;
        private readonly ISystemConfigurationService _systemConfig;

        public ClientController(
            IContractService contractService,
            ICoworkingService coworkingService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IDocumentService documentService,
            IAddressDeclarationService addressDeclarationService,
            IPagBankService pagBankService,
            ApplicationDbContext context,
            IReservationTransferService reservationTransferService,
            IEmailService emailService,
            ILogger<ClientController> logger,
            ISystemConfigurationService systemConfig)
        {
            _contractService = contractService;
            _coworkingService = coworkingService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _documentService = documentService;
            _addressDeclarationService = addressDeclarationService;
            _pagBankService = pagBankService;
            _context = context;
            _reservationTransferService = reservationTransferService;
            _emailService = emailService;
            _logger = logger;
            _systemConfig = systemConfig;
        }

        private async Task<long> GetMaxPhotoSizeBytesAsync()
        {
            var mb = await _systemConfig.GetValueAsync("Upload.MaxPhotoSizeMB");
            if (int.TryParse(mb, out var n) && n > 0) return n * 1024L * 1024L;
            return 5 * 1024L * 1024L;
        }

        [HttpGet]
        public async Task<IActionResult> Services()
        {
            var marketplaceRoute = Url.Action("Marketplace", "Home") ?? "/Home/Marketplace";
            var chooseLocationRoute = Url.Action("ChooseLocation", "Client") ?? "/Client/ChooseLocation";

            // Buscar serviços ativos ou criar serviços padrão se não existirem
            var services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            // Corrigir rotas incorretas de serviços relacionados a salas
            var roomServiceKeywords = new[] { "Sala", "Auditório", "Aluguel" };
            var needsUpdate = false;

            foreach (var service in services)
            {
                // Se o serviço é relacionado a salas e tem rota incorreta, corrigir
                if (roomServiceKeywords.Any(keyword => service.Name.Contains(keyword)) && 
                    service.Route != marketplaceRoute && 
                    !service.Route.Contains("/Home/Marketplace"))
                {
                    service.Route = marketplaceRoute;
                    service.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                    needsUpdate = true;
                }
                // Corrigir rotas que apontam para /marketplace (sem /Home/)
                else if (service.Route == "/marketplace" || service.Route.Contains("/marketplace") && !service.Route.Contains("/Home/Marketplace"))
                {
                    service.Route = marketplaceRoute;
                    service.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                await _context.SaveChangesAsync();
                services = await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();
            }

            // Se não houver serviços cadastrados, criar os serviços padrão
            if (!services.Any())
            {
                var defaultServices = new[]
                {
                    new Domain.Entities.Service
                    {
                        Name = "Contratar Endereço Fiscal",
                        Description = "Contrate um endereço fiscal para sua empresa",
                        Route = chooseLocationRoute,
                        DisplayOrder = 1,
                        IsActive = true,
                        ImageUrl = "/images/services/endereco-fiscal.svg",
                        CreatedAt = DateTimeHelper.GetBrazilianTime()
                    },
                    new Domain.Entities.Service
                    {
                        Name = "Reservar Sala",
                        Description = "Reserve salas de coworking para reuniões e trabalho",
                        Route = marketplaceRoute,
                        DisplayOrder = 2,
                        IsActive = true,
                        ImageUrl = "/images/services/sala-compartilhada.svg",
                        CreatedAt = DateTimeHelper.GetBrazilianTime()
                    }
                };

                foreach (var service in defaultServices)
                {
                    _context.Services.Add(service);
                }
                await _context.SaveChangesAsync();

                services = await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();
            }

            // Verificar se os menus de coworking estão habilitados
            var enableCoworkingMenus = await _systemConfig.GetValueAsync("Features.EnableCoworkingMenus");
            var showCoworkingMenus = enableCoworkingMenus?.ToLower() == "true";

            // Se os menus de coworking estiverem desabilitados, mostrar apenas "Contratar Endereço Fiscal"
            if (!showCoworkingMenus)
            {
                services = services
                    .Where(s => s.Name == "Contratar Endereço Fiscal")
                    .ToList();
            }

            return View(services);
        }

        public async Task<IActionResult> Dashboard()
        {
            // Redirecionar para a página de Serviços (página principal do cliente)
            return RedirectToAction("Services");
        }

        [HttpGet]
        public async Task<IActionResult> DashboardDetails()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.CoworkingAddress)
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.Subscription)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            // Sistema de créditos removido - não buscamos mais créditos

            // Buscar reservas do cliente
            int reservationsCount = 0;
            if (client != null)
            {
                try
                {
                    reservationsCount = await _context.Reservations
                        .Where(r => r.ClientId == client.Id && r.Status == "Confirmed")
                        .CountAsync();
                }
                catch
                {
                    // Tabela Reservations pode não existir ainda
                    reservationsCount = 0;
                }
            }

            ViewBag.Client = client;
            // ViewBag.AvailableCredits removido - sistema não usa mais créditos
            ViewBag.ReservationsCount = reservationsCount;
            return View("Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                TempData["Error"] = "Cliente não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction("RegisterClient");
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;

            var maxPhotoMb = 5;
            var mbStr = await _systemConfig.GetValueAsync("Upload.MaxPhotoSizeMB");
            if (int.TryParse(mbStr, out var n) && n > 0) maxPhotoMb = n;
            ViewBag.MaxPhotoSizeMB = maxPhotoMb;
            
            return View(client);
        }

        [HttpGet]
        public async Task<IActionResult> GetLogo(int clientId)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            
            if (client == null || string.IsNullOrEmpty(client.LogoPath))
            {
                return NotFound();
            }

            // Verificar se o usuário logado tem permissão para ver a logo
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var userClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            
            // Permitir acesso se:
            // 1. É o próprio cliente
            // 2. É administrador
            var canAccess = User.IsInRole("Administrator") || (userClient != null && userClient.Id == clientId);
            
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Client client, IFormFile? Logo)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (existingClient == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("Profile");
                }

                // Remover máscaras do CNPJ e CPF para validação
                // Tratar caso onde o campo pode vir como null ou string vazia
                var cnpjInput = string.IsNullOrWhiteSpace(client.CNPJ) ? null : client.CNPJ;
                var cnpjClean = cnpjInput?.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var cpfInput = string.IsNullOrWhiteSpace(client.CPF) ? null : client.CPF;
                var cpfClean = cpfInput?.Replace(".", "").Replace("-", "").Trim();

                // Validar CNPJ se informado
                if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length == 14)
                {
                    // Verificar se outro cliente já possui este CNPJ
                    var allClients = await _context.Clients
                        .Where(c => c.CNPJ != null && c.Id != existingClient.Id)
                        .ToListAsync();
                    var existingCNPJ = allClients.FirstOrDefault(c => 
                        c.CNPJ != null && 
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                    
                    if (existingCNPJ != null)
                    {
                        TempData["Error"] = $"O CNPJ {client.CNPJ} já está cadastrado no sistema.";
                        return RedirectToAction("Profile");
                    }
                    // Salvar CNPJ com máscara
                    existingClient.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    // Marcar explicitamente como modificado para garantir que o EF detecte a mudança
                    _context.Entry(existingClient).Property(c => c.CNPJ).IsModified = true;
                }
                else if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length != 14)
                {
                    TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos.";
                    return RedirectToAction("Profile");
                }
                else
                {
                    // Se o CNPJ vier vazio/null, limpar o CNPJ existente
                    existingClient.CNPJ = null;
                    // Marcar explicitamente como modificado para garantir que o EF detecte a mudança
                    _context.Entry(existingClient).Property(c => c.CNPJ).IsModified = true;
                }

                // Validar CPF se informado
                if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length == 11)
                {
                    // Verificar se outro cliente já possui este CPF
                    var allClients = await _context.Clients
                        .Where(c => c.CPF != null && c.Id != existingClient.Id)
                        .ToListAsync();
                    var existingCPF = allClients.FirstOrDefault(c => 
                        c.CPF != null && 
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                    
                    if (existingCPF != null)
                    {
                        TempData["Error"] = $"O CPF {client.CPF} já está cadastrado no sistema.";
                        return RedirectToAction("Profile");
                    }
                    // Salvar CPF com máscara
                    existingClient.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    // Marcar explicitamente como modificado para garantir que o EF detecte a mudança
                    _context.Entry(existingClient).Property(c => c.CPF).IsModified = true;
                }
                else if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length != 11)
                {
                    TempData["Error"] = "CPF inválido. O CPF deve conter 11 dígitos.";
                    return RedirectToAction("Profile");
                }
                else
                {
                    // Se o CPF vier vazio/null, limpar o CPF existente
                    existingClient.CPF = null;
                    // Marcar explicitamente como modificado para garantir que o EF detecte a mudança
                    _context.Entry(existingClient).Property(c => c.CPF).IsModified = true;
                }

                // Atualizar campos (exceto ContactEmail que sempre vem do usuário)
                existingClient.RazaoSocial = client.RazaoSocial;
                existingClient.NomeFantasia = client.NomeFantasia;
                existingClient.ContactName = client.ContactName;
                existingClient.ContactEmail = userEmail; // Sempre usar o email do usuário logado
                
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
                existingClient.City = client.City;
                existingClient.State = client.State;
                
                // Upload da logo se fornecida
                if (Logo != null && Logo.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(Logo.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["Error"] = "Formato de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.";
                        return RedirectToAction("Profile");
                    }
                    
                    var maxBytes = await GetMaxPhotoSizeBytesAsync();
                    var maxMb = (int)(maxBytes / (1024 * 1024));
                    if (Logo.Length > maxBytes)
                    {
                        TempData["Error"] = $"O arquivo é muito grande. O tamanho máximo é {maxMb}MB. (Configurável em Admin > Configurações > Upload.MaxPhotoSizeMB)";
                        return RedirectToAction("Profile");
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
                        TempData["Error"] = $"Erro ao fazer upload da logo: {ex.Message}";
                        return RedirectToAction("Profile");
                    }
                }
                
                existingClient.UpdatedAt = DateTimeHelper.GetBrazilianTime();

                await _context.SaveChangesAsync();

                TempData["Success"] = "Perfil atualizado com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                TempData["Error"] = "Este CNPJ ou CPF já está cadastrado no sistema. Verifique os dados informados.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar perfil: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpGet]
        public async Task<IActionResult> RegisterClient(int? id = null)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
            
            // Se houver um ID, carregar o cliente para edição
            if (id.HasValue)
            {
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == id.Value);
                
                if (client != null)
                {
                    // Se for admin ou o próprio cliente, permitir edição
                    if (User.IsInRole("Administrator") || client.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)))
                    {
                        return View(client);
                    }
                }
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClient(Client client)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var isAdmin = User.IsInRole("Administrator");
                
                // Se houver ID, é uma edição
                if (client.Id > 0)
                {
                    var existingClient = await _context.Clients
                        .Include(c => c.User)
                        .FirstOrDefaultAsync(c => c.Id == client.Id);
                    
                    if (existingClient == null)
                    {
                        TempData["Error"] = "Cliente não encontrado.";
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    
                    // Verificar permissão: admin ou o próprio cliente
                    if (!isAdmin && existingClient.UserId != userId)
                    {
                        TempData["Error"] = "Você não tem permissão para editar este cliente.";
                        return RedirectToAction("Dashboard");
                    }
                    
                    // Atualizar campos
                    existingClient.RazaoSocial = client.RazaoSocial;
                    existingClient.NomeFantasia = client.NomeFantasia;
                    existingClient.ContactName = client.ContactName;
                    existingClient.ContactPhone = client.ContactPhone;
                    existingClient.Address = client.Address;
                    existingClient.City = client.City;
                    existingClient.State = client.State;
                    existingClient.ZipCode = client.ZipCode;
                    
                    // Se for admin, permitir alterar email
                    if (isAdmin && !string.IsNullOrEmpty(client.ContactEmail))
                    {
                        existingClient.ContactEmail = client.ContactEmail;
                    }
                    
                    // Validar e atualizar CNPJ/CPF
                    var cnpjClean = client.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    var cpfClean = client.CPF?.Replace(".", "").Replace("-", "").Trim();
                    
                    if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length == 14)
                    {
                        // Verificar se outro cliente já possui este CNPJ
                        var allClients = await _context.Clients
                            .Where(c => c.CNPJ != null && c.Id != existingClient.Id)
                            .ToListAsync();
                        var existingCNPJ = allClients.FirstOrDefault(c => 
                            c.CNPJ != null && 
                            c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                        
                        if (existingCNPJ != null)
                        {
                            TempData["Error"] = $"O CNPJ {client.CNPJ} já está cadastrado para outro cliente.";
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        existingClient.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    }
                    else if (!string.IsNullOrEmpty(cnpjClean))
                    {
                        existingClient.CNPJ = null;
                    }
                    
                    if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length == 11)
                    {
                        // Verificar se outro cliente já possui este CPF
                        var allClients = await _context.Clients
                            .Where(c => c.CPF != null && c.Id != existingClient.Id)
                            .ToListAsync();
                        var existingCPF = allClients.FirstOrDefault(c => 
                            c.CPF != null && 
                            c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                        
                        if (existingCPF != null)
                        {
                            TempData["Error"] = $"O CPF {client.CPF} já está cadastrado para outro cliente.";
                            ViewBag.UserEmail = existingClient.ContactEmail ?? userEmail;
                            return View(existingClient);
                        }
                        existingClient.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    }
                    else if (!string.IsNullOrEmpty(cpfClean))
                    {
                        existingClient.CPF = null;
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
                    
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cliente atualizado com sucesso!";
                    
                    if (isAdmin)
                    {
                        return RedirectToAction("Contracts", "Admin");
                    }
                    return RedirectToAction("Dashboard");
                }
                
                // Se não houver ID, é um novo cadastro
                // Verificar se o usuário já possui um cliente cadastrado
                var userClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (userClient != null && !isAdmin)
                {
                    TempData["Error"] = "Você já possui um cadastro de cliente. Acesse seu perfil para editar as informações.";
                    return RedirectToAction("Dashboard");
                }

                // Remover máscaras do CNPJ e CPF para validação
                var cnpjCleanNew = client.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var cpfCleanNew = client.CPF?.Replace(".", "").Replace("-", "").Trim();

                // Validar CNPJ se informado
                if (!string.IsNullOrEmpty(cnpjCleanNew) && cnpjCleanNew.Length == 14)
                {
                    // Buscar todos os clientes e comparar CNPJ sem máscara
                    var allClients = await _context.Clients.Where(c => c.CNPJ != null).ToListAsync();
                    var existingCNPJ = allClients.FirstOrDefault(c => 
                        c.CNPJ != null && 
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjCleanNew);
                    
                    if (existingCNPJ != null)
                    {
                        TempData["Error"] = $"O CNPJ {client.CNPJ} já está cadastrado no sistema.";
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CNPJ com máscara
                    client.CNPJ = $"{cnpjCleanNew.Substring(0, 2)}.{cnpjCleanNew.Substring(2, 3)}.{cnpjCleanNew.Substring(5, 3)}/{cnpjCleanNew.Substring(8, 4)}-{cnpjCleanNew.Substring(12, 2)}";
                }
                else if (!string.IsNullOrEmpty(cnpjCleanNew) && cnpjCleanNew.Length != 14)
                {
                    TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos.";
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }

                // Validar CPF se informado
                if (!string.IsNullOrEmpty(cpfCleanNew) && cpfCleanNew.Length == 11)
                {
                    // Buscar todos os clientes e comparar CPF sem máscara
                    var allClients = await _context.Clients.Where(c => c.CPF != null).ToListAsync();
                    var existingCPF = allClients.FirstOrDefault(c => 
                        c.CPF != null && 
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfCleanNew);
                    
                    if (existingCPF != null)
                    {
                        TempData["Error"] = $"O CPF {client.CPF} já está cadastrado no sistema.";
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CPF com máscara
                    client.CPF = $"{cpfCleanNew.Substring(0, 3)}.{cpfCleanNew.Substring(3, 3)}.{cpfCleanNew.Substring(6, 3)}-{cpfCleanNew.Substring(9, 2)}";
                }
                else if (!string.IsNullOrEmpty(cpfCleanNew) && cpfCleanNew.Length != 11)
                {
                    TempData["Error"] = "CPF inválido. O CPF deve conter 11 dígitos.";
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

                TempData["Success"] = "Cadastro realizado com sucesso!";
                return RedirectToAction("Dashboard");
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData["Error"] = "Este CNPJ ou CPF já está cadastrado no sistema. Verifique os dados informados.";
                ViewBag.UserEmail = userEmail;
                return View(client);
            }
            catch (Exception ex)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData["Error"] = $"Erro ao cadastrar cliente: {ex.Message}";
                ViewBag.UserEmail = userEmail;
                return View(client);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ChooseLocation(string? region = null, string? state = null, int page = 1)
        {
            const int pageSize = 6;
            
            // Query base: localidades ativas
            var query = _context.Locations.Where(l => l.IsActive);
            var allLocations = await query.ToListAsync();
            var locationIds = allLocations.Select(l => l.Id).ToList();
            
            // Contar endereços disponíveis por localidade (IsAvailable, CurrentContracts < MaxContracts)
            var availableAddressesCount = await _context.CoworkingAddresses
                .Where(a => a.IsAvailable && a.CurrentContracts < a.MaxContracts && locationIds.Contains(a.LocationId))
                .GroupBy(a => a.LocationId)
                .Select(g => new { LocationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LocationId, x => x.Count);
            
            // Mostrar somente localidades que tenham pelo menos um endereço cadastrado
            var locationsWithAddresses = allLocations
                .Where(l => availableAddressesCount.ContainsKey(l.Id) && availableAddressesCount[l.Id] > 0)
                .ToList();
            
            // Aplicar filtros de região e estado
            if (!string.IsNullOrEmpty(region))
                locationsWithAddresses = locationsWithAddresses.Where(l => l.Region == region).ToList();
            if (!string.IsNullOrEmpty(state))
                locationsWithAddresses = locationsWithAddresses.Where(l => l.State == state).ToList();
            
            // Ordenar por quantidade de endereços (maior primeiro) e depois por estado
            var sortedLocations = locationsWithAddresses
                .OrderByDescending(l => availableAddressesCount[l.Id])
                .ThenBy(l => l.State)
                .ToList();
            
            // Paginação
            var totalItems = sortedLocations.Count;
            var totalPages = totalItems > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));
            
            var pagedLocations = sortedLocations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            ViewBag.AvailableAddressesCount = availableAddressesCount;
            ViewBag.Region = region;
            ViewBag.State = state;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            
            // Filtros: apenas regiões e estados que possuem ao menos uma localidade com endereço
            var baseWithAddresses = allLocations
                .Where(l => availableAddressesCount.ContainsKey(l.Id) && availableAddressesCount[l.Id] > 0)
                .ToList();
            ViewBag.Regions = baseWithAddresses.Select(l => l.Region).Where(r => !string.IsNullOrEmpty(r)).Distinct().OrderBy(r => r!).ToList();
            ViewBag.States = baseWithAddresses.Select(l => l.State).Distinct().OrderBy(s => s).ToList();
            
            return View(pagedLocations);
        }

        [HttpGet]
        public async Task<IActionResult> AvailableAddresses(int locationId)
        {
            var addresses = await _coworkingService.GetAvailableAddressesAsync(locationId);
            
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
            ViewBag.MainPhotos = mainPhotos;
            
            return View(addresses);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateContract(int coworkingAddressId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado. Complete seu cadastro primeiro.";
                    return RedirectToAction("RegisterClient");
                }

                var addressEntity = await _context.CoworkingAddresses
                    .Include(a => a.Location)
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);

                if (addressEntity == null)
                {
                    TempData["Error"] = "Endereço não encontrado.";
                    return RedirectToAction("ChooseLocation");
                }

                if (!addressEntity.IsAvailable)
                {
                    TempData["Error"] = "Este endereço não está mais disponível.";
                    return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
                }

                if (addressEntity.CurrentContracts >= addressEntity.MaxContracts)
                {
                    TempData["Error"] = "Este endereço atingiu o limite máximo de contratos.";
                    return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
                }

                // Verificar se o cliente já tem um contrato ativo para este endereço
                var existingActiveContract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .Where(c => c.ClientId == client.Id 
                        && c.CoworkingAddressId == coworkingAddressId 
                        && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                    .FirstOrDefaultAsync();

                if (existingActiveContract != null)
                {
                    TempData["Error"] = $"Você já possui um contrato {(existingActiveContract.Status == ContractStatus.Active ? "ativo" : "suspenso")} para este endereço. Número do contrato: {existingActiveContract.ContractNumber}";
                    return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
                }

                // Verificar se o cliente já tem um contrato ativo na mesma localidade
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
                    TempData["Error"] = $"Você já possui um contrato {(existingContractInLocation.Status == ContractStatus.Active ? "ativo" : "suspenso")} nesta localidade ({locationInfo}). Número do contrato: {existingContractInLocation.ContractNumber}";
                    return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
                }

                var contract = await _contractService.CreateContractAsync(
                    client.Id,
                    coworkingAddressId,
                    addressEntity.BasePrice
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
                            Action = "ContractCreated",
                            ActionBy = userId, // ID do cliente que criou o contrato
                            ActionAt = DateTimeHelper.GetBrazilianTime()
                        };
                        _context.PaymentHistories.Add(history);
                        await _context.SaveChangesAsync();
                        
                        TempData["Success"] = "Contrato criado com sucesso!";
                        return RedirectToAction("PaymentDetails", new { id = payment.Id });
                    }
                }

                TempData["Success"] = "Contrato criado com sucesso!";
                return RedirectToAction("ContractDetails", new { id = contract.Id });
            }
            catch (Exception ex)
            {
                // Se houver qualquer erro, o contrato NÃO foi criado (transação foi revertida)
                TempData["Error"] = $"Erro ao criar contrato: {ex.Message}";
                var addressEntity = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);
                if (addressEntity != null)
                {
                    return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
                }
                return RedirectToAction("ChooseLocation");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ContractDetails(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (contract.Client.UserId != userId && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            return View(contract);
        }

        [HttpGet]
        public async Task<IActionResult> MyContracts(string? search = null, string? status = null, int page = 1, int pageSize = 15)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction("RegisterClient");
            }

            var query = _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
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

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(contracts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestContractCancellation(int contractId, string reason)
        {
            try
            {
                var contract = await _contractService.GetByIdAsync(contractId);
                if (contract == null)
                {
                    TempData["Error"] = "Contrato não encontrado.";
                    return RedirectToAction("MyContracts");
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (contract.Client.UserId != userId)
                {
                    return Forbid();
                }

                if (contract.Status != ContractStatus.Active)
                {
                    TempData["Error"] = "Apenas contratos ativos podem ser cancelados.";
                    return RedirectToAction("ContractDetails", new { id = contractId });
                }

                // Atualizar contrato com solicitação de cancelamento
                contract.Status = ContractStatus.PendingCancellation;
                contract.CancellationRequestedAt = DateTimeHelper.GetBrazilianTime();
                contract.CancellationRequestReason = reason;
                contract.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                _context.Contracts.Update(contract);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Solicitação de cancelamento enviada com sucesso! Aguarde a aprovação administrativa.";
                return RedirectToAction("ContractDetails", new { id = contractId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao solicitar cancelamento: {ex.Message}";
                return RedirectToAction("ContractDetails", new { id = contractId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelContract(int contractId, string reason)
        {
            try
            {
                await _contractService.CancelContractAsync(contractId, reason);
                return RedirectToAction("MyContracts");
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
                return RedirectToAction("ContractDetails", new { id = contractId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId)
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
        public async Task<IActionResult> MyPayments(int page = 1, int pageSize = 15)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction("RegisterClient");
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
        public async Task<IActionResult> PaymentDetails(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (payment == null)
            {
                return NotFound();
            }

            // Verificar se o pagamento pertence ao cliente logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction("RegisterClient");
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
            
            if (!isAuthorized && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }
            
            // Usar o serviço para gerar códigos se necessário
            if (payment.Status == PaymentStatus.Pending)
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
                            .Include(p => p.Reservation)
                                .ThenInclude(r => r.CoworkingAddress)
                                    .ThenInclude(a => a.Coworking)
                            .FirstOrDefaultAsync(p => p.Id == id);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("PagBank") || ex.Message.Contains("Token de acesso"))
                {
                    // PagBank não configurado - mostrar mensagem amigável mas permitir visualizar o pagamento
                    TempData["Warning"] = "Sistema de pagamento não configurado. Configure o PagBank nas Configurações do Sistema para gerar códigos de pagamento.";
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
                catch (InvalidOperationException ex) when (ex.Message.Contains("PagBank") || ex.Message.Contains("Token de acesso"))
                {
                    // PagBank não configurado - mostrar mensagem amigável mas permitir visualizar o pagamento
                    TempData["Warning"] = "Sistema de pagamento não configurado. Configure o PagBank nas Configurações do Sistema para gerar códigos de pagamento.";
                    // Continuar para mostrar a página mesmo sem o código gerado
                }
                catch (Exception ex)
                {
                    // Outros erros - logar mas não impedir visualização
                    TempData["Warning"] = $"Aviso: Não foi possível gerar o código de pagamento automaticamente. Você pode tentar novamente mais tarde.";
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

            // Calcular valor com percentual de cartão de crédito se aplicável
            decimal originalAmount = payment.Amount;
            decimal finalAmount = payment.Amount;
            decimal? feePercentage = null;
            
            if (payment.Method == PaymentMethod.CreditCard)
            {
                var feePercentageStr = await _systemConfig.GetValueAsync("Payment.CreditCardFeePercentage");
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

            // Buscar histórico do pagamento
            var paymentHistory = await _context.PaymentHistories
                .Include(h => h.ActionByUser)
                .Where(h => h.PaymentId == payment.Id)
                .OrderByDescending(h => h.ActionAt)
                .ToListAsync();
            ViewBag.PaymentHistory = paymentHistory;

            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentMethod(int paymentId, string method)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("MyPayments");
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction("RegisterClient");
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole("Administrator"))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData["Error"] = "Apenas pagamentos pendentes podem ter o método alterado.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                // Converter string para enum
                if (!Enum.TryParse<PaymentMethod>(method, out var paymentMethod))
                {
                    TempData["Error"] = "Método de pagamento inválido.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                await _paymentService.UpdatePaymentMethodAsync(paymentId, paymentMethod);
                TempData["Success"] = "Método de pagamento atualizado com sucesso!";

                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar método de pagamento: {ex.Message}";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePixCode(int paymentId)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("MyPayments");
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction("RegisterClient");
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole("Administrator"))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData["Error"] = "Apenas pagamentos pendentes podem ter o código PIX gerado.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                if (payment.Method != PaymentMethod.PIX)
                {
                    TempData["Error"] = "Este pagamento não é via PIX.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                // Gerar código PIX
                await _paymentService.GeneratePixCodeAsync(paymentId);
                TempData["Success"] = "Código PIX gerado com sucesso!";
                
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("PagBank") || ex.Message.Contains("Token de acesso"))
            {
                TempData["Error"] = "Sistema de pagamento não configurado. Configure o PagBank nas Configurações do Sistema (Admin > Configurações do Sistema > Configurar PagBank Sandbox).";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao gerar código PIX: {ex.Message}";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
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
                    TempData["Error"] = "Pagamento não encontrado.";
                    return RedirectToAction("MyPayments");
                }

                // Verificar se o pagamento pertence ao cliente logado
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    return RedirectToAction("RegisterClient");
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
                
                if (contract == null || contract.ClientId != client.Id)
                {
                    if (!User.IsInRole("Administrator"))
                    {
                        return Forbid();
                    }
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    TempData["Error"] = "Este pagamento já foi processado.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                // Validar se o comprovante foi enviado
                if (receiptFile == null || receiptFile.Length == 0)
                {
                    TempData["Error"] = "Por favor, envie o comprovante de pagamento.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                // Validar tipo de arquivo (apenas imagens e PDFs)
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var fileExtension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["Error"] = "Formato de arquivo inválido. Envie apenas imagens (JPG, PNG) ou PDF.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
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
                    Action = "ReceiptUploaded",
                    ActionBy = userId,
                    ActionAt = DateTimeHelper.GetBrazilianTime()
                };
                _context.PaymentHistories.Add(history);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Comprovante enviado com sucesso! O pagamento está aguardando validação pelo coworking.";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao confirmar pagamento: {ex.Message}";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(int paymentId)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.ReceiptFilePath))
            {
                return NotFound();
            }

            // Verificar se o pagamento pertence ao cliente logado
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction("RegisterClient");
            }

            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
            
            if (contract == null || contract.ClientId != client.Id)
            {
                if (!User.IsInRole("Administrator") && !User.IsInRole("CoworkingPartner"))
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCreditCard(int paymentId, string cardNumber, string cardHolderName, 
            string expiryMonth, string expiryYear, string cvv, string? isDefault)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
                }

                // Remover espaços e caracteres não numéricos do número do cartão
                var cleanCardNumber = cardNumber.Replace(" ", "").Replace("-", "");
                if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19)
                {
                    TempData["Error"] = "Número do cartão inválido.";
                    return RedirectToAction("PaymentDetails", new { id = paymentId });
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
                var setAsDefault = !string.IsNullOrEmpty(isDefault) && isDefault.ToLower() == "true";
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

                TempData["Success"] = "Cartão adicionado com sucesso!";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar cartão: {ex.Message}";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Declarations(
            string? search = null,
            int? contractId = null,
            string? status = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (client == null)
            {
                TempData["Error"] = "Cliente não encontrado. Complete seu cadastro primeiro.";
                return RedirectToAction("RegisterClient");
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

            ViewBag.Contracts = contractsToDisplay;
            ViewBag.AllContracts = allContracts; // Para o dropdown de filtros
            ViewBag.DeclarationsByContract = declarationsByContract;
            ViewBag.Search = search;
            ViewBag.ContractId = contractId;
            ViewBag.Status = status;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDeclaration(int contractId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("Declarations");
                }

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientId == client.Id);

                if (contract == null)
                {
                    TempData["Error"] = "Contrato não encontrado ou não pertence a você.";
                    return RedirectToAction("Declarations");
                }

                var declaration = await _addressDeclarationService.GenerateDeclarationAsync(contractId);
                
                TempData["Success"] = $"Declaração gerada com sucesso! Número: {declaration.DeclarationNumber}";
                return RedirectToAction("Declarations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao gerar declaração: {ex.Message}";
                return RedirectToAction("Declarations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDeclaration(int declarationId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
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
                    TempData["Error"] = "Esta declaração está inválida ou expirada.";
                    return RedirectToAction("Declarations");
                }

                var pdfBytes = await _addressDeclarationService.GetDeclarationPdfAsync(declarationId);
                
                if (pdfBytes == null)
                {
                    TempData["Error"] = "Erro ao baixar declaração. Arquivo não encontrado.";
                    return RedirectToAction("Declarations");
                }

                var fileName = $"Declaracao_{declaration.DeclarationNumber}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao baixar declaração: {ex.Message}";
                return RedirectToAction("Declarations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuyCredits()
        {
            // Sistema de créditos removido - redirecionar para dashboard
            TempData["Info"] = "O sistema não utiliza mais o conceito de créditos. Todas as transações são realizadas diretamente em R$.";
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> CheckoutCredits(int creditPlanId)
        {
            // Sistema de créditos removido
            TempData["Info"] = "O sistema não utiliza mais o conceito de créditos. Todas as transações são realizadas diretamente em R$.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> GenerateCreditPurchasePix([FromBody] CreditPlanIdRequest req)
        {
            // Sistema de créditos removido
            return Json(new { success = false, error = "O sistema não utiliza mais o conceito de créditos. Todas as transações são realizadas diretamente em R$." });
        }

        [HttpPost]
        public async Task<IActionResult> GenerateCreditPurchaseBoleto([FromBody] CreditPlanIdRequest req)
        {
            // Sistema de créditos removido
            return Json(new { success = false, error = "O sistema não utiliza mais o conceito de créditos. Todas as transações são realizadas diretamente em R$." });
        }

        [HttpPost]
        public async Task<IActionResult> CreateCreditCardPurchase([FromBody] CreditPlanIdRequest req)
        {
            try
            {
                if (req == null || req.CreditPlanId <= 0)
                    return Json(new { success = false, error = "Plano inválido." });

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                    return Json(new { success = false, error = "Cliente não encontrado." });

                var plan = await _context.CreditPlans.FirstOrDefaultAsync(p => p.Id == req.CreditPlanId && p.IsActive);
                if (plan == null)
                    return Json(new { success = false, error = "Plano não encontrado ou indisponível." });

                var purchase = new CreditPurchase
                {
                    ClientId = client.Id,
                    CreditPlanId = plan.Id,
                    AmountPaid = plan.Price,
                    CreditsReceived = plan.Credits,
                    BonusCredits = plan.BonusCredits,
                    Status = "Processing",
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
        public async Task<IActionResult> PurchaseCredits(int creditPlanId, string paymentMethod, string? returnUrl = null)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("BuyCredits");
                }

                var plan = await _context.CreditPlans.FirstOrDefaultAsync(p => p.Id == creditPlanId && p.IsActive);
                if (plan == null)
                {
                    TempData["Error"] = "Plano não encontrado ou indisponível.";
                    return RedirectToAction("BuyCredits");
                }

                // Validar método de pagamento
                if (string.IsNullOrEmpty(paymentMethod))
                {
                    TempData["Error"] = "Por favor, selecione um método de pagamento.";
                    return RedirectToAction("CheckoutCredits", new { creditPlanId });
                }

                // Converter string para enum
                PaymentMethod method;
                if (!Enum.TryParse<PaymentMethod>(paymentMethod, out method))
                {
                    TempData["Error"] = "Método de pagamento inválido.";
                    return RedirectToAction("CheckoutCredits", new { creditPlanId });
                }

                // Criar compra de créditos
                var purchase = new CreditPurchase
                {
                    ClientId = client.Id,
                    CreditPlanId = plan.Id,
                    AmountPaid = plan.Price,
                    CreditsReceived = plan.Credits,
                    BonusCredits = plan.BonusCredits,
                    Status = "Pending",
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
                    // Criar pedido PIX no PagBank
                    var orderResponse = await _pagBankService.CreateOrderAsync(
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

                    purchase.Status = "Processing";
                    await _context.SaveChangesAsync();

                    // Redirecionar para página de pagamento PIX
                    return RedirectToAction("CreditPurchasePayment", new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.BankTransfer)
                {
                    // Criar pedido Boleto no PagBank
                    var orderResponse = await _pagBankService.CreateOrderAsync(
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

                    purchase.Status = "Processing";
                    await _context.SaveChangesAsync();

                    // Redirecionar para página de pagamento Boleto
                    return RedirectToAction("CreditPurchasePayment", new { purchaseId = purchase.Id });
                }
                else if (method == PaymentMethod.CreditCard)
                {
                    // Para cartão, redirecionar para página com modal de cartão
                    purchase.Status = "Processing";
                    await _context.SaveChangesAsync();
                    
                    return RedirectToAction("CreditPurchasePayment", new { purchaseId = purchase.Id });
                }
                else
                {
                    TempData["Error"] = "Método de pagamento não suportado.";
                    return RedirectToAction("CheckoutCredits", new { creditPlanId });
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("PagBank") || ex.Message.Contains("Token de acesso"))
            {
                TempData["Error"] = "Sistema de pagamento não configurado. Configure o PagBank nas Configurações do Sistema (Admin > Configurações do Sistema > Configurar PagBank Sandbox).";
                return RedirectToAction("CheckoutCredits", new { creditPlanId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao processar pagamento: {ex.Message}";
                return RedirectToAction("CheckoutCredits", new { creditPlanId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreditPurchasePayment(int purchaseId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (client == null)
            {
                TempData["Error"] = "Cliente não encontrado.";
                return RedirectToAction("BuyCredits");
            }

            var purchase = await _context.CreditPurchases
                .Include(p => p.CreditPlan)
                .FirstOrDefaultAsync(p => p.Id == purchaseId && p.ClientId == client.Id);

            if (purchase == null)
            {
                TempData["Error"] = "Compra não encontrada.";
                return RedirectToAction("BuyCredits");
            }

            ViewBag.Purchase = purchase;
            ViewBag.Client = client;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCreditCardPayment([FromBody] ProcessCreditCardRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, error = "Dados inválidos." });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    return Json(new { success = false, error = "Cliente não encontrado." });
                }

                var purchase = await _context.CreditPurchases
                    .Include(p => p.CreditPlan)
                    .FirstOrDefaultAsync(p => p.Id == request.PurchaseId && p.ClientId == client.Id);

                if (purchase == null)
                {
                    return Json(new { success = false, error = "Compra não encontrada." });
                }

                if (purchase.PaymentMethod != PaymentMethod.CreditCard)
                {
                    return Json(new { success = false, error = "Este pagamento não é via cartão de crédito." });
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

                // Criar pedido no PagBank
                var orderResponse = await _pagBankService.CreateOrderAsync(
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
                        purchase.Status = "Paid";
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
                        purchase.Status = "Cancelled";
                    }
                    else
                    {
                        purchase.Status = "Processing";
                    }
                }

                await _context.SaveChangesAsync();

                if (purchase.Status == "Paid")
                {
                    return Json(new { success = true, message = "Pagamento aprovado! Créditos creditados em sua conta." });
                }
                else if (purchase.Status == "Cancelled")
                {
                    return Json(new { success = false, error = "Pagamento recusado. Verifique os dados do cartão e tente novamente." });
                }
                else
                {
                    return Json(new { success = true, message = "Pagamento em processamento. Você será notificado quando for confirmado." });
                }
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pagamento com cartão (PurchaseId: {PurchaseId})", request?.PurchaseId);
                return Json(new { success = false, error = "Não foi possível processar o pagamento. Tente novamente ou entre em contato com o suporte." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BookRoom(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData["Error"] = "Cliente não encontrado.";
                return RedirectToAction("Marketplace", "Home");
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado.";
                return RedirectToAction("Marketplace", "Home");
            }

            var offersRoomRental = address.Coworking?.Products?.Any(p => p.ProductType == "RoomRental" && p.IsActive) ?? false;
            if (!offersRoomRental)
            {
                TempData["Error"] = "Este coworking não oferece aluguel de salas.";
                return RedirectToAction("MarketplaceDetails", "Home", new { id });
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
            ViewBag.GetAvailabilityForMonthUrl = Url.Action("GetAvailabilityForMonth", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservations(int CoworkingAddressId, string RoomType, string slotsJson)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                TempData["Error"] = "Cliente não encontrado.";
                return RedirectToAction("Marketplace", "Home");
            }

            List<ReservationSlotDto>? slots;
            try
            {
                slots = System.Text.Json.JsonSerializer.Deserialize<List<ReservationSlotDto>>(slotsJson);
            }
            catch
            {
                TempData["Error"] = "Dados das reservas inválidos.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
            }
            if (slots == null || !slots.Any())
            {
                TempData["Error"] = "Selecione ao menos uma data e horário.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
            }

            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(a => a.Id == CoworkingAddressId);
            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado.";
                return RedirectToAction("Marketplace", "Home");
            }

            var offersRoomRental = await _context.CoworkingProducts
                .AnyAsync(p => p.CoworkingId == address.CoworkingId && p.ProductType == "RoomRental" && p.IsActive);
            if (!offersRoomRental)
            {
                TempData["Error"] = "Este coworking não oferece aluguel de salas.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
            }

            if (RoomType != "Shared" && RoomType != "Private")
            {
                TempData["Error"] = "Tipo de sala inválido.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
            }
            if (RoomType == "Shared" && !address.OffersSharedRoom) { TempData["Error"] = "Este endereço não oferece sala compartilhada."; return RedirectToAction("BookRoom", new { id = CoworkingAddressId }); }
            if (RoomType == "Private" && !address.OffersPrivateRoom) { TempData["Error"] = "Este endereço não oferece sala privativa."; return RedirectToAction("BookRoom", new { id = CoworkingAddressId }); }

            decimal pricePerHour = RoomType == "Shared" ? (address.SharedRoomPricePerHour ?? 0) : (address.PrivateRoomPricePerHour ?? 0);
            int maxSeats = RoomType == "Shared" ? (address.SharedRoomMaxSeats ?? 0) : (address.PrivateRoomMaxSeats ?? 0);
            if (pricePerHour <= 0)
            {
                TempData["Error"] = "Preço por hora não configurado para este tipo de sala.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
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
                TempData["Error"] = "Nenhum horário válido. Verifique datas e horários.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
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
                TempData["Error"] = $"Créditos insuficientes. Necessário: {totalCredits}, disponível: {availableCredits}.";
                return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
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
                            r.ReservationDate.Date == Date && r.RoomType == RoomType && r.Status != "Cancelled" &&
                            r.StartTime.HasValue && r.EndTime.HasValue && !(r.EndTime <= Start || r.StartTime >= End));
                }
                if (hasConflict)
                {
                    TempData["Error"] = "Você já possui uma reserva neste horário ou há sobreposição entre os horários selecionados. Não é permitida duplicidade.";
                    return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
                }
                // (3) Conflito com outras reservas: sala privativa = qualquer sobreposição; compartilhada = lotação
                if (RoomType == "Private")
                {
                    hasConflict = await _context.Reservations
                        .AnyAsync(r => r.CoworkingAddressId == CoworkingAddressId && r.ReservationDate.Date == Date &&
                            r.RoomType == "Private" && r.Status != "Cancelled" && r.StartTime.HasValue && r.EndTime.HasValue &&
                            !(r.EndTime <= Start || r.StartTime >= End));
                }
                else
                {
                    var countDb = await _context.Reservations
                        .CountAsync(r => r.CoworkingAddressId == CoworkingAddressId && r.ReservationDate.Date == Date &&
                            r.RoomType == "Shared" && r.Status != "Cancelled" && r.StartTime.HasValue && r.EndTime.HasValue &&
                            !(r.EndTime <= Start || r.StartTime >= End));
                    var countAdded = addedInRequest.Count(t => t.Date == Date && !(t.End <= Start || t.Start >= End));
                    hasConflict = maxSeats > 0 && (countDb + countAdded >= maxSeats);
                }
                if (hasConflict)
                {
                    TempData["Error"] = $"O horário {Date:dd/MM/yyyy} {Start:hh\\:mm}-{End:hh\\:mm} já está reservado. Escolha outro horário.";
                    return RedirectToAction("BookRoom", new { id = CoworkingAddressId });
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
                    Status = "Confirmed",
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

            TempData["Success"] = toCreate.Count == 1
                ? $"Reserva confirmada para {toCreate[0].Date:dd/MM/yyyy}!"
                : $"{toCreate.Count} reservas confirmadas com sucesso!";
            return RedirectToAction("MyReservations");
        }

        [HttpGet]
        public async Task<IActionResult> MyReservations(string? sortBy = "date_asc", string? status = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string? search = null, string? roomType = null, int page = 1, int pageSize = 12)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("Dashboard");
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
                    if (s == "Confirmed" || s == "Completed" || s == "Cancelled")
                        query = query.Where(r => r.Status == s);
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
                ViewBag.TotalConfirmed = allReservations.Count(r => r.Status == "Confirmed");
                ViewBag.TotalCompleted = allReservations.Count(r => r.Status == "Completed");
                ViewBag.TotalCancelled = allReservations.Count(r => r.Status == "Cancelled");

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
                TempData["Error"] = $"Erro ao carregar reservas: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        /// <summary>
        /// Cliente confirma que realizou/utilizou a reserva. Após dupla confirmação (cliente + coworking),
        /// o sistema repassa os créditos ao coworking e aplica a comissão da plataforma.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservationUsed(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) { TempData["Error"] = "Cliente não encontrado."; return RedirectToAction("MyReservations"); }

            var r = await _context.Reservations.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == client.Id);
            if (r == null) { TempData["Error"] = "Reserva não encontrada."; return RedirectToAction("MyReservations"); }
            if (string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) { TempData["Error"] = "Reserva cancelada."; return RedirectToAction("MyReservations"); }
            if (string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase)) { TempData["Success"] = "Reserva já concluída."; return RedirectToAction("MyReservations"); }

            if (r.ClientConfirmedAt == null)
            {
                r.ClientConfirmedAt = DateTimeHelper.GetBrazilianTime();
                r.UpdatedAt = DateTimeHelper.GetBrazilianTime();
                await _context.SaveChangesAsync();
            }
            await _reservationTransferService.ExecuteTransferIfBothConfirmedAsync(r);
            TempData["Success"] = "Confirmação registrada. Após o coworking confirmar seu comparecimento, o pagamento será processado.";
            return RedirectToAction("MyReservations");
        }

        [HttpGet]
        public async Task<IActionResult> CreditSpendingReport(int? month = null, int? year = null)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("Dashboard");
                }

                // Buscar todas as reservas do cliente (gastos de créditos)
                var allReservations = await _context.Reservations
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Location)
                    .Where(r => r.ClientId == client.Id)
                    .OrderByDescending(r => r.ReservationDate)
                    .ThenByDescending(r => r.CreatedAt)
                    .ToListAsync();

                // Aplicar filtro por mês/ano se fornecido
                var reservations = allReservations;
                if (month.HasValue && year.HasValue)
                {
                    reservations = allReservations
                        .Where(r => r.ReservationDate.Year == year.Value && r.ReservationDate.Month == month.Value)
                        .ToList();
                }

                // Calcular totais (sempre baseado em todas as reservas para os cards de resumo)
                var totalValueSpent = allReservations.Sum(r => r.TotalValue);
                var totalReservations = allReservations.Count;
                var confirmedReservations = allReservations.Count(r => r.Status == "Confirmed");
                var cancelledReservations = allReservations.Count(r => r.Status == "Cancelled");
                var completedReservations = allReservations.Count(r => r.Status == "Completed");

                // Calcular totais filtrados (para exibição na tabela quando filtrado)
                var filteredValueSpent = reservations.Sum(r => r.TotalValue);
                var filteredReservations = reservations.Count;

                // Agrupar por mês (sempre de todas as reservas para a tabela de meses)
                var monthlySpending = allReservations
                    .GroupBy(r => new { r.ReservationDate.Year, r.ReservationDate.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Value = g.Sum(r => r.TotalValue),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .ToList();

                ViewBag.TotalValueSpent = totalValueSpent;
                ViewBag.TotalReservations = totalReservations;
                ViewBag.ConfirmedReservations = confirmedReservations;
                ViewBag.CancelledReservations = cancelledReservations;
                ViewBag.CompletedReservations = completedReservations;
                ViewBag.MonthlySpending = monthlySpending;
                ViewBag.FilteredValueSpent = filteredValueSpent;
                ViewBag.FilteredReservations = filteredReservations;
                ViewBag.SelectedMonth = month;
                ViewBag.SelectedYear = year;

                return View(reservations);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao carregar relatório: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }
    }
}
