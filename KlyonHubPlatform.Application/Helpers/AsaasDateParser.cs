using System.Globalization;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Parse de datas no formato retornado pela API Asaas (reutilizável em mapeamentos e serviços).
    /// </summary>
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
