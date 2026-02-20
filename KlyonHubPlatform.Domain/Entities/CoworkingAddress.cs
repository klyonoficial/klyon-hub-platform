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
        public AddressStatus AddressStatus { get; set; } = AddressStatus.PendingApproval;
        public int MaxContracts { get; set; } = 1;
        public int CurrentContracts { get; set; } = 0;
        public int RequestedFiscalCapacity { get; set; } = 1;
        public int ApprovedFiscalCapacity { get; set; } = 1;
        public CapacityApprovalStatus CapacityApprovalStatus { get; set; } = CapacityApprovalStatus.None;
        public int RequestedMaxContracts { get; set; } = 1;
        public CapacityApprovalStatus MaxContractsApprovalStatus { get; set; } = CapacityApprovalStatus.None;
        public CapacityApprovalStatus CardPricesApprovalStatus { get; set; } = CapacityApprovalStatus.None;
        public bool OffersSharedRoom { get; set; } = false;
        public decimal? SharedRoomPricePerHour { get; set; }
        public int? SharedRoomMaxSeats { get; set; }
        public bool OffersPrivateRoom { get; set; } = false;
        public decimal? PrivateRoomPricePerHour { get; set; }
        public int? PrivateRoomMaxSeats { get; set; }
        public decimal? CardPrice1x { get; set; }
        public decimal? CardPriceMonthly { get; set; }
        public decimal? CardPrice12x { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Location Location { get; set; } = null!;
        public virtual ICollection<CoworkingRoom> Rooms { get; set; } = new List<CoworkingRoom>();
        public virtual ICollection<CapacityChangeRequest> CapacityChangeRequests { get; set; } = new List<CapacityChangeRequest>();
        public virtual ICollection<MaxContractsChangeRequest> MaxContractsChangeRequests { get; set; } = new List<MaxContractsChangeRequest>();
        public virtual ICollection<CardPriceChangeRequest> CardPriceChangeRequests { get; set; } = new List<CardPriceChangeRequest>();
    }
}