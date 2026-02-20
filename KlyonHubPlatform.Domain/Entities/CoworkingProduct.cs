namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingProduct
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string ProductType { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
    }
}