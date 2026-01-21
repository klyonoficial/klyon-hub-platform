using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IO;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
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

        public CoworkingController(
            ICoworkingService coworkingService,
            ICommissionService commissionService,
            IPaymentService paymentService,
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            IAddressDeclarationService addressDeclarationService)
        {
            _coworkingService = coworkingService;
            _commissionService = commissionService;
            _paymentService = paymentService;
            _context = context;
            _storageFactory = storageFactory;
            _addressDeclarationService = addressDeclarationService;
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

            ViewBag.Coworking = coworkingWithAddresses ?? coworkingEntity;
            ViewBag.ActiveContracts = contracts.Count(c => c.Status == ContractStatus.Active);
            ViewBag.PendingCommissions = totalPending;
            ViewBag.Commissions = commissions;
            ViewBag.TotalAddresses = coworkingWithAddresses?.Addresses?.Count ?? 0;
            ViewBag.PendingPaymentsCount = pendingPaymentsCount;
            ViewBag.PendingReservationsCount = pendingReservationsCount;

            return View();
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
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                coworking.UserId = userId;
                coworking.IsActive = true;
                coworking.IsPartner = true;

                var createdCoworking = await _coworkingService.CreateAsync(coworking);
                var storageService = await _storageFactory.GetStorageServiceAsync();

                // Processar fotos
                if (Photos != null && Photos.Count > 0)
                {
                    var photoCount = 0;
                    foreach (var photo in Photos.Take(10)) // Máximo 10 fotos
                    {
                        if (photo.Length > 0)
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

                await _context.SaveChangesAsync();

                TempData["Success"] = "Coworking cadastrado com sucesso!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar coworking: {ex.Message}";
                return View(coworking);
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
            string neighborhood, string city, string state, string zipCode, decimal basePrice, int maxContracts,
            bool offersSharedRoom = false, decimal? sharedRoomPricePerHour = null, int? sharedRoomMaxSeats = null,
            bool offersPrivateRoom = false, decimal? privateRoomPricePerHour = null, int? privateRoomMaxSeats = null)
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

                // Validar configurações de salas
                if (offersSharedRoom && (!sharedRoomPricePerHour.HasValue || sharedRoomPricePerHour.Value <= 0))
                {
                    TempData["Error"] = "Por favor, informe o valor por hora da sala compartilhada.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    return View(locations);
                }

                if (offersSharedRoom && (!sharedRoomMaxSeats.HasValue || sharedRoomMaxSeats.Value < 1))
                {
                    TempData["Error"] = "Por favor, informe a quantidade de carteiras da sala compartilhada.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    return View(locations);
                }

                if (offersPrivateRoom && (!privateRoomPricePerHour.HasValue || privateRoomPricePerHour.Value <= 0))
                {
                    TempData["Error"] = "Por favor, informe o valor por hora da sala privativa.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    return View(locations);
                }

                if (offersPrivateRoom && (!privateRoomMaxSeats.HasValue || privateRoomMaxSeats.Value < 1))
                {
                    TempData["Error"] = "Por favor, informe a quantidade de carteiras da sala privativa.";
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
                    CurrentContracts = 0,
                    OffersSharedRoom = offersSharedRoom,
                    SharedRoomPricePerHour = offersSharedRoom ? sharedRoomPricePerHour : null,
                    SharedRoomMaxSeats = offersSharedRoom ? sharedRoomMaxSeats : null,
                    OffersPrivateRoom = offersPrivateRoom,
                    PrivateRoomPricePerHour = offersPrivateRoom ? privateRoomPricePerHour : null,
                    PrivateRoomMaxSeats = offersPrivateRoom ? privateRoomMaxSeats : null
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
            string neighborhood, string city, string state, string zipCode, decimal basePrice, int maxContracts, bool isAvailable,
            bool offersSharedRoom = false, decimal? sharedRoomPricePerHour = null, int? sharedRoomMaxSeats = null,
            bool offersPrivateRoom = false, decimal? privateRoomPricePerHour = null, int? privateRoomMaxSeats = null)
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

                // Validar configurações de salas
                if (offersSharedRoom && (!sharedRoomPricePerHour.HasValue || sharedRoomPricePerHour.Value <= 0))
                {
                    TempData["Error"] = "Por favor, informe o valor por hora da sala compartilhada.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                if (offersSharedRoom && (!sharedRoomMaxSeats.HasValue || sharedRoomMaxSeats.Value < 1))
                {
                    TempData["Error"] = "Por favor, informe a quantidade de carteiras da sala compartilhada.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                if (offersPrivateRoom && (!privateRoomPricePerHour.HasValue || privateRoomPricePerHour.Value <= 0))
                {
                    TempData["Error"] = "Por favor, informe o valor por hora da sala privativa.";
                    var locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                    ViewBag.Locations = locations;
                    ViewBag.Address = existingAddress;
                    return View(existingAddress);
                }

                if (offersPrivateRoom && (!privateRoomMaxSeats.HasValue || privateRoomMaxSeats.Value < 1))
                {
                    TempData["Error"] = "Por favor, informe a quantidade de carteiras da sala privativa.";
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
                existingAddress.OffersSharedRoom = offersSharedRoom;
                existingAddress.SharedRoomPricePerHour = offersSharedRoom ? sharedRoomPricePerHour : null;
                existingAddress.SharedRoomMaxSeats = offersSharedRoom ? sharedRoomMaxSeats : null;
                existingAddress.OffersPrivateRoom = offersPrivateRoom;
                existingAddress.PrivateRoomPricePerHour = offersPrivateRoom ? privateRoomPricePerHour : null;
                existingAddress.PrivateRoomMaxSeats = offersPrivateRoom ? privateRoomMaxSeats : null;
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

            // Query base - por padrão mostra apenas ativos se não houver filtro de status
            var query = _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingEntity.Id)
                .AsQueryable();

            // Se não houver filtro de status, mostrar apenas ativos por padrão
            if (string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == ContractStatus.Active);
            }
            else if (Enum.TryParse<ContractStatus>(status, out var contractStatus))
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
        public async Task<IActionResult> Reservations(
            string? search = null,
            string? status = null,
            int? addressId = null,
            DateTime? allocationDateFrom = null,
            DateTime? allocationDateTo = null,
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
                ["CreditsUsed"] = (object?)r.CreditsUsed ?? DBNull.Value
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
                ["CreditsUsed"] = DBNull.Value
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
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == coworkingEntity.Id);

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
            }

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
                // Manter a aba ativa após salvar
                var activeTab = Request.Form["activeTab"].ToString();
                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData["ActiveTab"] = activeTab;
                }
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar dados: {ex.Message}";
                // Manter a aba ativa mesmo em caso de erro
                var activeTab = Request.Form["activeTab"].ToString();
                if (!string.IsNullOrEmpty(activeTab))
                {
                    TempData["ActiveTab"] = activeTab;
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

                await _context.SaveChangesAsync();

                TempData["Success"] = "Produtos atualizados com sucesso!";
                TempData["ActiveTab"] = "products";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar produtos: {ex.Message}";
                TempData["ActiveTab"] = "products";
                return RedirectToAction("Profile");
            }
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
                await _context.SaveChangesAsync();

                // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                var previousPaidPayments = await _context.Payments
                    .Where(p => p.SubscriptionId == payment.SubscriptionId && 
                               p.Id != paymentId && 
                               p.Status == PaymentStatus.Paid)
                    .CountAsync();

                var isFirstPayment = previousPaidPayments == 0;

                // Ativar contrato
                contract.Status = ContractStatus.Active;
                contract.UpdatedAt = DateTime.UtcNow;
                
                // Se for o primeiro pagamento, definir validade de 1 ano a partir do pagamento
                if (isFirstPayment && contract.ExpiryDate == null)
                {
                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                }
                
                await _context.SaveChangesAsync();

                // Atualizar contador de contratos do endereço
                if (contract.CoworkingAddress != null)
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

                // Verificar limite de fotos (máximo 10)
                var existingPhotosCount = await _context.CoworkingPhotos
                    .Where(p => p.CoworkingId == coworkingEntity.Id)
                    .CountAsync();

                var photosToAdd = Photos.Take(10 - existingPhotosCount);
                if (!photosToAdd.Any())
                {
                    TempData["Error"] = "Você já atingiu o limite máximo de 10 fotos.";
                    return RedirectToAction("Profile");
                }

                var storageService = await _storageFactory.GetStorageServiceAsync();
                var coworkingFolder = $"coworkings/{coworkingEntity.Id}/photos";
                var photoCount = existingPhotosCount;

                foreach (var photo in photosToAdd)
                {
                    if (photo.Length > 0 && photo.Length <= 5 * 1024 * 1024) // 5MB
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
        public async Task<IActionResult> AddPartner(CoworkingPartner partner)
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

                partner.CoworkingId = coworkingEntity.Id;
                partner.CreatedAt = DateTime.UtcNow;

                _context.CoworkingPartners.Add(partner);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Sócio adicionado com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar sócio: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePartner(CoworkingPartner partner)
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
                existingPartner.SharePercentage = partner.SharePercentage;
                existingPartner.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Sócio atualizado com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar sócio: {ex.Message}";
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

                _context.CoworkingPartners.Remove(partner);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Sócio excluído com sucesso!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao excluir sócio: {ex.Message}";
                return RedirectToAction("Profile");
            }
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
                    
                    // Definir data de expiração para comprovante de endereço (90 dias)
                    if (docType == CoworkingDocumentType.AddressProof)
                    {
                        existingDoc.ExpiresAt = DateTime.UtcNow.AddDays(90);
                    }
                }
                else
                {
                    // Criar novo documento
                    var coworkingDocument = new CoworkingDocument
                    {
                        CoworkingId = coworkingEntity.Id,
                        Type = docType,
                        FileName = documentFile.FileName,
                        FilePath = filePath,
                        IsValid = true,
                        ExpiresAt = docType == CoworkingDocumentType.AddressProof 
                            ? DateTime.UtcNow.AddDays(90) 
                            : null
                    };

                    _context.CoworkingDocuments.Add(coworkingDocument);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Documento enviado com sucesso!";
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
    }
}
