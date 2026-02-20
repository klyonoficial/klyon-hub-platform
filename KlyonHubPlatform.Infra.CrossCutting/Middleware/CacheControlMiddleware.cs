using KlyonHubPlatform.Domain.Core.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace KlyonHubPlatform.Infra.CrossCutting.Middleware
{
    /// <summary>
    /// Middleware para gerenciar cache de navegador via headers HTTP.
    /// Dados públicos podem usar cache, dados sensíveis (financeiros, administrativos, contratuais) devem usar no-store.
    /// </summary>
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
            // Verificar se o cache está desabilitado via configuração
            var disableBrowserCache = _configuration.GetValue<bool>("AppSettings:DisableBrowserCache", false);
            
            // Determinar o tipo de cache ANTES de processar a requisição
            var path = context.Request.Path.Value?.ToLower() ?? "";
            string? cacheControlValue = null;
            string? pragmaValue = null;
            string? expiresValue = null;

            // Se o cache estiver desabilitado, aplicar no-cache para todas as rotas de Client, Coworking e Admin
            if (disableBrowserCache && (IsClientRoute(path) || IsCoworkingRoute(path) || IsAdminRoute(path)))
            {
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            // Página principal e Home: nunca cachear — a resposta pode ser redirect (FiscalAddress) ou Index conforme EnableCoworkingMenus
            else if (IsHomeOrRootRoute(path))
            {
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            // Rotas sensíveis que NÃO devem ser cacheadas
            // (financeiras, administrativas, contratuais, dados pessoais)
            else if (IsSensitiveRoute(path))
            {
                // Dados sensíveis: não armazenar em cache
                cacheControlValue = "no-store, no-cache, must-revalidate, private";
                pragmaValue = "no-cache";
                expiresValue = "0";
            }
            // Rotas públicas que podem usar cache
            else if (IsPublicRoute(path))
            {
                // Dados públicos: cache por 24 horas (86400 segundos)
                cacheControlValue = "public, max-age=86400";
            }
            // Rotas estáticas (CSS, JS, imagens) - já gerenciadas pelo UseStaticFiles
            // mas podemos garantir cache mais longo se necessário
            else if (IsStaticAsset(path))
            {
                cacheControlValue = "public, max-age=31536000"; // 1 ano
            }
            // Rotas padrão: cache moderado (1 hora)
            else
            {
                cacheControlValue = "public, max-age=3600";
            }

            // Aplicar headers ANTES de processar a requisição
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

            // Processar a requisição
            await _next(context);
        }

        /// <summary>
        /// Verifica se é a raiz ou rotas do Home (Index, FiscalAddress, Salas).
        /// Não cachear: a decisão de redirecionar ou exibir a página depende de EnableCoworkingMenus.
        /// </summary>
        private bool IsHomeOrRootRoute(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
                return true;
            path = path.TrimEnd('/');
            if (path == "" || path == "/home" || path == "/home/index" || path == "/home/fiscaladdress" || path == "/home/salas")
                return true;
            return path.StartsWith("/home/");
        }

        /// <summary>
        /// Verifica se a rota é do Client.
        /// </summary>
        private bool IsClientRoute(string path)
        {
            return path.StartsWith("/client/");
        }

        /// <summary>
        /// Verifica se a rota é do Coworking/Parceiro.
        /// </summary>
        private bool IsCoworkingRoute(string path)
        {
            return path.StartsWith("/coworking/") || 
                   path.StartsWith("/parceiro/") || 
                   path.StartsWith("/partner/");
        }

        /// <summary>
        /// Verifica se a rota é do Admin.
        /// </summary>
        private bool IsAdminRoute(string path)
        {
            return path.StartsWith("/admin/");
        }

        /// <summary>
        /// Verifica se a rota contém dados sensíveis que não devem ser cacheados.
        /// </summary>
        private bool IsSensitiveRoute(string path)
        {
            // Rotas administrativas
            if (path.StartsWith("/admin/"))
            {
                // Exceções: algumas rotas admin podem ser públicas (mas por segurança, vamos bloquear todas)
                return true;
            }

            // Rotas financeiras
            if (path.Contains("/payment") || 
                path.Contains("/payout") || 
                path.Contains("/commission") ||
                path.Contains("/credit") ||
                path.Contains("/pagbank") ||
                path.Contains("/webhook"))
            {
                return true;
            }

            // Rotas contratuais
            if (path.Contains("/contract") || 
                path.Contains("/subscription"))
            {
                return true;
            }

            // Rotas de autenticação
            if (path.Contains("/auth/") || 
                path.Contains("/login") || 
                path.Contains("/logout"))
            {
                return true;
            }

            // Rotas de dados pessoais do cliente
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

            // Rotas de coworking/parceiro com dados sensíveis
            if (path.Contains("/coworking/") || path.Contains("/parceiro/") || path.Contains("/partner/"))
            {
                // Rotas financeiras do parceiro
                if (path.Contains("/financial") || 
                    path.Contains("/financial/history") ||
                    path.Contains("/commissions"))
                {
                    return true;
                }
                
                // Rotas de dados pessoais e sensíveis
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

            // Rotas de documentos e auditoria
            if (path.Contains("/document") || 
                path.Contains("/audit") ||
                path.Contains("/fiscalnote"))
            {
                return true;
            }

            // Rotas de relatórios
            if (path.Contains("/report"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica se a rota contém dados públicos que podem ser cacheados.
        /// </summary>
        private bool IsPublicRoute(string path)
        {
            // Home, páginas públicas
            if (path == "/" || 
                path.StartsWith("/home/") ||
                path.StartsWith("/public/"))
            {
                return true;
            }

            // Páginas de informações públicas sobre coworkings (sem dados sensíveis)
            if (path.Contains("/coworking/") && 
                !path.Contains("/activecontracts") && 
                !path.Contains("/commissions") &&
                !path.Contains("/contractdetails") &&
                !path.Contains("/mycontract") &&
                !path.Contains("/profile") &&
                !path.Contains("/sefaz"))
            {
                // Apenas listagens públicas, não dados pessoais
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica se é um arquivo estático (CSS, JS, imagens, etc).
        /// </summary>
        private bool IsStaticAsset(string path)
        {
            var staticExtensions = new[] { ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot" };
            return staticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
