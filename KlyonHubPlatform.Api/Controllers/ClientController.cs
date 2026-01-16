using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> RegisterClient(Client client)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            
            // Sempre usar o email do usuário logado
            client.ContactEmail = userEmail;
            client.UserId = userId;
            client.IsActive = true;

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard");
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
        public async Task<IActionResult> CreateContract(int coworkingAddressId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return RedirectToAction("RegisterClient");
            }

            var address = await _coworkingService.GetByIdAsync(
                (await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == coworkingAddressId))?.CoworkingId ?? 0
            );

            var addressEntity = await _context.CoworkingAddresses
                .FirstOrDefaultAsync(a => a.Id == coworkingAddressId);

            if (addressEntity == null)
            {
                return RedirectToAction("AvailableAddresses", new { locationId = addressEntity?.LocationId });
            }

            try
            {
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
                        return RedirectToAction("PaymentDetails", new { id = payment.Id });
                    }
                }

                return RedirectToAction("ContractDetails", new { id = contract.Id });
            }
            catch (Exception ex)
            {
                ViewData["Error"] = ex.Message;
                return RedirectToAction("AvailableAddresses", new { locationId = addressEntity.LocationId });
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

            // Gerar código PIX se necessário
            if (payment.Method == PaymentMethod.PIX && string.IsNullOrEmpty(payment.PixCode))
            {
                await _paymentService.GeneratePixCodeAsync(payment.Id);
                payment = await _paymentService.GetPaymentByIdAsync(id);
            }

            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int paymentId)
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

                // Confirmar pagamento (simula confirmação - em produção viria do webhook)
                await _paymentService.ConfirmPaymentAsync(paymentId, Guid.NewGuid().ToString());

                TempData["Success"] = "Pagamento confirmado! Seu contrato foi ativado.";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao confirmar pagamento: {ex.Message}";
                return RedirectToAction("PaymentDetails", new { id = paymentId });
            }
        }
    }
}
