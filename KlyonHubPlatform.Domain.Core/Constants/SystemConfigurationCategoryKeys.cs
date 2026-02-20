namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves de categorias das configurações do sistema (SystemConfiguration.Category).
    /// Use estas constantes em vez de strings fixas em SeedService, AdminController e views.
    /// </summary>
    public static class SystemConfigurationCategoryKeys
    {
        public const string Email = "Email";
        public const string Payment = "Payment";
        public const string Storage = "Storage";
        public const string D4Sign = "D4Sign";
        public const string Upload = "Upload";
        public const string General = "General";
        public const string Features = "Features";
        public const string Platform = "Platform";
    }
}
