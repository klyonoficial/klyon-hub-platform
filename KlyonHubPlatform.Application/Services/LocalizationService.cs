using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class LocalizationService : ILocalizationService
    {
        private readonly ApplicationDbContext _context;

        public LocalizationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetTranslationAsync(string key, string languageCode)
        {
            var translation = await _context.Translations
                .FirstOrDefaultAsync(t => t.Key == key && t.LanguageCode == languageCode);

            if (translation != null)
                return translation.Value;

            // Fallback para pt-BR se não encontrar tradução
            var fallback = await _context.Translations
                .FirstOrDefaultAsync(t => t.Key == key && t.LanguageCode == "pt-BR");

            return fallback?.Value ?? key;
        }

        public async Task<Dictionary<string, string>> GetTranslationsByCategoryAsync(string category, string languageCode)
        {
            var translations = await _context.Translations
                .Where(t => t.Category == category && t.LanguageCode == languageCode)
                .ToDictionaryAsync(t => t.Key, t => t.Value);

            return translations;
        }

        public async Task<List<Domain.Entities.Language>> GetAvailableLanguagesAsync()
        {
            return await _context.Languages
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();
        }

        public async Task SetDefaultLanguageAsync(string languageCode)
        {
            var languages = await _context.Languages.ToListAsync();
            
            foreach (var lang in languages)
            {
                lang.IsDefault = lang.Code == languageCode;
            }

            await _context.SaveChangesAsync();
        }
    }
}
