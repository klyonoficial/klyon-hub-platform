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
        private readonly ApplicationDbContext _context;

        public ClientController(
            IContractService contractService,
            ICoworkingService coworkingService,
            ISubscriptionService subscriptionService,
            IPaymentService paymentService,
            IDocumentService documentService,
            ApplicationDbContext context)
        {
            _contractService = contractService;
            _coworkingService = coworkingService;
            _subscriptionService = subscriptionService;
            _paymentService = paymentService;
            _documentService = documentService;
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
            return View(locations);
        }

        [HttpGet]
        public async Task<IActionResult> AvailableAddresses(int locationId)
        {
            var addresses = await _coworkingService.GetAvailableAddressesAsync(locationId);
            ViewBag.LocationId = locationId;
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
                if (payment.Method == PaymentMethod.PIX && string.IsNullOrEmpty(payment.PixCode))
                {
                    await _paymentService.GeneratePixCodeAsync(payment.Id);
                    payment = await _paymentService.GetPaymentByIdAsync(id);
                }
                else if (payment.Method == PaymentMethod.BankTransfer && string.IsNullOrEmpty(payment.BoletoCode))
                {
                    await _paymentService.GenerateBoletoCodeAsync(payment.Id);
                    payment = await _paymentService.GetPaymentByIdAsync(id);
                }
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
    }
}
