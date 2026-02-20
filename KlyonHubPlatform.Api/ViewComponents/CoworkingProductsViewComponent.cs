using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Api.Constants;
using System.Security.Claims;
namespace KlyonHubPlatform.Api.ViewComponents
{
    public class CoworkingProductsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        public CoworkingProductsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new CoworkingProductsModel
            {
                HasFiscalAddress = false,
                HasRoomRental = false
            };
            var user = HttpContext.User as ClaimsPrincipal;
            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(RoleNames.CoworkingPartner))
            {
                return View(model);
            }
            try
            {
                var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return View(model);
                }
                var coworking = await _context.Coworkings
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                if (coworking == null)
                {
                    return View(model);
                }
                var products = await _context.CoworkingProducts
                    .Where(p => p.CoworkingId == coworking.Id && p.IsActive)
                    .Select(p => p.ProductType)
                    .ToListAsync();
                model.HasFiscalAddress = products.Contains("FiscalAddress");
                model.HasRoomRental = products.Contains("RoomRental");
                ViewContext.ViewData[ViewDataKeys.HasFiscalAddress] = model.HasFiscalAddress;
                ViewContext.ViewData[ViewDataKeys.HasRoomRental] = model.HasRoomRental;
                return View(model);
            }
            catch
            {
                ViewContext.ViewData[ViewDataKeys.HasFiscalAddress] = false;
                ViewContext.ViewData[ViewDataKeys.HasRoomRental] = false;
                return View(model);
            }
        }
    }
}