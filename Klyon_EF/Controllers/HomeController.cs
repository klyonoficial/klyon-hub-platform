using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KlyonEF.Controllers
{
    public class HomeController : Controller
    {
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

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
