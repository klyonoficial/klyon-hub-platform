namespace KlyonHubPlatform.Infra.CrossCutting.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo BrazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public static DateTime GetBrazilianTime()
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, BrazilTimeZone);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(-3);
            }
        }
        public static DateTime Now => GetBrazilianTime();
        public static DateTime ConvertToBrazilianTime(DateTime utcDateTime)
        {
            try
            {
                if (utcDateTime.Kind == DateTimeKind.Unspecified)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), BrazilTimeZone);
                }
                if (utcDateTime.Kind == DateTimeKind.Utc)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BrazilTimeZone);
                }
                return utcDateTime;
            }
            catch
            {
                return utcDateTime.AddHours(-3);
            }
        }
        public static DateTime ConvertToUtc(DateTime brazilianDateTime)
        {
            try
            {
                if (brazilianDateTime.Kind == DateTimeKind.Unspecified)
                {
                    return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(brazilianDateTime, DateTimeKind.Unspecified), BrazilTimeZone);
                }
                if (brazilianDateTime.Kind == DateTimeKind.Local)
                {
                    return TimeZoneInfo.ConvertTimeToUtc(brazilianDateTime, BrazilTimeZone);
                }
                return brazilianDateTime;
            }
            catch
            {
                return brazilianDateTime.AddHours(3);
            }
        }
    }
}