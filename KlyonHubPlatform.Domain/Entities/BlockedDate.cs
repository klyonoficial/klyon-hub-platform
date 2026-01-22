namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Datas bloqueadas para reservas (feriados, manutenção, eventos, etc.)
    /// </summary>
    public class BlockedDate
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        /// <summary>Data bloqueada</summary>
        public DateTime BlockedDateValue { get; set; }
        /// <summary>Motivo do bloqueio (Feriado, Manutenção, Evento interno, etc.)</summary>
        public string Reason { get; set; } = string.Empty;
        /// <summary>Se bloqueia o dia inteiro ou apenas horários específicos</summary>
        public bool BlocksFullDay { get; set; } = true;
        /// <summary>Horário inicial do bloqueio (se não for dia inteiro)</summary>
        public TimeSpan? StartTime { get; set; }
        /// <summary>Horário final do bloqueio (se não for dia inteiro)</summary>
        public TimeSpan? EndTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
