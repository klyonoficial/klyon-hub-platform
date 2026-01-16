using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KlyonEF.Services;
using KlyonEF.Models;
using KlyonEF.Data;
using Microsoft.EntityFrameworkCore;

namespace KlyonEF.Controllers
{
    [Authorize(Roles = "CoworkingPartner,Administrator")]
    public class CoworkingController : Controller
    {
        private readonly ICoworkingService _coworkingService;
        private readonly ICommissionService _commissionService;
        private readonly ApplicationDbContext _context;

        public CoworkingController(
            ICoworkingService coworkingService,
            ICommissionService commissionService,
            ApplicationDbContext context)
        {
            _coworkingService = coworkingService;
            _commissionService = commissionService;
            _context = context;
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
            var totalPending = commissions.Sum(c => c.Amount);

            ViewBag.Coworking = coworkingWithAddresses ?? coworkingEntity;
            ViewBag.ActiveContracts = contracts.Count(c => c.Status == ContractStatus.Active);
            ViewBag.PendingCommissions = totalPending;
            ViewBag.Commissions = commissions;
            ViewBag.TotalAddresses = coworkingWithAddresses?.Addresses?.Count ?? 0;

            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(Coworking coworking)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            coworking.UserId = userId;
            coworking.IsActive = true;
            coworking.IsPartner = true;

            await _coworkingService.CreateAsync(coworking);
            return RedirectToAction("Dashboard");
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

            return View(addresses);
        }

        [HttpGet]
        public async Task<IActionResult> AddAddress()
        {
            var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
            ViewBag.Locations = locations;
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

                // Validar se a localidade existe
                var location = await _context.Locations.FindAsync(locationId);
                if (location == null)
                {
                    TempData["Error"] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    return View(locations);
                }

                // Validar preço base (não pode ser negativo)
                if (basePrice < 0)
                {
                    TempData["Error"] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    return View(locations);
                }

                // Criar objeto CoworkingAddress
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
                    IsAvailable = true,
                    CurrentContracts = 0
                };

                await _coworkingService.CreateAddressAsync(coworkingAddress);
                
                TempData["Success"] = "Endereço cadastrado com sucesso!";
                return RedirectToAction("MyAddresses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar endereço: {ex.Message}";
                var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                ViewBag.Locations = locations;
                return View(locations);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditAddress(int id)
        {
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
                .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);

            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                return RedirectToAction("MyAddresses");
            }

            var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
            ViewBag.Locations = locations;
            ViewBag.Address = address;

            return View(address);
        }

        [HttpPost]
        public async Task<IActionResult> EditAddress(int id, int locationId, string address, string number, string? complement,
            string neighborhood, string city, string state, string zipCode, decimal basePrice, int maxContracts, bool isAvailable)
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
                    .FirstOrDefaultAsync(a => a.Id == id && a.CoworkingId == coworkingEntity.Id);

                if (existingAddress == null)
                {
                    TempData["Error"] = "Endereço não encontrado ou você não tem permissão para editá-lo.";
                    return RedirectToAction("MyAddresses");
                }

                // Validar se a localidade existe
                var location = await _context.Locations.FindAsync(locationId);
                if (location == null)
                {
                    TempData["Error"] = "Localidade selecionada não encontrada. Por favor, selecione uma localidade válida.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Validar preço base (não pode ser negativo)
                if (basePrice < 0)
                {
                    TempData["Error"] = "O preço base não pode ser negativo. Por favor, informe um valor maior ou igual a zero.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Validar maxContracts (não pode ser menor que currentContracts)
                if (maxContracts < existingAddress.CurrentContracts)
                {
                    TempData["Error"] = $"O máximo de contratos não pode ser menor que o número de contratos ativos ({existingAddress.CurrentContracts}).";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                // Atualizar o endereço
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
                existingAddress.IsAvailable = isAvailable;
                existingAddress.UpdatedAt = DateTime.UtcNow;

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
                var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                ViewBag.Locations = locations;
                ViewBag.Address = existingAddress;
                return View(existingAddress);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ActiveContracts()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var contracts = await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingEntity.Id && c.Status == ContractStatus.Active)
                .ToListAsync();

            return View(contracts);
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

            return View(commissions);
        }

        [HttpGet]
        public async Task<IActionResult> Transfers()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var transfers = await _context.Transfers
                .Where(t => t.CoworkingId == coworkingEntity.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(transfers);
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

            var pendingCommissions = await _commissionService.GetPendingCommissionsAsync(coworkingEntity.Id);
            if (pendingCommissions.Count == 0)
            {
                TempData["Error"] = "Não há comissões pendentes para transferência";
                return RedirectToAction("Commissions");
            }

            var commissionIds = pendingCommissions.Select(c => c.Id).ToList();
            await _commissionService.CreateTransferAsync(coworkingEntity.Id, commissionIds);

            TempData["Success"] = $"Repasse de R$ {pendingCommissions.Sum(c => c.Amount):N2} solicitado com sucesso!";
            return RedirectToAction("Transfers");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var coworking = await _coworkingService.GetByUserIdAsync(userId);
            var coworkingEntity = coworking.FirstOrDefault();

            if (coworkingEntity == null)
            {
                return RedirectToAction("Register");
            }

            var coworkingWithDetails = await _context.Coworkings
                .Include(c => c.Photos)
                .Include(c => c.Partners)
                .FirstOrDefaultAsync(c => c.Id == coworkingEntity.Id);

            return View(coworkingWithDetails ?? coworkingEntity);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Coworking coworking)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var existingCoworking = await _coworkingService.GetByUserIdAsync(userId);
                var coworkingEntity = existingCoworking.FirstOrDefault();

                if (coworkingEntity == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Register");
                }

                // Atualizar dados básicos
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
                coworkingEntity.UpdatedAt = DateTime.UtcNow;

                await _coworkingService.UpdateAsync(coworkingEntity);

                TempData["Success"] = "Dados do coworking atualizados com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar dados: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }
    }
}
