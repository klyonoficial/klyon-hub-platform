using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Registro de chegada de correspondência no endereço do parceiro, associada a um contrato (cliente).
    /// </summary>
    public class Correspondence
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public DateTime ArrivalDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        /// <summary>Status da retirada: aguardando agendamento, agendado ou já retirado.</summary>
        public CorrespondencePickupStatus PickupStatus { get; set; } = CorrespondencePickupStatus.AwaitingSchedule;
        /// <summary>Data e hora agendadas pelo cliente para retirada.</summary>
        public DateTime? PickupScheduledAt { get; set; }

        public virtual Contract Contract { get; set; } = null!;
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual ICollection<CorrespondencePhoto> Photos { get; set; } = new List<CorrespondencePhoto>();
    }
}
