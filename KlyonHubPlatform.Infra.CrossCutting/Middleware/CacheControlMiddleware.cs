using KlyonHubPlatform.Domain.Core.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace KlyonHubPlatform.Infra.CrossCutting.Middleware
{
    public class CacheControlMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        public CacheControlMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            var disableBrowserCache = _configuration.GetValue<bool>("AppSettings:DisableBrowserCache", false);
            var path = context.Request.Path.Value?.ToLower() ?? "";
            string? cacheControlValue = null;
            string? pragmaValue = null;
            string? expiresValue = null;
            if (disableBrowserCache && (IsClientRoute(path) || IsCoworkingRoute(path) || IsAdminRoute(path)))
            {
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            else if (IsHomeOrRootRoute(path))
            {
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            else if (IsSensitiveRoute(path))
            {
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            else if (IsPublicRoute(path))
            {
                cacheControlValue = "public, max-age=86400";
            }
            else if (IsStaticAsset(path))
            {
                cacheControlValue = "public, max-age=31536000";
            }
            else
            {
                cacheControlValue = "public, max-age=3600";
            }
            if (!string.IsNullOrEmpty(cacheControlValue))
            {
                context.Response.Headers[HttpHeaderNames.CacheControl] = cacheControlValue;
            }
            if (!string.IsNullOrEmpty(pragmaValue))
            {
                context.Response.Headers[HttpHeaderNames.Pragma] = pragmaValue;
            }
            if (!string.IsNullOrEmpty(expiresValue))
            {
                context.Response.Headers[HttpHeaderNames.Expires] = expiresValue;
            }
            await _next(context);
        }
        private bool IsHomeOrRootRoute(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
                return true;
            path = path.TrimEnd('/');
            if (path == "" || path == "/home" || path == "/home/index" || path == "/home/fiscaladdress" || path == "/home/salas")
                return true;
            return path.StartsWith("/home/");
        }
        private bool IsClientRoute(string path)
        {
            return path.StartsWith("/client/");
        }
        private bool IsCoworkingRoute(string path)
        {
            return path.StartsWith("/coworking/") || 
                   path.StartsWith("/parceiro/") || 
                   path.StartsWith("/partner/");
        }
        private bool IsAdminRoute(string path)
        {
            return path.StartsWith("/admin/");
        }
        private bool IsSensitiveRoute(string path)
        {
            if (path.StartsWith("/admin/"))
            {
                return true;
            }
            if (path.Contains("/payment") || 
                path.Contains("/payout") || 
                path.Contains("/commission") ||
                path.Contains("/credit") ||
                path.Contains("/pagbank") ||
                path.Contains("/webhook"))
            {
                return true;
            }
            if (path.Contains("/contract") || 
                path.Contains("/subscription"))
            {
                return true;
            }
            if (path.Contains("/auth/") || 
                path.Contains("/login") || 
                path.Contains("/logout"))
            {
                return true;
            }
            if (path.Contains("/client/") && 
                (path.Contains("/mycontracts") || 
                 path.Contains("/contractdetails") ||
                 path.Contains("/profile") ||
                 path.Contains("/history") ||
                 path.Contains("/notifications") ||
                 path.Contains("/documents")))
            {
                return true;
            }
            if (path.Contains("/coworking/") || path.Contains("/parceiro/") || path.Contains("/partner/"))
            {
                if (path.Contains("/financial") || 
                    path.Contains("/financial/history") ||
                    path.Contains("/commissions"))
                {
                    return true;
                }
                if (path.Contains("/activecontracts") || 
                    path.Contains("/contractdetails") ||
                    path.Contains("/mycontract") ||
                    path.Contains("/profile") ||
                    path.Contains("/sefaz") ||
                    path.Contains("/notifications") ||
                    (path.Contains("/addresses/") && path.Contains("/capacityrequest")))
                {
                    return true;
                }
            }
            if (path.Contains("/document") || 
                path.Contains("/audit") ||
                path.Contains("/fiscalnote"))
            {
                return true;
            }
            if (path.Contains("/report"))
            {
                return true;
            }
            return false;
        }
        private bool IsPublicRoute(string path)
        {
            if (path == "/" || 
                path.StartsWith("/home/") ||
                path.StartsWith("/public/"))
            {
                return true;
            }
            if (path.Contains("/coworking/") && 
                !path.Contains("/activecontracts") && 
                !path.Contains("/commissions") &&
                !path.Contains("/contractdetails") &&
                !path.Contains("/mycontract") &&
                !path.Contains("/profile") &&
                !path.Contains("/sefaz"))
            {
                return true;
            }
            return false;
        }
        private bool IsStaticAsset(string path)
        {
            var staticExtensions = new[] { ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot" };
            return staticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}