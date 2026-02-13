using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KlyonHubPlatform.Api.ViewModels;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Api.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISystemConfigurationService _systemConfig;

        public HomeController(ApplicationDbContext context, ISystemConfigurationService systemConfig)
        {
            _context = context;
            _systemConfig = systemConfig;
        }

        /// <summary>
        /// Página principal: escolha entre Endereço Fiscal ou Aluguel de Salas.
        /// Se Features.EnableCoworkingMenus for false, redireciona direto para a página de endereço fiscal.
        /// </summary>
        /// <summary>
        /// Página principal: escolha entre Endereço Fiscal, Aluguel de Salas (se habilitado) e Quero ser Parceiro.
        /// Admin, Parceiro e Cliente podem acessar normalmente sem redirecionamento.
        /// </summary>
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData["EnableCoworkingMenus"] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            return View();
        }

        /// <summary>
        /// Página comercial de contratação de endereço fiscal. Cliente logado vai para ChooseLocation.
        /// </summary>
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> FiscalAddress(CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                return RedirectToAction(ActionNames.ChooseLocation, ControllerNames.Client);
            }
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData["EnableCoworkingMenus"] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            return View();
        }

        /// <summary>
        /// Commercial page for room rental (meeting, consulting, coworking). Redirects to Marketplace.
        /// </summary>
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Rooms(CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            if (StringBooleanValue.IsFalse(enableCoworkingMenus))
            {
                return RedirectToAction(ActionNames.Index);
            }
            ViewData["EnableCoworkingMenus"] = true;
            return View();
        }

        /// <summary>
        /// Commercial page for becoming a partner (coworking / corporate space).
        /// </summary>
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Partner(CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.CoworkingPartner))
            {
                return RedirectToAction(ActionNames.Dashboard, ControllerNames.Coworking);
            }
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData["EnableCoworkingMenus"] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Marketplace([FromQuery] MarketplaceSearchVm search, CancellationToken cancellationToken = default)
        {
            // Normalização
            search.Page = search.Page <= 0 ? 1 : search.Page;
            search.PageSize = 10;

            var baseQuery = _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Photos)
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .Where(a => a.Coworking.IsActive && 
                           a.Coworking.IsPartner &&
                           a.Coworking.ApprovalStatus == KlyonHubPlatform.Domain.Core.Enums.CoworkingApprovalStatus.Approved &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom)) // Apenas endereços aprovados que oferecem salas
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search.State))
            {
                var uf = search.State.Trim().ToUpper();
                baseQuery = baseQuery.Where(a => a.State.ToUpper() == uf);
                search.State = uf;
            }

            if (!string.IsNullOrWhiteSpace(search.City))
            {
                var city = search.City.Trim();
                baseQuery = baseQuery.Where(a => a.City == city);
                search.City = city;
            }

            if (!string.IsNullOrWhiteSpace(search.Neighborhood))
            {
                var neighborhood = search.Neighborhood.Trim();
                baseQuery = baseQuery.Where(a => a.Neighborhood.Contains(neighborhood));
            }

            // Filtro por capacidade mínima (usando MaxContracts como referência)
            if (search.MinPeople.HasValue && search.MinPeople.Value > 0)
            {
                baseQuery = baseQuery.Where(a => a.MaxContracts >= search.MinPeople.Value);
            }

            // Filtro por capacidade máxima
            if (search.MaxPeople.HasValue && search.MaxPeople.Value > 0)
            {
                baseQuery = baseQuery.Where(a => a.MaxContracts <= search.MaxPeople.Value);
            }

            // Filtros (UF/Cidade) dinamicamente baseados no catálogo total (apenas endereços que oferecem salas)
            var allStates = await _context.CoworkingAddresses
                .Where(a => a.Coworking.IsActive && 
                           a.Coworking.IsPartner &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom))
                .Select(a => a.State)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var citiesQuery = _context.CoworkingAddresses
                .Where(a => a.Coworking.IsActive && 
                           a.Coworking.IsPartner &&
                           a.Coworking.ApprovalStatus == KlyonHubPlatform.Domain.Core.Enums.CoworkingApprovalStatus.Approved &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom));
            if (!string.IsNullOrWhiteSpace(search.State))
            {
                citiesQuery = citiesQuery.Where(a => a.State.ToUpper() == search.State!.ToUpper());
            }
            var allCities = await citiesQuery
                .Select(a => a.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var totalItems = await baseQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)search.PageSize);
            if (totalPages == 0) totalPages = 1;
            if (search.Page > totalPages) search.Page = totalPages;

            var addresses = await baseQuery
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Photos)
                .OrderByDescending(a => a.IsAvailable)
                .ThenBy(a => a.City)
                .ThenBy(a => a.Neighborhood)
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .ToListAsync();

            // Verificar se o coworking oferece produto RoomRental (opcional, para compatibilidade)
            var coworkingIds = addresses.Select(a => a.CoworkingId).Distinct().ToList();
            var productsByCoworking = await _context.CoworkingProducts
                .Where(p => coworkingIds.Contains(p.CoworkingId) && 
                           p.ProductType == "RoomRental" && 
                           p.IsActive)
                .Select(p => p.CoworkingId)
                .Distinct()
                .ToListAsync();

            var items = addresses.Select(a =>
            {
                var mainPhoto = a.Coworking.Photos?.FirstOrDefault(p => p.IsMain)
                    ?? a.Coworking.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault();

                // Calcular capacidade máxima baseada nas salas oferecidas
                var maxCapacity = 0;
                if (a.OffersSharedRoom && a.SharedRoomMaxSeats.HasValue)
                {
                    maxCapacity = Math.Max(maxCapacity, a.SharedRoomMaxSeats.Value);
                }
                if (a.OffersPrivateRoom && a.PrivateRoomMaxSeats.HasValue)
                {
                    maxCapacity = Math.Max(maxCapacity, a.PrivateRoomMaxSeats.Value);
                }
                // Se não tiver configuração de salas, usar MaxContracts como fallback
                if (maxCapacity == 0)
                {
                    maxCapacity = a.MaxContracts;
                }

                return new MarketplaceCardVm
                {
                    CoworkingAddressId = a.Id,
                    Title = $"Endereço em {a.City}",
                    Subtitle = a.Neighborhood,
                    CityState = $"{a.City}/{a.State}",
                    BasePrice = a.BasePrice,
                    IsAvailable = a.IsAvailable && a.CurrentContracts < a.MaxContracts,
                    CurrentContracts = a.CurrentContracts,
                    MaxContracts = a.MaxContracts,
                    MaxPeople = maxCapacity,
                    MainPhotoId = mainPhoto?.Id,
                    OffersSharedRoom = a.OffersSharedRoom,
                    SharedRoomPricePerHour = a.SharedRoomPricePerHour,
                    OffersPrivateRoom = a.OffersPrivateRoom,
                    PrivateRoomPricePerHour = a.PrivateRoomPricePerHour
                };
            }).ToList();

            // Criar dicionário para verificar se cada endereço tem aluguel de sala (já filtrado, mas mantido para compatibilidade)
            var coworkingProductsDict = new Dictionary<int, bool>();
            foreach (var item in items)
            {
                var address = addresses.First(a => a.Id == item.CoworkingAddressId);
                // Todos os endereços já oferecem salas (devido ao filtro), mas verificamos também o produto
                coworkingProductsDict[item.CoworkingAddressId] = (address.OffersSharedRoom || address.OffersPrivateRoom) &&
                                                                 productsByCoworking.Contains(address.CoworkingId);
            }
            ViewBag.CoworkingProducts = coworkingProductsDict;

            var vm = new MarketplaceListVm
            {
                Search = search,
                AvailableStates = allStates,
                AvailableCities = allCities,
                Items = items,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> MarketplaceDetails(int id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Photos)
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (address == null) return NotFound();
            if (address.Coworking.ApprovalStatus != KlyonHubPlatform.Domain.Core.Enums.CoworkingApprovalStatus.Approved) return NotFound();

            var photos = address.Coworking.Photos?.OrderByDescending(p => p.IsMain)
                .ThenBy(p => p.DisplayOrder)
                .Select(p => p.Id)
                .ToList() ?? new List<int>();

            var mainPhotoId = address.Coworking.Photos?.FirstOrDefault(p => p.IsMain)?.Id
                ?? address.Coworking.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.Id;

            // Buscar avaliações do coworking
            var ratings = await _context.CoworkingRatings
                .Where(r => r.CoworkingId == address.CoworkingId)
                .ToListAsync();

            var averageRating = ratings.Any() ? ratings.Average(r => r.Rating) : 0;
            var totalRatings = ratings.Count;

            ViewBag.AverageRating = averageRating;
            ViewBag.TotalRatings = totalRatings;
            ViewBag.Ratings = ratings;

            // Verificar se cliente está logado
            var isClientLogged = User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client);
            ViewBag.IsClientLogged = isClientLogged;

            // Obter créditos disponíveis do cliente se estiver logado
            int availableCredits = 0;
            if (isClientLogged)
            {
                try
                {
                    var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
                    var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                    if (client != null)
                    {
                        availableCredits = await _context.ClientCredits
                            .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTime.UtcNow)
                            .SumAsync(c => c.Credits);
                    }
                }
                catch
                {
                    // Tabela ClientCredits pode não existir ainda ou erro ao buscar
                    availableCredits = 0;
                }
            }
            ViewBag.AvailableCredits = availableCredits;

            // Passar informações de salas para a view
            ViewBag.OffersSharedRoom = address.OffersSharedRoom;
            ViewBag.OffersPrivateRoom = address.OffersPrivateRoom;
            ViewBag.SharedRoomPricePerHour = address.SharedRoomPricePerHour;
            ViewBag.SharedRoomMaxSeats = address.SharedRoomMaxSeats;
            ViewBag.PrivateRoomPricePerHour = address.PrivateRoomPricePerHour;
            ViewBag.PrivateRoomMaxSeats = address.PrivateRoomMaxSeats;

            var vm = new MarketplaceDetailsVm
            {
                Address = address,
                Coworking = address.Coworking,
                PhotoIds = photos,
                MainPhotoId = mainPhotoId,
                ReturnUrl = returnUrl
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitInterest([FromForm] InterestFormVm model, CancellationToken cancellationToken = default)
        {
            // Validação básica
            if (string.IsNullOrWhiteSpace(model.Name) || 
                string.IsNullOrWhiteSpace(model.LastName) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Phone))
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.FillAllRequiredFields;
                return RedirectToAction(ActionNames.MarketplaceDetails, new { id = model.CoworkingAddressId });
            }

            // Verificar se o endereço existe
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == model.CoworkingAddressId);

            if (address == null)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                return RedirectToAction(ActionNames.Marketplace);
            }

            // Converter horários de string para TimeSpan
            TimeSpan? startTime = null;
            TimeSpan? endTime = null;
            
            if (!string.IsNullOrWhiteSpace(model.StartTime) && TimeSpan.TryParse(model.StartTime, out var parsedStartTime))
            {
                startTime = parsedStartTime;
            }
            
            if (!string.IsNullOrWhiteSpace(model.EndTime) && TimeSpan.TryParse(model.EndTime, out var parsedEndTime))
            {
                endTime = parsedEndTime;
            }

            // Salvar o interesse no banco de dados
            var interest = new KlyonHubPlatform.Domain.Entities.Interest
            {
                CoworkingAddressId = model.CoworkingAddressId,
                AllocationDate = model.AllocationDate,
                StartTime = startTime,
                EndTime = endTime,
                Name = model.Name,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Status = KlyonHubPlatform.Domain.Core.Enums.InterestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Interests.Add(interest);
            await _context.SaveChangesAsync();

            // TODO: Enviar email de notificação para o coworking parceiro

            TempData[SuccessMessage.TempDataKey] = SuccessMessage.InterestRegistered;
            return RedirectToAction(ActionNames.MarketplaceDetails, new { id = model.CoworkingAddressId });
        }

        [HttpPost]
        [Authorize(Roles = KlyonHubPlatform.Domain.Core.Constants.RoleNames.Client)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(int CoworkingAddressId, DateTime ReservationDate, string? StartTime, string? EndTime, string RoomType = "Shared", CancellationToken cancellationToken = default)
        {
            try
            {
                // Validar cliente
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Validar endereço
                var address = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == CoworkingAddressId);

                if (address == null)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNotFound;
                    return RedirectToAction(ActionNames.Marketplace);
                }

                // Verificar se o coworking oferece aluguel de sala
                var offersRoomRental = await _context.CoworkingProducts
                    .AnyAsync(p => p.CoworkingId == address.CoworkingId && 
                                  p.ProductType == "RoomRental" && 
                                  p.IsActive);

                if (!offersRoomRental)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNoRoomRental;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Validar data
                if (ReservationDate < DateTime.Today)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationDateCannotBeInPast;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Converter horários
                TimeSpan? startTimeSpan = null;
                TimeSpan? endTimeSpan = null;

                if (!string.IsNullOrWhiteSpace(StartTime) && TimeSpan.TryParse(StartTime, out var parsedStart))
                {
                    startTimeSpan = parsedStart;
                }

                if (!string.IsNullOrWhiteSpace(EndTime) && TimeSpan.TryParse(EndTime, out var parsedEnd))
                {
                    endTimeSpan = parsedEnd;
                }

                // Validar horários - obrigatórios para calcular valor
                if (!startTimeSpan.HasValue || !endTimeSpan.HasValue)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InformStartAndEndTime;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                if (endTimeSpan <= startTimeSpan)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.EndTimeMustBeGreaterThanStart;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Validar tipo de sala
                if (RoomType != "Shared" && RoomType != "Private")
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.InvalidRoomType;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Verificar se o endereço oferece o tipo de sala selecionado
                if (RoomType == "Shared" && !address.OffersSharedRoom)
                {
                    TempData[ErrorMessages.TempDataKey] = "Este endereço não oferece sala compartilhada.";
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                if (RoomType == "Private" && !address.OffersPrivateRoom)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.AddressNoPrivateRoom;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Validar horários de funcionamento
                var dayOfWeek = ReservationDate.DayOfWeek.ToString().ToUpper();
                var operatingHour = await _context.CoworkingOperatingHours
                    .FirstOrDefaultAsync(h => h.CoworkingId == address.CoworkingId && h.DayOfWeek == dayOfWeek);

                if (operatingHour == null || !operatingHour.IsActive)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.CoworkingNotOpenOnThisDay;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                if (!operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.OperatingHoursNotConfiguredForDay;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Verificar se o horário da reserva está dentro do horário de funcionamento
                if (startTimeSpan < operatingHour.OpeningTime || endTimeSpan > operatingHour.ClosingTime)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationTimeMustBeBetween(operatingHour.OpeningTime.Value.ToString(@"hh\:mm"), operatingHour.ClosingTime.Value.ToString(@"hh\:mm"));
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Verificar se a data está bloqueada
                var blockedDate = await _context.BlockedDates
                    .FirstOrDefaultAsync(b => b.CoworkingId == address.CoworkingId && 
                                             b.BlockedDateValue.Date == ReservationDate.Date);

                if (blockedDate != null)
                {
                    if (blockedDate.BlocksFullDay)
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.DateBlocked(blockedDate.Reason);
                        return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                    }
                    else if (blockedDate.StartTime.HasValue && blockedDate.EndTime.HasValue)
                    {
                        // Verificar se há sobreposição com o horário bloqueado
                        if (!(endTimeSpan <= blockedDate.StartTime || startTimeSpan >= blockedDate.EndTime))
                        {
                            TempData[ErrorMessages.TempDataKey] = ErrorMessages.TimeBlocked(blockedDate.Reason);
                            return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                        }
                    }
                }

                // Calcular duração em horas
                var duration = endTimeSpan.Value - startTimeSpan.Value;
                var hours = (decimal)Math.Max(1, Math.Ceiling(duration.TotalHours)); // Mínimo 1 hora, arredondar para cima

                // Obter preço por hora baseado no tipo de sala
                decimal pricePerHour = 0;
                int maxSeats = 0;

                if (RoomType == "Shared")
                {
                    pricePerHour = address.SharedRoomPricePerHour ?? 0;
                    maxSeats = address.SharedRoomMaxSeats ?? 0;
                }
                else
                {
                    pricePerHour = address.PrivateRoomPricePerHour ?? 0;
                    maxSeats = address.PrivateRoomMaxSeats ?? 0;
                }

                if (pricePerHour <= 0)
                {
                    TempData[ErrorMessages.TempDataKey] = ErrorMessages.PricePerHourNotConfigured;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Calcular valor total
                var totalValue = pricePerHour * hours;

                // Verificar disponibilidade baseada no tipo de sala
                var hasConflict = false;
                
                if (RoomType == "Private")
                {
                    // Sala privativa: não pode ter outra reserva no mesmo horário
                    hasConflict = await _context.Reservations
                        .AnyAsync(r => r.CoworkingAddressId == CoworkingAddressId &&
                                      r.ReservationDate.Date == ReservationDate.Date &&
                                      r.RoomType == "Private" &&
                                      r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled &&
                                      r.StartTime.HasValue && r.EndTime.HasValue &&
                                      !(r.EndTime <= startTimeSpan || r.StartTime >= endTimeSpan));
                }
                else
                {
                    // Sala compartilhada: verificar se não excede a quantidade máxima de carteiras
                    var reservationsInTimeSlot = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == CoworkingAddressId &&
                                   r.ReservationDate.Date == ReservationDate.Date &&
                                   r.RoomType == "Shared" &&
                                   r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled &&
                                   r.StartTime.HasValue && r.EndTime.HasValue &&
                                   !(r.EndTime <= startTimeSpan || r.StartTime >= endTimeSpan))
                        .CountAsync();

                    // Verificar se há espaço disponível (considerando que cada reserva ocupa 1 carteira)
                    if (reservationsInTimeSlot >= maxSeats)
                    {
                        hasConflict = true;
                    }
                }

                if (hasConflict)
                {
                    if (RoomType == "Private")
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.ReservationAlreadyExistsForPrivateRoom;
                    }
                    else
                    {
                        TempData[ErrorMessages.TempDataKey] = ErrorMessages.NoSeatsAvailable(maxSeats);
                    }
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }

                // Criar reserva com status "Pending" até o pagamento ser confirmado
                var reservation = new Reservation
                {
                    ClientId = client.Id,
                    CoworkingAddressId = CoworkingAddressId,
                    ReservationDate = ReservationDate.Date,
                    StartTime = startTimeSpan,
                    EndTime = endTimeSpan,
                    RoomType = RoomType,
                    TotalValue = totalValue,
                    Status = KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Pending, // Pendente até pagamento ser confirmado
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                // Criar pagamento direto para a reserva
                var payment = new Payment
                {
                    ReservationId = reservation.Id,
                    Amount = totalValue,
                    Status = PaymentStatus.Pending,
                    Method = PaymentMethod.PIX, // Default, pode ser alterado na tela de pagamento
                    DueDate = ReservationDate.Date,
                    ExternalPaymentId = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Redirecionar para página de pagamento
                return RedirectToAction(ActionNames.PaymentDetails, ControllerNames.Client, new { paymentId = payment.Id });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("criar reserva", ex.Message);
                return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
            }
        }

        /// <summary>
        /// Retorna, para cada dia do mês, fullyBooked (dia todo ocupado) e occupied (intervalos já reservados).
        /// Usado pela tela BookRoom para desabilitar dias e esconder horários indisponíveis.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailabilityForMonth(int addressId, string roomType, int year, int month, CancellationToken cancellationToken = default)
        {
            try
            {
                var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
                if (address == null)
                    return Json(new { error = ErrorJson.AddressNotFound });

                if (roomType != "Shared" && roomType != "Private")
                    return Json(new { error = ErrorJson.InvalidRoomType });

                if (roomType == "Shared" && !address.OffersSharedRoom)
                    return Json(new { error = ErrorJson.AddressNoSharedRoom });
                if (roomType == "Private" && !address.OffersPrivateRoom)
                    return Json(new { error = ErrorJson.AddressNoPrivateRoom });

                var daysInMonth = DateTime.DaysInMonth(year, month);
                var dates = new Dictionary<string, object>();

                // Carregar horários de funcionamento e datas bloqueadas
                var operatingHours = await _context.CoworkingOperatingHours
                    .Where(h => h.CoworkingId == address.CoworkingId)
                    .ToListAsync();
                var blockedDates = await _context.BlockedDates
                    .Where(b => b.CoworkingId == address.CoworkingId &&
                               b.BlockedDateValue.Year == year &&
                               b.BlockedDateValue.Month == month)
                    .ToListAsync();

                // Reservas do mês para este endereço e tipo
                var first = new DateTime(year, month, 1);
                var last = new DateTime(year, month, daysInMonth);
                var reservations = await _context.Reservations
                    .Where(r => r.CoworkingAddressId == addressId &&
                               r.ReservationDate >= first && r.ReservationDate <= last &&
                               r.RoomType == roomType &&
                               r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled &&
                               r.StartTime.HasValue && r.EndTime.HasValue)
                    .Select(r => new { r.ReservationDate, r.StartTime, r.EndTime })
                    .ToListAsync();

                var today = DateTime.Today;

                for (var d = 1; d <= daysInMonth; d++)
                {
                    var dt = new DateTime(year, month, d);
                    var key = dt.ToString("yyyy-MM-dd");
                    if (dt < today)
                    {
                        dates[key] = new { fullyBooked = true, occupied = Array.Empty<object>() };
                        continue;
                    }

                    // Verificar se a data está bloqueada
                    var blockedDate = blockedDates.FirstOrDefault(b => b.BlockedDateValue.Date == dt.Date);
                    if (blockedDate != null && blockedDate.BlocksFullDay)
                    {
                        dates[key] = new { fullyBooked = true, occupied = Array.Empty<object>() };
                        continue;
                    }

                    // Obter horário de funcionamento para este dia
                    var dayOfWeek = dt.DayOfWeek.ToString().ToUpper();
                    var operatingHour = operatingHours.FirstOrDefault(h => h.DayOfWeek == dayOfWeek);
                    
                    if (operatingHour == null || !operatingHour.IsActive || !operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                    {
                        dates[key] = new { fullyBooked = true, occupied = Array.Empty<object>() };
                        continue;
                    }

                    var opStart = operatingHour.OpeningTime.Value;
                    var opEnd = operatingHour.ClosingTime.Value;

                    var dayRes = reservations.Where(r => r.ReservationDate.Date == dt).ToList();

                    // Adicionar horários bloqueados às reservas se houver bloqueio parcial
                    if (blockedDate != null && !blockedDate.BlocksFullDay && blockedDate.StartTime.HasValue && blockedDate.EndTime.HasValue)
                    {
                        dayRes.Add(new { ReservationDate = dt, StartTime = (TimeSpan?)blockedDate.StartTime, EndTime = (TimeSpan?)blockedDate.EndTime });
                    }

                    if (roomType == "Private")
                    {
                        var intervals = dayRes
                            .Select(r => (s: r.StartTime!.Value, e: r.EndTime!.Value))
                            .OrderBy(x => x.s)
                            .ToList();
                        var merged = new List<(TimeSpan s, TimeSpan e)>();
                        foreach ((TimeSpan s, TimeSpan e) in intervals)
                        {
                            if (merged.Count > 0 && s <= merged[merged.Count - 1].e)
                                merged[merged.Count - 1] = (merged[merged.Count - 1].s, merged[merged.Count - 1].e > e ? merged[merged.Count - 1].e : e);
                            else
                                merged.Add((s, e));
                        }
                        var occupied = merged.Select(m => new { start = m.s.ToString(@"hh\:mm"), end = m.e.ToString(@"hh\:mm") }).ToList<object>();

                        var fullyBooked = merged.Count > 0 &&
                                          merged[0].s <= opStart &&
                                          merged[merged.Count - 1].e >= opEnd;
                        if (fullyBooked && merged.Count > 1)
                        {
                            for (var i = 0; i < merged.Count - 1; i++)
                            {
                                if (merged[i].e < merged[i + 1].s) { fullyBooked = false; break; }
                            }
                        }

                        dates[key] = new { fullyBooked, occupied };
                    }
                    else
                    {
                        var maxSeats = address.SharedRoomMaxSeats ?? 0;
                        if (maxSeats <= 0)
                        {
                            dates[key] = new { fullyBooked = false, occupied = Array.Empty<object>() };
                            continue;
                        }

                        var slotStarts = new List<TimeSpan>();
                        var t = opStart;
                        while (t < opEnd)
                        {
                            slotStarts.Add(t);
                            t = t.Add(TimeSpan.FromMinutes(30));
                        }

                        var fullSlots = new List<TimeSpan>();
                        foreach (var slotStart in slotStarts)
                        {
                            var slotEnd = slotStart.Add(TimeSpan.FromMinutes(30));
                            var count = dayRes.Count(r => !(r.EndTime <= slotStart || r.StartTime >= slotEnd));
                            if (count >= maxSeats)
                                fullSlots.Add(slotStart);
                        }

                        var occupied = new List<object>();
                        if (fullSlots.Count > 0)
                        {
                            fullSlots.Sort((a, b) => a.CompareTo(b));
                            var segStart = fullSlots[0];
                            var segEnd = segStart.Add(TimeSpan.FromMinutes(30));
                            for (var i = 1; i < fullSlots.Count; i++)
                            {
                                if (fullSlots[i] == segEnd)
                                {
                                    segEnd = fullSlots[i].Add(TimeSpan.FromMinutes(30));
                                }
                                else
                                {
                                    occupied.Add(new { start = segStart.ToString(@"hh\:mm"), end = segEnd.ToString(@"hh\:mm") });
                                    segStart = fullSlots[i];
                                    segEnd = segStart.Add(TimeSpan.FromMinutes(30));
                                }
                            }
                            occupied.Add(new { start = segStart.ToString(@"hh\:mm"), end = segEnd.ToString(@"hh\:mm") });
                        }

                        var fullyBooked = fullSlots.Count == slotStarts.Count;
                        dates[key] = new { fullyBooked, occupied };
                    }
                }

                return Json(new { dates });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailability(int addressId, string roomType, DateTime? date = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var address = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == addressId);

                if (address == null)
                {
                    return Json(new { error = ErrorJson.AddressNotFound });
                }

                // Se uma data específica foi fornecida, retornar horários disponíveis para aquela data
                if (date.HasValue)
                {
                    // Verificar se a data está bloqueada
                    var blockedDate = await _context.BlockedDates
                        .FirstOrDefaultAsync(b => b.CoworkingId == address.CoworkingId && 
                                                 b.BlockedDateValue.Date == date.Value.Date);

                    if (blockedDate != null && blockedDate.BlocksFullDay)
                    {
                        return Json(new
                        {
                            available = false,
                            occupiedSlots = Array.Empty<object>(),
                            error = $"Data bloqueada: {blockedDate.Reason}"
                        });
                    }

                    // Obter horário de funcionamento para este dia
                    var dayOfWeek = date.Value.DayOfWeek.ToString().ToUpper();
                    var operatingHour = await _context.CoworkingOperatingHours
                        .FirstOrDefaultAsync(h => h.CoworkingId == address.CoworkingId && h.DayOfWeek == dayOfWeek);

                    if (operatingHour == null || !operatingHour.IsActive || !operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                    {
                        return Json(new
                        {
                            available = false,
                            occupiedSlots = Array.Empty<object>(),
                            error = ErrorJson.CoworkingNotOpenOnDay
                        });
                    }

                    var reservations = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate.Date == date.Value.Date &&
                                   r.RoomType == roomType &&
                                   r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled &&
                                   r.StartTime.HasValue && r.EndTime.HasValue)
                        .Select(r => new { r.StartTime, r.EndTime })
                        .ToListAsync();

                    // Adicionar horários bloqueados se houver bloqueio parcial
                    if (blockedDate != null && !blockedDate.BlocksFullDay && blockedDate.StartTime.HasValue && blockedDate.EndTime.HasValue)
                    {
                        reservations.Add(new { StartTime = (TimeSpan?)blockedDate.StartTime, EndTime = (TimeSpan?)blockedDate.EndTime });
                    }

                    // Se for sala privativa, retornar horários ocupados
                    if (roomType == "Private")
                    {
                        var occupiedSlots = reservations.Select(r => new
                        {
                            start = r.StartTime.Value.ToString(@"hh\:mm"),
                            end = r.EndTime.Value.ToString(@"hh\:mm")
                        }).ToList();

                        return Json(new
                        {
                            available = occupiedSlots.Count == 0,
                            occupiedSlots = occupiedSlots,
                            operatingHours = new
                            {
                                opening = operatingHour.OpeningTime.Value.ToString(@"hh\:mm"),
                                closing = operatingHour.ClosingTime.Value.ToString(@"hh\:mm")
                            }
                        });
                    }
                    else // Sala compartilhada
                    {
                        var maxSeats = address.SharedRoomMaxSeats ?? 0;
                        var occupiedSlots = new List<object>();

                        // Agrupar reservas por horário para contar quantas carteiras estão ocupadas
                        var timeSlots = new Dictionary<string, int>();
                        
                        foreach (var res in reservations)
                        {
                            var start = res.StartTime.Value;
                            var end = res.EndTime.Value;
                            
                            // Criar slots de 1 hora
                            var current = start;
                            while (current < end)
                            {
                                var slotKey = current.ToString(@"hh\:mm");
                                if (!timeSlots.ContainsKey(slotKey))
                                {
                                    timeSlots[slotKey] = 0;
                                }
                                timeSlots[slotKey]++;
                                current = current.Add(TimeSpan.FromHours(1));
                            }
                        }

                        // Identificar slots completamente ocupados
                        foreach (var slot in timeSlots)
                        {
                            if (slot.Value >= maxSeats)
                            {
                                occupiedSlots.Add(new { start = slot.Key, end = slot.Key });
                            }
                        }

                        return Json(new
                        {
                            available = true,
                            occupiedSlots = occupiedSlots,
                            maxSeats = maxSeats,
                            currentOccupancy = timeSlots,
                            operatingHours = new
                            {
                                opening = operatingHour.OpeningTime.Value.ToString(@"hh\:mm"),
                                closing = operatingHour.ClosingTime.Value.ToString(@"hh\:mm")
                            }
                        });
                    }
                }

                // Se não foi fornecida data, retornar dias disponíveis nos próximos 30 dias
                var unavailableDates = new List<string>();
                var startDate = DateTime.Today;
                var endDate = DateTime.Today.AddDays(30);

                if (roomType == "Private")
                {
                    // Para sala privativa, um dia está indisponível se tiver qualquer reserva
                    var reservedDates = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate >= startDate &&
                                   r.ReservationDate <= endDate &&
                                   r.RoomType == "Private" &&
                                   r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled)
                        .Select(r => r.ReservationDate.Date)
                        .Distinct()
                        .ToListAsync();

                    unavailableDates = reservedDates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
                }
                else // Sala compartilhada
                {
                    // Para sala compartilhada, verificar se há dias completamente lotados
                    var maxSeats = address.SharedRoomMaxSeats ?? 0;
                    
                    var reservationsByDate = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate >= startDate &&
                                   r.ReservationDate <= endDate &&
                                   r.RoomType == "Shared" &&
                                   r.Status != KlyonHubPlatform.Domain.Core.Enums.ReservationStatus.Cancelled)
                        .GroupBy(r => r.ReservationDate.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .ToListAsync();

                    // Considerar um dia indisponível se tiver muitas reservas (pode ser ajustado)
                    // Por enquanto, vamos considerar disponível se não exceder o limite
                    unavailableDates = reservationsByDate
                        .Where(r => r.Count >= maxSeats * 8) // Assumindo 8 horas de operação
                        .Select(r => r.Date.ToString("yyyy-MM-dd"))
                        .ToList();
                }

                return Json(new
                {
                    unavailableDates = unavailableDates,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Página pública com os Termos de Uso e Política de Privacidade (LGPD).
        /// Pode ser visualizada diretamente ou dentro de uma modal no cadastro.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult TermosUsoLgpd()
        {
            return View();
        }
    }
}
