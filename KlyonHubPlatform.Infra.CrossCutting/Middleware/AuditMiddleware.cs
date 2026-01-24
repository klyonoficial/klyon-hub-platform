using System.Security.Claims;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Http;

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
            // Verificar se a auditoria deve ser ignorada (ex: durante limpeza de base de dados)
            if (context.Items.ContainsKey("SkipAudit") && (bool)context.Items["SkipAudit"] == true)
            {
                await _next(context);
                return;
            }

            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var action = $"{context.Request.Method} {context.Request.Path}";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            await _next(context);

            // Log apenas ações importantes
            if (context.Request.Method != "GET" && userId != null)
            {
                try
                {
                    int? userIdInt = int.TryParse(userId, out var id) ? id : null;
                    await auditService.LogActionAsync(
                        userIdInt,
                        action,
                        "HTTP",
                        null,
                        null,
                        $"Status: {context.Response.StatusCode}",
                        ipAddress,
                        userAgent
                    );
                }
                catch
                {
                    // Ignorar erros de auditoria para não quebrar o fluxo da aplicação
                    // (ex: durante limpeza de base de dados, migrações, etc.)
                }
            }
        }
    }
}
