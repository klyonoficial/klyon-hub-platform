using System.Globalization;
namespace KlyonHubPlatform.Application.Helpers
{
    public static class AsaasDateParser
    {
        /// <summary>
        /// Parse de datas Asaas. Strings só-data (<c>yyyy-MM-dd</c>) viram calendário Unspecified
        /// (não UTC), para não sofrer conversão -3h no SaveChanges.
        /// </summary>
        public static DateTime? TryParse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            if (trimmed.Length == 10
                && DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
                return BrazilDateTime.ToCalendarDate(dateOnly);
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            return null;
        }
    }
}