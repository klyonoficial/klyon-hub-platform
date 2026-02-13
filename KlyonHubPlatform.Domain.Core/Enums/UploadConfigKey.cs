namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração de upload.
    /// </summary>
    public enum UploadConfigKey
    {
        MaxPhotoSizeMB
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class UploadConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<UploadConfigKey, string> Keys = new Dictionary<UploadConfigKey, string>
        {
            { UploadConfigKey.MaxPhotoSizeMB, "Upload.MaxPhotoSizeMB" }
        };

        public static string ToConfigKey(this UploadConfigKey key) => Keys[key];
    }
}
