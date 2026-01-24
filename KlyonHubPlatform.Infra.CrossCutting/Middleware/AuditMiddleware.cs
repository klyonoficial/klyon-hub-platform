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
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var action = $"{context.Request.Method} {context.Request.Path}";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            await _next(context);

            // Log apenas ações importantes
            // Não criar logs para operações de manutenção do banco de dados (podem falhar se a tabela foi limpa)
            var isDatabaseMaintenance = context.Request.Path.StartsWithSegments("/Admin/ClearDatabase") ||
                                       context.Request.Path.StartsWithSegments("/Admin/DatabaseManager");
            
            if (context.Request.Method != "GET" && userId != null && !isDatabaseMaintenance)
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
                    // Se houver erro ao criar log (ex: tabela AuditLogs foi limpa), ignorar silenciosamente
                    // O tratamento de erro no AuditService já deve ter capturado isso, mas garantimos aqui também
                }
            }
        }
    }
}
