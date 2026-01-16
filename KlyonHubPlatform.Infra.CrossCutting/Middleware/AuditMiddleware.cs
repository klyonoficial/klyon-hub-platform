using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;

namespace KlyonHubPlatform.Infra.CrossCutting.Middleware
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAuditService auditService)
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var action = $"{context.Request.Method} {context.Request.Path}";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            await _next(context);

            // Log apenas ações importantes
            if (context.Request.Method != "GET" && userId != null)
            {
                int? userIdInt = int.TryParse(userId, out var id) ? id : null;
                await auditService.LogActionAsync(
                    userIdInt,
                    action,
                    "HTTP",
                    null,
                    null,
                    $"Status: {context.Response.StatusCode}"
                );
            }
        }
    }
}
