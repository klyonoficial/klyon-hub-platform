namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Sala reservável vinculada a um endereço. Cada sala é gerenciada individualmente:
    /// capacidade, créditos por hora, fotos. Não pode haver reserva duplicada no mesmo horário/dia.
    /// </summary>
    public class CoworkingRoom
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        /// <summary>Shared, Private ou Auditorium</summary>
        public string RoomType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>Quantidade de pessoas que a sala suporta</summary>
        public int Capacity { get; set; }
        /// <summary>Créditos cobrados por hora de uso</summary>
        public int CreditsPerHour { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual ICollection<CoworkingRoomPhoto> Photos { get; set; } = new List<CoworkingRoomPhoto>();
    }
}
