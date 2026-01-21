namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingProduct
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string ProductType { get; set; } = string.Empty; // "FiscalAddress", "RoomRental", "DigitalCertificate"
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
