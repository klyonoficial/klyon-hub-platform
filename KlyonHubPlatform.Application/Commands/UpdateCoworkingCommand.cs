using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Commands
{
    /// <summary>
    /// Comando para atualização de Coworking (admin). Mesma forma do DTO da API para evitar acoplamento.
    /// </summary>
    public class UpdateCoworkingCommand
    {
        public string Name { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;
        public string? TradeName { get; set; }
        public string? PropertyRegistration { get; set; }
        public string? LegalRepresentative { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? BankName { get; set; }
        public string? BankAgency { get; set; }
        public string? BankAccount { get; set; }
        public string? BankAccountType { get; set; }
        public string? BankPixKey { get; set; }
        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? LinkedIn { get; set; }
        public string? Address { get; set; }
        public string? AddressNumber { get; set; }
        public string? AddressComplement { get; set; }
        public string? AddressNeighborhood { get; set; }
        public string? AddressCity { get; set; }
        public string? AddressState { get; set; }
        public string? AddressZipCode { get; set; }
        public bool IsActive { get; set; }
        public bool IsPartner { get; set; }
        public CoworkingApprovalStatus ApprovalStatus { get; set; }
        public string? RejectionReason { get; set; }
    }
}
