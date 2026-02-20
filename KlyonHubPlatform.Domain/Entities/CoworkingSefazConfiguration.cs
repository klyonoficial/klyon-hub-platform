namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingSefazConfiguration
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string State { get; set; } = "ES";
        public string Environment { get; set; } = "Homologacao";
        public string PartnerCNPJ { get; set; } = string.Empty;
        public string PartnerMunicipalRegistration { get; set; } = string.Empty;
        public string? CertificatePath { get; set; }
        public string? CertificatePassword { get; set; }
        public string? CertificateContent { get; set; }
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceDescription { get; set; } = "Prestação de serviços";
        public string PartnerCNAE { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public decimal ISSAliquot { get; set; }
        public string ISSMunicipality { get; set; } = string.Empty;
        public string? HomologationUrl { get; set; }
        public string? ProductionUrl { get; set; }
        public string? ApiToken { get; set; }
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
    }
}