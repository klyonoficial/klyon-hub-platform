namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    public class FiscalOnboardingStartRequest
    {
        public int CoworkingAddressId { get; set; }
        public bool DeclaresHasCnpj { get; set; }
        /// <summary>CPF ou CNPJ (com ou sem máscara).</summary>
        public string TaxId { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
    }
}
