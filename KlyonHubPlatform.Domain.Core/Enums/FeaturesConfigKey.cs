namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum FeaturesConfigKey
    {
        EnableCoworkingMenus
    }
    public static class FeaturesConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<FeaturesConfigKey, string> Keys = new Dictionary<FeaturesConfigKey, string>
        {
            { FeaturesConfigKey.EnableCoworkingMenus, "Features.EnableCoworkingMenus" }
        };
        public static string ToConfigKey(this FeaturesConfigKey key) => Keys[key];
    }
}