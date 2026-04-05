namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    public class FiscalOnboardingStartRequest
    {
        public int CoworkingAddressId { get; set; }
        public bool DeclaresHasCnpj { get; set; }
        /// <summary>CNPJ quando DeclaresHasCnpj=true; CPF quando DeclaresHasCnpj=false.</summary>
        public string TaxId { get; set; } = string.Empty;
        /// <summary>CPF do responsável — sempre obrigatório.</summary>
        public string? Cpf { get; set; }
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
    }
}
