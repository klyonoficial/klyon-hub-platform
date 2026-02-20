namespace KlyonHubPlatform.Application.Interfaces;
public interface IAppBaseUrlProvider
{
    Task<string> GetBaseUrlAsync();
}