namespace KlyonHubPlatform.Domain.Entities
{
    public class LocationCommission
    {
        public int Id { get; set; }
        public int LocationId { get; set; }
        public decimal CommissionPercentage { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public virtual Location Location { get; set; } = null!;
    }
}