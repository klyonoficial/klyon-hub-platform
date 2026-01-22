namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Horários de funcionamento do coworking por dia da semana
    /// </summary>
    public class CoworkingOperatingHours
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        /// <summary>Dia da semana: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday</summary>
        public string DayOfWeek { get; set; } = string.Empty;
        /// <summary>Horário de abertura (formato HH:mm)</summary>
        public TimeSpan? OpeningTime { get; set; }
        /// <summary>Horário de fechamento (formato HH:mm)</summary>
        public TimeSpan? ClosingTime { get; set; }
        /// <summary>Se o coworking está ativo neste dia</summary>
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
