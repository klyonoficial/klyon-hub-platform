using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;

namespace KlyonHubPlatform.Api.Controllers
{
    public class LocalizationController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (string.IsNullOrEmpty(culture))
            {
                culture = "pt-BR";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions 
                { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true
                }
            );

            return LocalRedirect(returnUrl ?? "/");
        }
    }
}
