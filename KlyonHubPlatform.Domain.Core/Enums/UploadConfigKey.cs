namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum UploadConfigKey
    {
        MaxPhotoSizeMB
    }
    public static class UploadConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<UploadConfigKey, string> Keys = new Dictionary<UploadConfigKey, string>
        {
            { UploadConfigKey.MaxPhotoSizeMB, "Upload.MaxPhotoSizeMB" }
        };
        public static string ToConfigKey(this UploadConfigKey key) => Keys[key];
    }
}