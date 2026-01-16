namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ILocalizationService
    {
        Task<string> GetTranslationAsync(string key, string languageCode);
        Task<Dictionary<string, string>> GetTranslationsByCategoryAsync(string category, string languageCode);
        Task<List<Domain.Entities.Language>> GetAvailableLanguagesAsync();
        Task SetDefaultLanguageAsync(string languageCode);
    }
}
