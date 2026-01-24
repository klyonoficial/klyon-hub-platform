using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingAddress
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int LocationId { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? Complement { get; set; }
        public string Neighborhood { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public bool IsAvailable { get; set; } = true;
        /// <summary>Status do endereço (ativo/suspenso/removido). Suspensão/remoção não afeta contratos em andamento.</summary>
        public AddressStatus AddressStatus { get; set; } = AddressStatus.Active;
        public int MaxContracts { get; set; } = 1;
        public int CurrentContracts { get; set; } = 0;
        
        // Capacidade fiscal
        /// <summary>Capacidade fiscal solicitada pelo parceiro</summary>
        public int RequestedFiscalCapacity { get; set; } = 1;
        /// <summary>Capacidade fiscal aprovada pela plataforma</summary>
        public int ApprovedFiscalCapacity { get; set; } = 1;
        /// <summary>Status da aprovação de alteração de capacidade</summary>
        public CapacityApprovalStatus CapacityApprovalStatus { get; set; } = CapacityApprovalStatus.None;
        
        // Configurações de salas para aluguel
        public bool OffersSharedRoom { get; set; } = false;
        public decimal? SharedRoomPricePerHour { get; set; }
        public int? SharedRoomMaxSeats { get; set; }
        
        public bool OffersPrivateRoom { get; set; } = false;
        public decimal? PrivateRoomPricePerHour { get; set; }
        public int? PrivateRoomMaxSeats { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Location Location { get; set; } = null!;
        public virtual ICollection<CoworkingRoom> Rooms { get; set; } = new List<CoworkingRoom>();
        public virtual ICollection<CapacityChangeRequest> CapacityChangeRequests { get; set; } = new List<CapacityChangeRequest>();
    }
}

