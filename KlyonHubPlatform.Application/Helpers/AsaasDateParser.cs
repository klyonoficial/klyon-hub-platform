using System.Globalization;
namespace KlyonHubPlatform.Application.Helpers
{
    public static class AsaasDateParser
    {
        public static DateTime? TryParse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            return null;
        }
    }
}