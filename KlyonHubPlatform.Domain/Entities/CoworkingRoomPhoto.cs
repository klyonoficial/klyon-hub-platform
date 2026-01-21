namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingRoomPhoto
    {
        public int Id { get; set; }
        public int CoworkingRoomId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMain { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual CoworkingRoom CoworkingRoom { get; set; } = null!;
    }
}
