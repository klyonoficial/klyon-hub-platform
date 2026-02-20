using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class AddressRejectionUpdateDto
    {
        public CapacityApprovalStatus? CapacityApprovalStatus { get; set; }
        public int? RequestedFiscalCapacity { get; set; }
        public CapacityApprovalStatus? MaxContractsApprovalStatus { get; set; }
        public CapacityApprovalStatus? CardPricesApprovalStatus { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}