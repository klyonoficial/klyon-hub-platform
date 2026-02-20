using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using KlyonHubPlatform.Domain.Core.Constants;

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
                culture = CultureNames.Default;
            }

            // Validar se a cultura é suportada
            var supportedCultures = new[] { CultureNames.PtBr, CultureNames.EnUs, CultureNames.EsEs };
            if (!supportedCultures.Contains(culture))
            {
                culture = CultureNames.Default;
            }

            // Definir o cookie de cultura
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions 
                { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                }
            );

            // Garantir que a cultura seja aplicada imediatamente
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            // Redirecionar para a URL de retorno ou página inicial
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("/");
        }
    }
}
