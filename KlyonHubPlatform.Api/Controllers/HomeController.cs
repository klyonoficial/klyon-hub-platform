using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KlyonHubPlatform.Api.ViewModels;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.AppHome;
namespace KlyonHubPlatform.Api.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISystemConfigurationService _systemConfig;
        private readonly IAppHomeService _appHome;
        public HomeController(ISystemConfigurationService systemConfig, IAppHomeService appHome)
        {
            _systemConfig = systemConfig;
            _appHome = appHome;
        }
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData[ViewDataKeys.EnableCoworkingMenus] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            var soleAddressId = await _appHome.GetSolePublishedAddressIdAsync(cancellationToken);
            if (soleAddressId.HasValue)
            {
                return RedirectToAction(ActionNames.AddressDetail, ControllerNames.Client, new { coworkingAddressId = soleAddressId.Value });
            }
            return View();
        }
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> FiscalAddress(CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                return RedirectToAction(ActionNames.ChooseLocation, ControllerNames.Client);
            }
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData[ViewDataKeys.EnableCoworkingMenus] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            return View();
        }
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Rooms(CancellationToken cancellationToken = default)
        {
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            if (StringBooleanValue.IsFalse(enableCoworkingMenus))
            {
                return RedirectToAction(ActionNames.Index);
            }
            ViewData[ViewDataKeys.EnableCoworkingMenus] = true;
            return View();
        }
        [ResponseCache(NoStore = true, Duration = 0, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Partner(CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.CoworkingPartner))
            {
                return RedirectToAction(ActionNames.Dashboard, ControllerNames.Coworking);
            }
            var enableCoworkingMenus = (await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken))?.Trim();
            ViewData[ViewDataKeys.EnableCoworkingMenus] = !StringBooleanValue.IsFalse(enableCoworkingMenus);
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Marketplace([FromQuery] MarketplaceSearchVm search, CancellationToken cancellationToken = default)
        {
            var searchDto = new MarketplaceSearchDto
            {
                State = search.State,
                City = search.City,
                Neighborhood = search.Neighborhood,
                MinPeople = search.MinPeople,
                MaxPeople = search.MaxPeople,
                Page = search.Page <= 0 ? 1 : search.Page,
                PageSize = 10
            };
            var result = await _appHome.GetMarketplaceListAsync(searchDto, cancellationToken);
            if (result == null)
                return View(new MarketplaceListVm { Search = search });
            search.Page = result.Search.Page;
            search.PageSize = result.Search.PageSize;
            search.State = result.Search.State;
            search.City = result.Search.City;
            ViewBag.CoworkingProducts = result.CoworkingProducts;
            var vm = new MarketplaceListVm
            {
                Search = search,
                AvailableStates = result.AvailableStates,
                AvailableCities = result.AvailableCities,
                Items = result.Items.Select(dto => new MarketplaceCardVm
                {
                    CoworkingAddressId = dto.CoworkingAddressId,
                    Title = dto.Title,
                    Subtitle = dto.Subtitle,
                    CityState = dto.CityState,
                    BasePrice = dto.BasePrice,
                    IsAvailable = dto.IsAvailable,
                    CurrentContracts = dto.CurrentContracts,
                    MaxContracts = dto.MaxContracts,
                    MaxPeople = dto.MaxPeople,
                    MainPhotoId = dto.MainPhotoId,
                    OffersSharedRoom = dto.OffersSharedRoom,
                    SharedRoomPricePerHour = dto.SharedRoomPricePerHour,
                    OffersPrivateRoom = dto.OffersPrivateRoom,
                    PrivateRoomPricePerHour = dto.PrivateRoomPricePerHour
                }).ToList(),
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            };
            return View(vm);
        }
        [HttpGet]
        public async Task<IActionResult> MarketplaceDetails(int id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            int? userId = null;
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Client))
            {
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
                    userId = uid;
            }
            var result = await _appHome.GetMarketplaceDetailsAsync(id, userId, cancellationToken);
            if (result == null || result.Address == null || result.Coworking == null)
                return NotFound();
            ViewBag.AverageRating = result.AverageRating;
            ViewBag.TotalRatings = result.TotalRatings;
            ViewBag.Ratings = result.Ratings;
            ViewBag.IsClientLogged = result.IsClientLogged;
            ViewBag.AvailableCredits = result.AvailableCredits;
            ViewBag.OffersSharedRoom = result.OffersSharedRoom;
            ViewBag.OffersPrivateRoom = result.OffersPrivateRoom;
            ViewBag.SharedRoomPricePerHour = result.SharedRoomPricePerHour;
            ViewBag.SharedRoomMaxSeats = result.SharedRoomMaxSeats;
            ViewBag.PrivateRoomPricePerHour = result.PrivateRoomPricePerHour;
            ViewBag.PrivateRoomMaxSeats = result.PrivateRoomMaxSeats;
            var vm = new MarketplaceDetailsVm
            {
                Address = result.Address,
                Coworking = result.Coworking,
                PhotoIds = result.PhotoIds,
                MainPhotoId = result.MainPhotoId,
                ReturnUrl = returnUrl
            };
            return View(vm);
        }
        [HttpPost]
        public async Task<IActionResult> SubmitInterest([FromForm] InterestFormVm model, CancellationToken cancellationToken = default)
        {
            var input = new SubmitInterestInput
            {
                CoworkingAddressId = model.CoworkingAddressId,
                AllocationDate = model.AllocationDate,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                Name = model.Name ?? "",
                LastName = model.LastName ?? "",
                Email = model.Email ?? "",
                Phone = model.Phone ?? ""
            };
            var result = await _appHome.SubmitInterestAsync(input, cancellationToken);
            if (!result.Success)
            {
                TempData[ErrorMessages.TempDataKey] = result.ErrorMessage ?? ErrorMessages.FillAllRequiredFields;
                if (result.ErrorMessage == "Endereço não encontrado.")
                    return RedirectToAction(ActionNames.Marketplace);
                return RedirectToAction(ActionNames.MarketplaceDetails, new { id = model.CoworkingAddressId });
            }
            TempData[SuccessMessage.TempDataKey] = SuccessMessage.InterestRegistered;
            return RedirectToAction(ActionNames.MarketplaceDetails, new { id = model.CoworkingAddressId });
        }
        [HttpPost]
        [Authorize(Roles = RoleNames.Client)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(int CoworkingAddressId, DateTime ReservationDate, string? StartTime, string? EndTime, string RoomType = "Shared", CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var input = new CreateReservationInput
            {
                UserId = userId,
                CoworkingAddressId = CoworkingAddressId,
                ReservationDate = ReservationDate,
                StartTime = StartTime,
                EndTime = EndTime,
                RoomType = RoomType
            };
            try
            {
                var result = await _appHome.CreateReservationAsync(input, cancellationToken);
                if (!result.Success)
                {
                    TempData[ErrorMessages.TempDataKey] = result.ErrorMessage ?? ErrorMessages.ClientNotFound;
                    return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
                }
                return RedirectToAction(ActionNames.PaymentDetails, ControllerNames.Client, new { paymentId = result.PaymentId });
            }
            catch (Exception ex)
            {
                TempData[ErrorMessages.TempDataKey] = ErrorMessages.ErrorWithException("criar reserva", ex.Message);
                return RedirectToAction(ActionNames.MarketplaceDetails, new { id = CoworkingAddressId });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetAvailabilityForMonth(int addressId, string roomType, int year, int month, CancellationToken cancellationToken = default)
        {
            var result = await _appHome.GetAvailabilityForMonthAsync(addressId, roomType, year, month, cancellationToken);
            if (result == null)
                return Json(new { error = ErrorJson.AddressNotFound });
            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAvailability(int addressId, string roomType, DateTime? date = null, CancellationToken cancellationToken = default)
        {
            var result = await _appHome.GetAvailabilityAsync(addressId, roomType, date, cancellationToken);
            if (result == null)
                return Json(new { error = ErrorJson.AddressNotFound });
            return Json(result);
        }
        public IActionResult Privacy()
        {
            return View();
        }
        [AllowAnonymous]
        [HttpGet]
        public IActionResult RequestEntityTooLarge()
        {
            Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return View();
        }
        [AllowAnonymous]
        [HttpGet]
        public IActionResult TermosUsoLgpd()
        {
            return View();
        }
    }
}