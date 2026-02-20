using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Nomes de exibição (tradução) das categorias de configuração do sistema para a UI.
    /// </summary>
    public static class SystemConfigurationCategoryDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { SystemConfigurationCategoryKeys.Email, "E-mail" },
            { SystemConfigurationCategoryKeys.Payment, "Pagamento" },
            { SystemConfigurationCategoryKeys.Storage, "Armazenamento" },
            { SystemConfigurationCategoryKeys.D4Sign, "D4Sign (Assinatura Eletrônica)" },
            { SystemConfigurationCategoryKeys.Upload, "Upload (Fotos)" },
            { SystemConfigurationCategoryKeys.General, "Geral" },
            { SystemConfigurationCategoryKeys.Features, "Funcionalidades" },
            { SystemConfigurationCategoryKeys.Platform, "Plataforma" }
        };

        /// <summary>Retorna o nome de exibição da categoria ou a própria chave se não houver tradução.</summary>
        public static string GetDisplayName(string categoryKey)
        {
            return DisplayNames.TryGetValue(categoryKey ?? "", out var name) ? name : (categoryKey ?? "");
        }
    }
}
