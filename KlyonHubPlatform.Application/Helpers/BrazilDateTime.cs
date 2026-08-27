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
        /// Data de calendário (só o dia) com <see cref="DateTimeKind.Unspecified"/>.
        /// Não usar Kind=Utc em dueDate do Asaas: <c>SaveChanges</c> converteria UTC→BRT e atrasaria 1 dia
        /// (ex.: 27/08 00:00 UTC vira 26/08 21:00 e o badge “Vencido” dispara cedo demais).
        /// </summary>
        public static DateTime ToCalendarDate(DateTime value) =>
            DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);

        /// <summary>
        /// Vencido somente após o dia de vencimento em horário de Brasília (não no próprio dia).
        /// Compara apenas a data de calendário (ignora hora / Kind).
        /// </summary>
        public static bool IsCalendarDueDateOverdue(DateTime dueDate, DateTime? referenceBrazilNow = null)
        {
            var today = (referenceBrazilNow ?? Now).Date;
            return ToCalendarDate(dueDate) < today;
        }
    }
}
