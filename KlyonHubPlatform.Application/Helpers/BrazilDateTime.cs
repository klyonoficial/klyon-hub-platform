namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Relógio de negócio em America/Sao_Paulo (datas de vencimento de cobrança).
    /// </summary>
    public static class BrazilDateTime
    {
        private static readonly TimeZoneInfo SaoPaulo =
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        public static DateTime Now
        {
            get
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SaoPaulo);
                }
                catch (TimeZoneNotFoundException)
                {
                    return DateTime.UtcNow.AddHours(-3);
                }
                catch (InvalidTimeZoneException)
                {
                    return DateTime.UtcNow.AddHours(-3);
                }
            }
        }

        public static DateTime Today => Now.Date;

        /// <summary>
        /// Vencido somente após o dia de vencimento em horário de Brasília (não no próprio dia).
        /// </summary>
        public static bool IsCalendarDueDateOverdue(DateTime dueDate, DateTime? referenceBrazilNow = null)
        {
            var today = (referenceBrazilNow ?? Now).Date;
            return dueDate.Date < today;
        }
    }
}
