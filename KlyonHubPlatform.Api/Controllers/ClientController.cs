using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = "Client,Administrator")]
    public class ClientController : Controller
    {
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentService _paymentService;
        private readonly IDocumentService _documentService;
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly ApplicationDbContext _context;

        public ClientController(
            IContractService contractService,
            ICoworkingService coworkingService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IDocumentService documentService,
            IAddressDeclarationService addressDeclarationService,
            ApplicationDbContext context)
        {
            _contractService = contractService;
            _coworkingService = coworkingService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _documentService = documentService;
            _addressDeclarationService = addressDeclarationService;
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.CoworkingAddress)
                .Include(c => c.Contracts)
                    .ThenInclude(ct => ct.Subscription)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            ViewBag.Client = client;
            return View();
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
            
            return View(client);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Client client)
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
                var cnpjClean = client.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var cpfClean = client.CPF?.Replace(".", "").Replace("-", "").Trim();

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
                        ViewBag.UserEmail = userEmail;
                        return View(existingClient);
                    }
                    // Salvar CNPJ com máscara
                    existingClient.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                }
                else if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length != 14)
                {
                    TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos.";
                    ViewBag.UserEmail = userEmail;
                    return View(existingClient);
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
                        ViewBag.UserEmail = userEmail;
                        return View(existingClient);
                    }
                    // Salvar CPF com máscara
                    existingClient.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                }
                else if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length != 11)
                {
                    TempData["Error"] = "CPF inválido. O CPF deve conter 11 dígitos.";
                    ViewBag.UserEmail = userEmail;
                    return View(existingClient);
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
                existingClient.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Perfil atualizado com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData["Error"] = "Este CNPJ ou CPF já está cadastrado no sistema. Verifique os dados informados.";
                ViewBag.UserEmail = userEmail;
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                return View(existingClient);
            }
            catch (Exception ex)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                TempData["Error"] = $"Erro ao atualizar perfil: {ex.Message}";
                ViewBag.UserEmail = userEmail;
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                return View(existingClient);
            }
        }

        [HttpGet]
        public async Task<IActionResult> RegisterClient()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.UserEmail = userEmail;
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
                
                // Verificar se o usuário já possui um cliente cadastrado
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (existingClient != null)
                {
                    TempData["Error"] = "Você já possui um cadastro de cliente. Acesse seu perfil para editar as informações.";
                    return RedirectToAction("Dashboard");
                }

                // Remover máscaras do CNPJ e CPF para validação
                var cnpjClean = client.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                var cpfClean = client.CPF?.Replace(".", "").Replace("-", "").Trim();

                // Validar CNPJ se informado
                if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length == 14)
                {
                    // Buscar todos os clientes e comparar CNPJ sem máscara
                    var allClients = await _context.Clients.Where(c => c.CNPJ != null).ToListAsync();
                    var existingCNPJ = allClients.FirstOrDefault(c => 
                        c.CNPJ != null && 
                        c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
                    
                    if (existingCNPJ != null)
                    {
                        TempData["Error"] = $"O CNPJ {client.CNPJ} já está cadastrado no sistema.";
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CNPJ com máscara
                    client.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                }
                else if (!string.IsNullOrEmpty(cnpjClean) && cnpjClean.Length != 14)
                {
                    TempData["Error"] = "CNPJ inválido. O CNPJ deve conter 14 dígitos.";
                    ViewBag.UserEmail = userEmail;
                    return View(client);
                }

                // Validar CPF se informado
                if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length == 11)
                {
                    // Buscar todos os clientes e comparar CPF sem máscara
                    var allClients = await _context.Clients.Where(c => c.CPF != null).ToListAsync();
                    var existingCPF = allClients.FirstOrDefault(c => 
                        c.CPF != null && 
                        c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
                    
                    if (existingCPF != null)
                    {
                        TempData["Error"] = $"O CPF {client.CPF} já está cadastrado no sistema.";
                        ViewBag.UserEmail = userEmail;
                        return View(client);
                    }
                    // Salvar CPF com máscara
                    client.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                }
                else if (!string.IsNullOrEmpty(cpfClean) && cpfClean.Length != 11)
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
        public async Task<IActionResult> ChooseLocation()
        {
            var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
            
            // Verificar quais localidades têm endereços disponíveis
            var locationIds = locations.Select(l => l.Id).ToList();
            var availableAddressesCount = await _context.CoworkingAddresses
                .Where(a => a.IsAvailable && a.CurrentContracts < a.MaxContracts && locationIds.Contains(a.LocationId))
                .GroupBy(a => a.LocationId)
                .Select(g => new { LocationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LocationId, x => x.Count);
            
            ViewBag.AvailableAddressesCount = availableAddressesCount;
            
            return View(locations);
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
                    c.CoworkingAddress.City.Contains(search));
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
                contract.CancellationRequestedAt = DateTime.UtcNow;
                contract.CancellationRequestReason = reason;
                contract.UpdatedAt = DateTime.UtcNow;
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
            var payment = await _paymentService.GetPaymentByIdAsync(id);
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

            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == payment.Subscription.ContractId);
            
            if (contract == null || contract.ClientId != client.Id)
            {
                if (!User.IsInRole("Administrator"))
                {
                    return Forbid();
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
                var fileName = $"receipt_{paymentId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var receiptPath = await storageService.SaveFileAsync(fileBytes, fileName, $"payments/{paymentId}/receipts");

                // Atualizar pagamento para aguardando validação
                payment.Status = PaymentStatus.AwaitingValidation;
                payment.ReceiptFilePath = receiptPath;
                payment.UpdatedAt = DateTime.UtcNow;
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
            var contentType = fileName.EndsWith(".pdf") ? "application/pdf" : "image/jpeg";
            
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
                var now = DateTime.UtcNow;
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
    }
}
