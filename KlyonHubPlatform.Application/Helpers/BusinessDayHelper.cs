namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Dias úteis (seg–sex, excluindo feriados nacionais fixos).
    /// Usado na janela Asaas de instrução PIX Automático (2–10 dias úteis antes do vencimento).
    /// </summary>
    public static class BusinessDayHelper
    {
        private static readonly HashSet<(int Month, int Day)> FixedHolidays = new()
        {
            (1, 1),   // Confraternização Universal
            (4, 21),  // Tiradentes
            (5, 1),   // Dia do Trabalho
            (9, 7),   // Independência
            (10, 12), // Nossa Senhora Aparecida
            (11, 2),  // Finados
            (11, 15), // Proclamação da República
            (12, 25)  // Natal
        };

        public static bool IsBusinessDay(DateTime date)
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return false;
            return !FixedHolidays.Contains((date.Month, date.Day));
        }

        /// <summary>
        /// Conta dias úteis estritamente após <paramref name="fromDate"/> até <paramref name="toDate"/> (inclusive).
        /// Ex.: segunda → quarta = 2 (terça e quarta).
        /// </summary>
        public static int CountBusinessDaysUntil(DateTime fromDate, DateTime toDate)
        {
            var from = fromDate.Date;
            var to = toDate.Date;
            if (to <= from)
                return 0;

            var count = 0;
            for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            {
                if (IsBusinessDay(d))
                    count++;
            }
            return count;
        }

        /// <summary>Asaas exige criar a instrução entre 2 e 10 dias úteis antes do vencimento.</summary>
        public static bool IsWithinPixAutomaticInstructionWindow(DateTime today, DateTime dueDate, int minBusinessDays = 2, int maxBusinessDays = 10)
        {
            var days = CountBusinessDaysUntil(today, dueDate);
            return days >= minBusinessDays && days <= maxBusinessDays;
        }

        public static DateTime AddBusinessDays(DateTime start, int businessDays)
        {
            var date = start.Date;
            var added = 0;
            while (added < businessDays)
            {
                date = date.AddDays(1);
                if (IsBusinessDay(date))
                    added++;
            }
            return date;
        }
    }
}
