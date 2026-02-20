namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingRoom
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        public string RoomType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int CreditsPerHour { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual ICollection<CoworkingRoomPhoto> Photos { get; set; } = new List<CoworkingRoomPhoto>();
    }
}