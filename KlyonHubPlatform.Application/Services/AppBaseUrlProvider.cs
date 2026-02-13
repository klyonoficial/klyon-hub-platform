using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace KlyonHubPlatform.Application.Services;

/// <summary>
/// Implementação que prioriza o host da requisição atual (produção atrás de Nginx),
/// depois AppSettings.BaseUrl e por fim https://localhost:5001.
/// </summary>
public class AppBaseUrlProvider : IAppBaseUrlProvider
{
    private readonly ISystemConfigurationService _systemConfigService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppBaseUrlProvider(ISystemConfigurationService systemConfigService, IHttpContextAccessor httpContextAccessor)
    {
        _systemConfigService = systemConfigService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> GetBaseUrlAsync()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null && !string.IsNullOrEmpty(request.Host.Value))
        {
            var scheme = request.Scheme;
            var host = request.Host.Value;
            if (!host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
                return $"{scheme}://{host.TrimEnd('/')}";
        }
        return await _systemConfigService.GetValueAsync(AppSettingsConfigKey.BaseUrl.ToConfigKey()) ?? "https://localhost:5001";
    }
}
