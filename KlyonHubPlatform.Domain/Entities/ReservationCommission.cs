namespace KlyonHubPlatform.Domain.Entities
{
    public class ReservationCommission
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal CommissionPercentage { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal CoworkingAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual Reservation Reservation { get; set; } = null!;
    }
}