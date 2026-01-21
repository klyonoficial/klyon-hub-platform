using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KlyonHubPlatform.Api.ViewModels;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Api.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToAction("Dashboard", role switch
                {
                    "Administrator" => "Admin",
                    "CoworkingPartner" => "Coworking",
                    "Client" => "Client",
                    _ => "Home"
                });
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Marketplace([FromQuery] MarketplaceSearchVm search)
        {
            // Normalização
            search.Page = search.Page <= 0 ? 1 : search.Page;
            search.PageSize = search.PageSize is < 6 or > 48 ? 12 : search.PageSize;

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
                    SharedRoomCreditsPerHour = a.SharedRoomPricePerHour.HasValue ? (int)Math.Ceiling(a.SharedRoomPricePerHour.Value) : null,
                    OffersPrivateRoom = a.OffersPrivateRoom,
                    PrivateRoomCreditsPerHour = a.PrivateRoomPricePerHour.HasValue ? (int)Math.Ceiling(a.PrivateRoomPricePerHour.Value) : null
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
        public async Task<IActionResult> MarketplaceDetails(int id, string? returnUrl = null)
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

            // Verificar se cliente está logado e buscar créditos disponíveis
            var isClientLogged = User.Identity?.IsAuthenticated == true && User.IsInRole("Client");
            int availableCredits = 0;
            if (isClientLogged)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client != null)
                {
                    availableCredits = await _context.ClientCredits
                        .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTime.UtcNow)
                        .SumAsync(c => c.Credits);
                }
            }
            ViewBag.AvailableCredits = availableCredits;
            ViewBag.IsClientLogged = isClientLogged;

            // Buscar planos de crédito para o modal
            if (isClientLogged)
            {
                var creditPlans = await _context.CreditPlans
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Price)
                    .ToListAsync();
                ViewBag.CreditPlans = creditPlans;
            }

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
        public async Task<IActionResult> SubmitInterest([FromForm] InterestFormVm model)
        {
            // Validação básica
            if (string.IsNullOrWhiteSpace(model.Name) || 
                string.IsNullOrWhiteSpace(model.LastName) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Phone))
            {
                TempData["Error"] = "Por favor, preencha todos os campos obrigatórios.";
                return RedirectToAction("MarketplaceDetails", new { id = model.CoworkingAddressId });
            }

            // Verificar se o endereço existe
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == model.CoworkingAddressId);

            if (address == null)
            {
                TempData["Error"] = "Endereço não encontrado.";
                return RedirectToAction("Marketplace");
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
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Interests.Add(interest);
            await _context.SaveChangesAsync();

            // TODO: Enviar email de notificação para o coworking parceiro

            TempData["Success"] = "Seu interesse foi registrado com sucesso! Entraremos em contato em breve.";
            return RedirectToAction("MarketplaceDetails", new { id = model.CoworkingAddressId });
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(int CoworkingAddressId, DateTime ReservationDate, string? StartTime, string? EndTime, string RoomType = "Shared")
        {
            try
            {
                // Validar cliente
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                
                if (client == null)
                {
                    TempData["Error"] = "Cliente não encontrado.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Validar endereço
                var address = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == CoworkingAddressId);

                if (address == null)
                {
                    TempData["Error"] = "Endereço não encontrado.";
                    return RedirectToAction("Marketplace");
                }

                // Verificar se o coworking oferece aluguel de sala
                var offersRoomRental = await _context.CoworkingProducts
                    .AnyAsync(p => p.CoworkingId == address.CoworkingId && 
                                  p.ProductType == "RoomRental" && 
                                  p.IsActive);

                if (!offersRoomRental)
                {
                    TempData["Error"] = "Este coworking não oferece aluguel de salas.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Validar data
                if (ReservationDate < DateTime.Today)
                {
                    TempData["Error"] = "A data da reserva não pode ser no passado.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
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
                    TempData["Error"] = "Por favor, informe o horário inicial e final para calcular o valor da reserva.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                if (endTimeSpan <= startTimeSpan)
                {
                    TempData["Error"] = "O horário final deve ser maior que o inicial.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Validar tipo de sala
                if (RoomType != "Shared" && RoomType != "Private")
                {
                    TempData["Error"] = "Tipo de sala inválido.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Verificar se o endereço oferece o tipo de sala selecionado
                if (RoomType == "Shared" && !address.OffersSharedRoom)
                {
                    TempData["Error"] = "Este endereço não oferece sala compartilhada.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                if (RoomType == "Private" && !address.OffersPrivateRoom)
                {
                    TempData["Error"] = "Este endereço não oferece sala privativa.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
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
                    TempData["Error"] = "Preço por hora não configurado para este tipo de sala.";
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Calcular valor total (1 crédito = R$ 1,00)
                var totalValue = pricePerHour * hours;
                var creditsNeeded = (int)Math.Ceiling(totalValue); // Arredondar para cima

                // Verificar disponibilidade baseada no tipo de sala
                var hasConflict = false;
                
                if (RoomType == "Private")
                {
                    // Sala privativa: não pode ter outra reserva no mesmo horário
                    hasConflict = await _context.Reservations
                        .AnyAsync(r => r.CoworkingAddressId == CoworkingAddressId &&
                                      r.ReservationDate.Date == ReservationDate.Date &&
                                      r.RoomType == "Private" &&
                                      r.Status != "Cancelled" &&
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
                                   r.Status != "Cancelled" &&
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
                        TempData["Error"] = "Já existe uma reserva para este horário na sala privativa. Por favor, escolha outro horário.";
                    }
                    else
                    {
                        TempData["Error"] = $"Não há carteiras disponíveis neste horário. Máximo: {maxSeats} carteiras.";
                    }
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Verificar créditos disponíveis
                var availableCredits = await _context.ClientCredits
                    .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTime.UtcNow)
                    .SumAsync(c => c.Credits);

                if (availableCredits < creditsNeeded)
                {
                    TempData["Error"] = $"Você não possui créditos suficientes. Necessário: {creditsNeeded} créditos (R$ {totalValue:F2}), Disponível: {availableCredits} créditos";
                    TempData["ShowBuyCredits"] = true;
                    return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
                }

                // Criar reserva
                var reservation = new Reservation
                {
                    ClientId = client.Id,
                    CoworkingAddressId = CoworkingAddressId,
                    ReservationDate = ReservationDate.Date,
                    StartTime = startTimeSpan,
                    EndTime = endTimeSpan,
                    RoomType = RoomType,
                    TotalValue = totalValue,
                    CreditsUsed = creditsNeeded,
                    Status = "Confirmed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);

                // Deduzir créditos (FIFO - primeiro a vencer)
                var creditsToDeduct = creditsNeeded;
                var credits = await _context.ClientCredits
                    .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTime.UtcNow && c.Credits > 0)
                    .OrderBy(c => c.ExpiresAt)
                    .ToListAsync();

                foreach (var credit in credits)
                {
                    if (creditsToDeduct <= 0) break;

                    if (credit.Credits >= creditsToDeduct)
                    {
                        credit.Credits -= creditsToDeduct;
                        creditsToDeduct = 0;
                    }
                    else
                    {
                        creditsToDeduct -= credit.Credits;
                        credit.Credits = 0;
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Reserva confirmada com sucesso para {ReservationDate:dd/MM/yyyy}! Você receberá um email de confirmação em breve.";
                return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao criar reserva: {ex.Message}";
                return RedirectToAction("MarketplaceDetails", new { id = CoworkingAddressId });
            }
        }

        /// <summary>
        /// Retorna, para cada dia do mês, fullyBooked (dia todo ocupado) e occupied (intervalos já reservados).
        /// Usado pela tela BookRoom para desabilitar dias e esconder horários indisponíveis.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailabilityForMonth(int addressId, string roomType, int year, int month)
        {
            try
            {
                var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
                if (address == null)
                    return Json(new { error = "Endereço não encontrado" });

                if (roomType != "Shared" && roomType != "Private")
                    return Json(new { error = "Tipo de sala inválido" });

                if (roomType == "Shared" && !address.OffersSharedRoom)
                    return Json(new { error = "Endereço não oferece sala compartilhada" });
                if (roomType == "Private" && !address.OffersPrivateRoom)
                    return Json(new { error = "Endereço não oferece sala privativa" });

                var opStart = new TimeSpan(8, 0, 0);
                var opEnd = new TimeSpan(23, 0, 0);
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var dates = new Dictionary<string, object>();

                // Reservas do mês para este endereço e tipo
                var first = new DateTime(year, month, 1);
                var last = new DateTime(year, month, daysInMonth);
                var reservations = await _context.Reservations
                    .Where(r => r.CoworkingAddressId == addressId &&
                               r.ReservationDate >= first && r.ReservationDate <= last &&
                               r.RoomType == roomType &&
                               r.Status != "Cancelled" &&
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

                    var dayRes = reservations.Where(r => r.ReservationDate.Date == dt).ToList();

                    if (roomType == "Private")
                    {
                        var intervals = dayRes
                            .Select(r => (s: r.StartTime!.Value, e: r.EndTime!.Value))
                            .OrderBy(x => x.s)
                            .ToList();
                        var merged = new List<(TimeSpan s, TimeSpan e)>();
                        foreach (var (s, e) in intervals)
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
        public async Task<IActionResult> GetAvailability(int addressId, string roomType, DateTime? date = null)
        {
            try
            {
                var address = await _context.CoworkingAddresses
                    .FirstOrDefaultAsync(a => a.Id == addressId);

                if (address == null)
                {
                    return Json(new { error = "Endereço não encontrado" });
                }

                // Se uma data específica foi fornecida, retornar horários disponíveis para aquela data
                if (date.HasValue)
                {
                    var reservations = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate.Date == date.Value.Date &&
                                   r.RoomType == roomType &&
                                   r.Status != "Cancelled" &&
                                   r.StartTime.HasValue && r.EndTime.HasValue)
                        .Select(r => new { r.StartTime, r.EndTime })
                        .ToListAsync();

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
                            occupiedSlots = occupiedSlots
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
                            currentOccupancy = timeSlots
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
                                   r.Status != "Cancelled")
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
                                   r.Status != "Cancelled")
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
    }
}
