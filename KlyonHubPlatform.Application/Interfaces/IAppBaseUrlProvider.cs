namespace KlyonHubPlatform.Application.Interfaces;

/// <summary>
/// Fornece a URL base da aplicação (scheme + host) para montar links absolutos (emails, redirects, etc.).
/// Em produção atrás de proxy: usa o domínio da requisição atual; caso contrário usa AppSettings.BaseUrl ou localhost.
/// </summary>
public interface IAppBaseUrlProvider
{
    Task<string> GetBaseUrlAsync();
}
