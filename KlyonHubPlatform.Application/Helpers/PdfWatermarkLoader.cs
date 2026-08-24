using Microsoft.Extensions.Hosting;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class PdfWatermarkLoader
    {
        public static byte[]? LoadKlyonLogo(IHostEnvironment environment)
        {
            var logoPath = Path.Combine(
                environment.ContentRootPath,
                "wwwroot",
                "images",
                "1-logo-klyon-transparente -alta-resolucao.png");
            return File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;
        }
    }
}
