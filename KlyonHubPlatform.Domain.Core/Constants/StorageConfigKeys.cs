namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves e valores de configuração do provedor de storage (FTP, AWS S3, etc.).
    /// </summary>
    public static class StorageConfigKeys
    {
        /// <summary>Chave que define qual provedor de storage usar: "FTP", "AWS", "S3".</summary>
        public const string Provider = "Storage.Provider";
    }
}
