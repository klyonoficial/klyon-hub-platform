using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Api.Dtos.Admin
{
    /// <summary>
    /// DTO para atualizar um CoworkingAddress com resultado de aprovação (capacidade, max contracts ou preços).
    /// Apenas propriedades preenchidas são aplicadas no mapeamento.
    /// </summary>
    public class AddressApprovalUpdateDto
    {
        public int? ApprovedFiscalCapacity { get; set; }
        public int? RequestedFiscalCapacity { get; set; }
        public CapacityApprovalStatus? CapacityApprovalStatus { get; set; }
        public int? MaxContracts { get; set; }
        public CapacityApprovalStatus? MaxContractsApprovalStatus { get; set; }
        public decimal? CardPrice1x { get; set; }
        public decimal? CardPriceMonthly { get; set; }
        public decimal? CardPrice12x { get; set; }
        public CapacityApprovalStatus? CardPricesApprovalStatus { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
