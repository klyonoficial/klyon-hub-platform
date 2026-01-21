namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Comissão da plataforma sobre uma reserva (percentual parametrizável).
    /// Gerado quando a reserva recebe dupla confirmação (cliente + coworking) e o repasse é feito.
    /// </summary>
    public class ReservationCommission
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal CommissionPercentage { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal CoworkingAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Reservation Reservation { get; set; } = null!;
    }
}
