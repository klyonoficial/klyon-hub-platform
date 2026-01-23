using System;

namespace KlyonHubPlatform.Infra.CrossCutting.Helpers
{
    /// <summary>
    /// Helper para trabalhar com datas e horários no fuso horário do Brasil (America/Sao_Paulo)
    /// </summary>
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo BrazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        /// <summary>
        /// Retorna a data e hora atual no fuso horário do Brasil (America/Sao_Paulo)
        /// Equivalente ao horário de Brasília (UTC-3)
        /// </summary>
        public static DateTime GetBrazilianTime()
        {
            try
            {
                // Converter UTC para horário de Brasília
                var utcNow = DateTime.UtcNow;
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, BrazilTimeZone);
            }
            catch
            {
                // Fallback: se não conseguir obter o timezone, usar UTC-3 manualmente
                return DateTime.UtcNow.AddHours(-3);
            }
        }

        /// <summary>
        /// Retorna a data e hora atual no fuso horário do Brasil (America/Sao_Paulo)
        /// Alias para GetBrazilianTime() para facilitar o uso
        /// </summary>
        public static DateTime Now => GetBrazilianTime();

        /// <summary>
        /// Converte uma data UTC para o horário de Brasília
        /// </summary>
        public static DateTime ConvertToBrazilianTime(DateTime utcDateTime)
        {
            try
            {
                if (utcDateTime.Kind == DateTimeKind.Unspecified)
                {
                    // Assumir que é UTC se não especificado
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), BrazilTimeZone);
                }
                
                if (utcDateTime.Kind == DateTimeKind.Utc)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BrazilTimeZone);
                }
                
                // Se já está em local time, retornar como está
                return utcDateTime;
            }
            catch
            {
                // Fallback: subtrair 3 horas se não conseguir converter
                return utcDateTime.AddHours(-3);
            }
        }

        /// <summary>
        /// Converte uma data do horário de Brasília para UTC
        /// </summary>
        public static DateTime ConvertToUtc(DateTime brazilianDateTime)
        {
            try
            {
                if (brazilianDateTime.Kind == DateTimeKind.Unspecified)
                {
                    // Assumir que está no horário de Brasília
                    return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(brazilianDateTime, DateTimeKind.Unspecified), BrazilTimeZone);
                }
                
                if (brazilianDateTime.Kind == DateTimeKind.Local)
                {
                    return TimeZoneInfo.ConvertTimeToUtc(brazilianDateTime, BrazilTimeZone);
                }
                
                // Se já está em UTC, retornar como está
                return brazilianDateTime;
            }
            catch
            {
                // Fallback: adicionar 3 horas se não conseguir converter
                return brazilianDateTime.AddHours(3);
            }
        }
    }
}
