using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace KlyonHubPlatform.Api.ViewComponents
{
    public class LastDeployViewComponent : ViewComponent
    {
        private readonly IWebHostEnvironment _env;

        public LastDeployViewComponent(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IViewComponentResult Invoke()
        {
            string? displayDate = null;
            var path = System.IO.Path.Combine(_env.ContentRootPath, "deploy-date.txt");
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var raw = System.IO.File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var toParse = raw.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase) ? raw[..^4].Trim() : raw;
                        if (DateTime.TryParse(toParse, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dtUtc))
                        {
                            DateTime dtBrasilia;
                            try
                            {
                                var brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                                dtBrasilia = TimeZoneInfo.ConvertTimeFromUtc(dtUtc, brasilia);
                            }
                            catch (TimeZoneNotFoundException)
                            {
                                dtBrasilia = dtUtc.AddHours(-3);
                            }
                            displayDate = dtBrasilia.ToString("dd/MM/yyyy 'às' HH:mm:ss");
                        }
                        else
                            displayDate = raw;
                    }
                }
            }
            catch
            {
                // ignorar falha de leitura
            }

            return View(model: displayDate);
        }
    }
}
