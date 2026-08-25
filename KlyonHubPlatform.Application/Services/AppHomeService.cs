using KlyonHubPlatform.Application.DTOs.AppHome;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class AppHomeService : IAppHomeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICoworkingService _coworkingService;

        public AppHomeService(ApplicationDbContext context, ICoworkingService coworkingService)
        {
            _context = context;
            _coworkingService = coworkingService;
        }

        public async Task<int?> GetSolePublishedAddressIdAsync(CancellationToken cancellationToken = default)
        {
            var addresses = await _coworkingService.GetAvailableAddressesAsync(null, cancellationToken);
            if (addresses.Count != 1)
                return null;
            return addresses[0].Id;
        }

        public async Task<MarketplaceListResult?> GetMarketplaceListAsync(MarketplaceSearchDto search, CancellationToken cancellationToken = default)
        {
            search.Page = search.Page <= 0 ? 1 : search.Page;
            search.PageSize = Math.Max(1, Math.Min(search.PageSize, 100));
            var baseQuery = _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Photos)
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .Where(a => a.Coworking.IsActive &&
                           a.Coworking.IsPartner &&
                           a.Coworking.ApprovalStatus == CoworkingApprovalStatus.Approved &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom))
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
                baseQuery = baseQuery.Where(a => a.Neighborhood != null && a.Neighborhood.Contains(neighborhood));
            }
            if (search.MinPeople.HasValue && search.MinPeople.Value > 0)
            {
                baseQuery = baseQuery.Where(a => a.MaxContracts >= search.MinPeople.Value);
            }
            if (search.MaxPeople.HasValue && search.MaxPeople.Value > 0)
            {
                baseQuery = baseQuery.Where(a => a.MaxContracts <= search.MaxPeople.Value);
            }
            var allStates = await _context.CoworkingAddresses
                .Where(a => a.Coworking.IsActive &&
                           a.Coworking.IsPartner &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom))
                .Select(a => a.State)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(cancellationToken);
            var citiesQuery = _context.CoworkingAddresses
                .Where(a => a.Coworking.IsActive &&
                           a.Coworking.IsPartner &&
                           a.Coworking.ApprovalStatus == CoworkingApprovalStatus.Approved &&
                           (a.OffersSharedRoom || a.OffersPrivateRoom));
            if (!string.IsNullOrWhiteSpace(search.State))
            {
                citiesQuery = citiesQuery.Where(a => a.State.ToUpper() == search.State!.ToUpper());
            }
            var allCities = await citiesQuery
                .Select(a => a.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken);
            var totalItems = await baseQuery.CountAsync(cancellationToken);
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
                .ToListAsync(cancellationToken);
            var coworkingIds = addresses.Select(a => a.CoworkingId).Distinct().ToList();
            var productsByCoworking = await _context.CoworkingProducts
                .Where(p => coworkingIds.Contains(p.CoworkingId) &&
                           p.ProductType == "RoomRental" &&
                           p.IsActive)
                .Select(p => p.CoworkingId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var items = addresses.Select(a =>
            {
                var mainPhoto = a.Coworking.Photos?.FirstOrDefault(p => p.IsMain)
                    ?? a.Coworking.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault();
                var maxCapacity = 0;
                if (a.OffersSharedRoom && a.SharedRoomMaxSeats.HasValue)
                    maxCapacity = Math.Max(maxCapacity, a.SharedRoomMaxSeats.Value);
                if (a.OffersPrivateRoom && a.PrivateRoomMaxSeats.HasValue)
                    maxCapacity = Math.Max(maxCapacity, a.PrivateRoomMaxSeats.Value);
                if (maxCapacity == 0)
                    maxCapacity = a.MaxContracts;
                return new MarketplaceCardDto
                {
                    CoworkingAddressId = a.Id,
                    Title = $"Endereço em {a.City}",
                    Subtitle = a.Neighborhood ?? "",
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
            var coworkingProductsDict = new Dictionary<int, bool>();
            foreach (var item in items)
            {
                var address = addresses.First(a => a.Id == item.CoworkingAddressId);
                coworkingProductsDict[item.CoworkingAddressId] = (address.OffersSharedRoom || address.OffersPrivateRoom) &&
                                                                 productsByCoworking.Contains(address.CoworkingId);
            }
            return new MarketplaceListResult
            {
                Search = search,
                AvailableStates = allStates,
                AvailableCities = allCities,
                Items = items,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CoworkingProducts = coworkingProductsDict
            };
        }

        public async Task<MarketplaceDetailsResult?> GetMarketplaceDetailsAsync(int addressId, int? userId, CancellationToken cancellationToken = default)
        {
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Photos)
                .Include(a => a.Coworking)
                    .ThenInclude(c => c.Products)
                .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
            if (address == null) return null;
            if (address.Coworking.ApprovalStatus != CoworkingApprovalStatus.Approved) return null;
            var photos = address.Coworking.Photos?.OrderByDescending(p => p.IsMain)
                .ThenBy(p => p.DisplayOrder)
                .Select(p => p.Id)
                .ToList() ?? new List<int>();
            var mainPhotoId = address.Coworking.Photos?.FirstOrDefault(p => p.IsMain)?.Id
                ?? address.Coworking.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.Id;
            var ratings = await _context.CoworkingRatings
                .Where(r => r.CoworkingId == address.CoworkingId)
                .ToListAsync(cancellationToken);
            var averageRating = ratings.Count > 0 ? ratings.Average(r => r.Rating) : 0;
            int availableCredits = 0;
            if (userId.HasValue && userId.Value > 0)
            {
                try
                {
                    var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);
                    if (client != null)
                    {
                        availableCredits = await _context.ClientCredits
                            .Where(c => c.ClientId == client.Id && c.ExpiresAt > DateTime.UtcNow)
                            .SumAsync(c => c.Credits, cancellationToken);
                    }
                }
                catch
                {
                    availableCredits = 0;
                }
            }
            return new MarketplaceDetailsResult
            {
                Address = address,
                Coworking = address.Coworking,
                PhotoIds = photos,
                MainPhotoId = mainPhotoId,
                AverageRating = averageRating,
                TotalRatings = ratings.Count,
                Ratings = ratings,
                IsClientLogged = userId.HasValue && userId.Value > 0,
                AvailableCredits = availableCredits,
                OffersSharedRoom = address.OffersSharedRoom,
                OffersPrivateRoom = address.OffersPrivateRoom,
                SharedRoomPricePerHour = address.SharedRoomPricePerHour,
                SharedRoomMaxSeats = address.SharedRoomMaxSeats,
                PrivateRoomPricePerHour = address.PrivateRoomPricePerHour,
                PrivateRoomMaxSeats = address.PrivateRoomMaxSeats
            };
        }

        public async Task<SubmitInterestResult> SubmitInterestAsync(SubmitInterestInput input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input.Name) ||
                string.IsNullOrWhiteSpace(input.LastName) ||
                string.IsNullOrWhiteSpace(input.Email) ||
                string.IsNullOrWhiteSpace(input.Phone))
            {
                return new SubmitInterestResult { Success = false, ErrorMessage = "Por favor, preencha todos os campos obrigatórios." };
            }
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == input.CoworkingAddressId, cancellationToken);
            if (address == null)
            {
                return new SubmitInterestResult { Success = false, ErrorMessage = "Endereço não encontrado." };
            }
            TimeSpan? startTime = null;
            TimeSpan? endTime = null;
            if (!string.IsNullOrWhiteSpace(input.StartTime) && TimeSpan.TryParse(input.StartTime, out var parsedStartTime))
                startTime = parsedStartTime;
            if (!string.IsNullOrWhiteSpace(input.EndTime) && TimeSpan.TryParse(input.EndTime, out var parsedEndTime))
                endTime = parsedEndTime;
            var interest = new Interest
            {
                CoworkingAddressId = input.CoworkingAddressId,
                AllocationDate = input.AllocationDate,
                StartTime = startTime,
                EndTime = endTime,
                Name = input.Name,
                LastName = input.LastName,
                Email = input.Email,
                Phone = input.Phone,
                Status = InterestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.Interests.Add(interest);
            await _context.SaveChangesAsync(cancellationToken);
            return new SubmitInterestResult { Success = true };
        }

        public async Task<CreateReservationResult> CreateReservationAsync(CreateReservationInput input, CancellationToken cancellationToken = default)
        {
            const string msgClientNotFound = "Cliente não encontrado.";
            const string msgAddressNotFound = "Endereço não encontrado.";
            const string msgCoworkingNoRoomRental = "Este coworking não oferece aluguel de salas.";
            const string msgReservationDatePast = "A data da reserva não pode ser no passado.";
            const string msgInformStartEndTime = "Por favor, informe o horário inicial e final para calcular o valor da reserva.";
            const string msgEndTimeGreater = "O horário final deve ser maior que o inicial.";
            const string msgInvalidRoomType = "Tipo de sala inválido.";
            const string msgNoSharedRoom = "Este endereço não oferece sala compartilhada.";
            const string msgNoPrivateRoom = "Este endereço não oferece sala privativa.";
            const string msgNotOpen = "O coworking não está funcionando neste dia da semana.";
            const string msgOperatingHoursNotConfigured = "Horários de funcionamento não configurados para este dia.";
            const string msgPriceNotConfigured = "Preço por hora não configurado para este tipo de sala.";
            const string msgPrivateRoomConflict = "Já existe uma reserva para este horário na sala privativa. Por favor, escolha outro horário.";

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == input.UserId, cancellationToken);
            if (client == null)
                return new CreateReservationResult { Success = false, ErrorMessage = msgClientNotFound };
            var address = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == input.CoworkingAddressId, cancellationToken);
            if (address == null)
                return new CreateReservationResult { Success = false, ErrorMessage = msgAddressNotFound };
            var offersRoomRental = await _context.CoworkingProducts
                .AnyAsync(p => p.CoworkingId == address.CoworkingId && p.ProductType == "RoomRental" && p.IsActive, cancellationToken);
            if (!offersRoomRental)
                return new CreateReservationResult { Success = false, ErrorMessage = msgCoworkingNoRoomRental };
            if (input.ReservationDate < DateTime.Today)
                return new CreateReservationResult { Success = false, ErrorMessage = msgReservationDatePast };
            TimeSpan? startTimeSpan = null;
            TimeSpan? endTimeSpan = null;
            if (!string.IsNullOrWhiteSpace(input.StartTime) && TimeSpan.TryParse(input.StartTime, out var parsedStart))
                startTimeSpan = parsedStart;
            if (!string.IsNullOrWhiteSpace(input.EndTime) && TimeSpan.TryParse(input.EndTime, out var parsedEnd))
                endTimeSpan = parsedEnd;
            if (!startTimeSpan.HasValue || !endTimeSpan.HasValue)
                return new CreateReservationResult { Success = false, ErrorMessage = msgInformStartEndTime };
            if (endTimeSpan <= startTimeSpan)
                return new CreateReservationResult { Success = false, ErrorMessage = msgEndTimeGreater };
            if (input.RoomType != "Shared" && input.RoomType != "Private")
                return new CreateReservationResult { Success = false, ErrorMessage = msgInvalidRoomType };
            if (input.RoomType == "Shared" && !address.OffersSharedRoom)
                return new CreateReservationResult { Success = false, ErrorMessage = msgNoSharedRoom };
            if (input.RoomType == "Private" && !address.OffersPrivateRoom)
                return new CreateReservationResult { Success = false, ErrorMessage = msgNoPrivateRoom };
            var dayOfWeek = input.ReservationDate.DayOfWeek.ToString().ToUpper();
            var operatingHour = await _context.CoworkingOperatingHours
                .FirstOrDefaultAsync(h => h.CoworkingId == address.CoworkingId && h.DayOfWeek == dayOfWeek, cancellationToken);
            if (operatingHour == null || !operatingHour.IsActive)
                return new CreateReservationResult { Success = false, ErrorMessage = msgNotOpen };
            if (!operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                return new CreateReservationResult { Success = false, ErrorMessage = msgOperatingHoursNotConfigured };
            if (startTimeSpan < operatingHour.OpeningTime || endTimeSpan > operatingHour.ClosingTime)
            {
                var openStr = operatingHour.OpeningTime!.Value.ToString(@"hh\:mm");
                var closeStr = operatingHour.ClosingTime!.Value.ToString(@"hh\:mm");
                return new CreateReservationResult { Success = false, ErrorMessage = $"O horário da reserva deve estar entre {openStr} e {closeStr}." };
            }
            var blockedDate = await _context.BlockedDates
                .FirstOrDefaultAsync(b => b.CoworkingId == address.CoworkingId && b.BlockedDateValue.Date == input.ReservationDate.Date, cancellationToken);
            if (blockedDate != null)
            {
                if (blockedDate.BlocksFullDay)
                    return new CreateReservationResult { Success = false, ErrorMessage = $"Esta data está bloqueada: {blockedDate.Reason}" };
                if (blockedDate.StartTime.HasValue && blockedDate.EndTime.HasValue &&
                    !(endTimeSpan <= blockedDate.StartTime || startTimeSpan >= blockedDate.EndTime))
                    return new CreateReservationResult { Success = false, ErrorMessage = $"Este horário está bloqueado: {blockedDate.Reason}" };
            }
            var duration = endTimeSpan.Value - startTimeSpan.Value;
            var hours = (decimal)Math.Max(1, Math.Ceiling(duration.TotalHours));
            decimal pricePerHour = 0;
            int maxSeats = 0;
            if (input.RoomType == "Shared")
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
                return new CreateReservationResult { Success = false, ErrorMessage = msgPriceNotConfigured };
            var totalValue = pricePerHour * hours;
            var hasConflict = false;
            if (input.RoomType == "Private")
            {
                hasConflict = await _context.Reservations
                    .AnyAsync(r => r.CoworkingAddressId == input.CoworkingAddressId &&
                                  r.ReservationDate.Date == input.ReservationDate.Date &&
                                  r.RoomType == "Private" &&
                                  r.Status != ReservationStatus.Cancelled &&
                                  r.StartTime.HasValue && r.EndTime.HasValue &&
                                  !(r.EndTime <= startTimeSpan || r.StartTime >= endTimeSpan), cancellationToken);
            }
            else
            {
                var reservationsInTimeSlot = await _context.Reservations
                    .Where(r => r.CoworkingAddressId == input.CoworkingAddressId &&
                               r.ReservationDate.Date == input.ReservationDate.Date &&
                               r.RoomType == "Shared" &&
                               r.Status != ReservationStatus.Cancelled &&
                               r.StartTime.HasValue && r.EndTime.HasValue &&
                               !(r.EndTime <= startTimeSpan || r.StartTime >= endTimeSpan))
                    .CountAsync(cancellationToken);
                hasConflict = reservationsInTimeSlot >= maxSeats;
            }
            if (hasConflict)
            {
                var errMsg = input.RoomType == "Private"
                    ? msgPrivateRoomConflict
                    : $"Não há carteiras disponíveis neste horário. Máximo: {maxSeats} carteiras.";
                return new CreateReservationResult { Success = false, ErrorMessage = errMsg };
            }
            var reservation = new Reservation
            {
                ClientId = client.Id,
                CoworkingAddressId = input.CoworkingAddressId,
                ReservationDate = input.ReservationDate.Date,
                StartTime = startTimeSpan,
                EndTime = endTimeSpan,
                RoomType = input.RoomType,
                TotalValue = totalValue,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync(cancellationToken);
            var payment = new Payment
            {
                ReservationId = reservation.Id,
                Amount = totalValue,
                Status = PaymentStatus.Pending,
                Method = PaymentMethod.PIX,
                DueDate = input.ReservationDate.Date,
                ExternalPaymentId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
            return new CreateReservationResult { Success = true, PaymentId = payment.Id };
        }

        public async Task<object?> GetAvailabilityForMonthAsync(int addressId, string roomType, int year, int month, CancellationToken cancellationToken = default)
        {
            const string errAddressNotFound = "Endereço não encontrado";
            const string errInvalidRoomType = "Tipo de sala inválido";
            const string errNoShared = "Endereço não oferece sala compartilhada";
            const string errNoPrivate = "Endereço não oferece sala privativa";

            try
            {
                var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
                if (address == null)
                    return new { error = errAddressNotFound };
                if (roomType != "Shared" && roomType != "Private")
                    return new { error = errInvalidRoomType };
                if (roomType == "Shared" && !address.OffersSharedRoom)
                    return new { error = errNoShared };
                if (roomType == "Private" && !address.OffersPrivateRoom)
                    return new { error = errNoPrivate };
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var dates = new Dictionary<string, object>();
                var operatingHours = await _context.CoworkingOperatingHours
                    .Where(h => h.CoworkingId == address.CoworkingId)
                    .ToListAsync(cancellationToken);
                var blockedDates = await _context.BlockedDates
                    .Where(b => b.CoworkingId == address.CoworkingId &&
                               b.BlockedDateValue.Year == year &&
                               b.BlockedDateValue.Month == month)
                    .ToListAsync(cancellationToken);
                var first = new DateTime(year, month, 1);
                var last = new DateTime(year, month, daysInMonth);
                var reservations = await _context.Reservations
                    .Where(r => r.CoworkingAddressId == addressId &&
                               r.ReservationDate >= first && r.ReservationDate <= last &&
                               r.RoomType == roomType &&
                               r.Status != ReservationStatus.Cancelled &&
                               r.StartTime.HasValue && r.EndTime.HasValue)
                    .Select(r => new { r.ReservationDate, r.StartTime, r.EndTime })
                    .ToListAsync(cancellationToken);
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
                    var blockedDate = blockedDates.FirstOrDefault(b => b.BlockedDateValue.Date == dt.Date);
                    if (blockedDate != null && blockedDate.BlocksFullDay)
                    {
                        dates[key] = new { fullyBooked = true, occupied = Array.Empty<object>() };
                        continue;
                    }
                    var dayOfWeek = dt.DayOfWeek.ToString().ToUpper();
                    var operatingHour = operatingHours.FirstOrDefault(h => h.DayOfWeek == dayOfWeek);
                    if (operatingHour == null || !operatingHour.IsActive || !operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                    {
                        dates[key] = new { fullyBooked = true, occupied = Array.Empty<object>() };
                        continue;
                    }
                    var opStart = operatingHour.OpeningTime!.Value;
                    var opEnd = operatingHour.ClosingTime!.Value;
                    var dayRes = reservations.Where(r => r.ReservationDate.Date == dt).ToList();
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
                                    segEnd = fullSlots[i].Add(TimeSpan.FromMinutes(30));
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
                return new { dates };
            }
            catch
            {
                return null;
            }
        }

        public async Task<object?> GetAvailabilityAsync(int addressId, string roomType, DateTime? date, CancellationToken cancellationToken = default)
        {
            const string errAddressNotFound = "Endereço não encontrado";
            const string errInvalidRoomType = "Tipo de sala inválido";
            const string errNoShared = "Endereço não oferece sala compartilhada";
            const string errNoPrivate = "Endereço não oferece sala privativa";
            const string errNotOpen = "Coworking não funciona neste dia";

            try
            {
                var address = await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
                if (address == null)
                    return new { error = errAddressNotFound };
                if (roomType != "Shared" && roomType != "Private")
                    return new { error = errInvalidRoomType };
                if (roomType == "Shared" && !address.OffersSharedRoom)
                    return new { error = errNoShared };
                if (roomType == "Private" && !address.OffersPrivateRoom)
                    return new { error = errNoPrivate };
                if (date.HasValue)
                {
                    var blockedDate = await _context.BlockedDates
                        .FirstOrDefaultAsync(b => b.CoworkingId == address.CoworkingId && b.BlockedDateValue.Date == date.Value.Date, cancellationToken);
                    if (blockedDate != null && blockedDate.BlocksFullDay)
                    {
                        return new { available = false, occupiedSlots = Array.Empty<object>(), error = $"Data bloqueada: {blockedDate.Reason}" };
                    }
                    var dayOfWeek = date.Value.DayOfWeek.ToString().ToUpper();
                    var operatingHour = await _context.CoworkingOperatingHours
                        .FirstOrDefaultAsync(h => h.CoworkingId == address.CoworkingId && h.DayOfWeek == dayOfWeek, cancellationToken);
                    if (operatingHour == null || !operatingHour.IsActive || !operatingHour.OpeningTime.HasValue || !operatingHour.ClosingTime.HasValue)
                    {
                        return new { available = false, occupiedSlots = Array.Empty<object>(), error = errNotOpen };
                    }
                    var reservations = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate.Date == date.Value.Date &&
                                   r.RoomType == roomType &&
                                   r.Status != ReservationStatus.Cancelled &&
                                   r.StartTime.HasValue && r.EndTime.HasValue)
                        .Select(r => new { r.StartTime, r.EndTime })
                        .ToListAsync(cancellationToken);
                    if (blockedDate != null && !blockedDate.BlocksFullDay && blockedDate.StartTime.HasValue && blockedDate.EndTime.HasValue)
                    {
                        reservations.Add(new { StartTime = (TimeSpan?)blockedDate.StartTime, EndTime = (TimeSpan?)blockedDate.EndTime });
                    }
                    if (roomType == "Private")
                    {
                        var privateOccupiedSlots = reservations.Select(r => new { start = r.StartTime!.Value.ToString(@"hh\:mm"), end = r.EndTime!.Value.ToString(@"hh\:mm") }).ToList();
                        return new
                        {
                            available = privateOccupiedSlots.Count == 0,
                            occupiedSlots = privateOccupiedSlots,
                            operatingHours = new { opening = operatingHour.OpeningTime!.Value.ToString(@"hh\:mm"), closing = operatingHour.ClosingTime!.Value.ToString(@"hh\:mm") }
                        };
                    }
                    var maxSeats = address.SharedRoomMaxSeats ?? 0;
                    var occupiedSlots = new List<object>();
                    var timeSlots = new Dictionary<string, int>();
                    foreach (var res in reservations)
                    {
                        var start = res.StartTime!.Value;
                        var end = res.EndTime!.Value;
                        var current = start;
                        while (current < end)
                        {
                            var slotKey = current.ToString(@"hh\:mm");
                            if (!timeSlots.ContainsKey(slotKey)) timeSlots[slotKey] = 0;
                            timeSlots[slotKey]++;
                            current = current.Add(TimeSpan.FromHours(1));
                        }
                    }
                    foreach (var slot in timeSlots)
                    {
                        if (slot.Value >= maxSeats)
                            occupiedSlots.Add(new { start = slot.Key, end = slot.Key });
                    }
                    return new
                    {
                        available = true,
                        occupiedSlots,
                        maxSeats,
                        currentOccupancy = timeSlots,
                        operatingHours = new { opening = operatingHour.OpeningTime!.Value.ToString(@"hh\:mm"), closing = operatingHour.ClosingTime!.Value.ToString(@"hh\:mm") }
                    };
                }
                var unavailableDates = new List<string>();
                var startDate = DateTime.Today;
                var endDate = DateTime.Today.AddDays(30);
                if (roomType == "Private")
                {
                    var reservedDates = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate >= startDate &&
                                   r.ReservationDate <= endDate &&
                                   r.RoomType == "Private" &&
                                   r.Status != ReservationStatus.Cancelled)
                        .Select(r => r.ReservationDate.Date)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                    unavailableDates = reservedDates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
                }
                else
                {
                    var maxSeats = address.SharedRoomMaxSeats ?? 0;
                    var reservationsByDate = await _context.Reservations
                        .Where(r => r.CoworkingAddressId == addressId &&
                                   r.ReservationDate >= startDate &&
                                   r.ReservationDate <= endDate &&
                                   r.RoomType == "Shared" &&
                                   r.Status != ReservationStatus.Cancelled)
                        .GroupBy(r => r.ReservationDate.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .ToListAsync(cancellationToken);
                    unavailableDates = reservationsByDate
                        .Where(r => r.Count >= maxSeats * 8)
                        .Select(r => r.Date.ToString("yyyy-MM-dd"))
                        .ToList();
                }
                return new
                {
                    unavailableDates,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
