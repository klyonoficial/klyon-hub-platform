namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Configurações SEFAZ específicas de cada parceiro/coworking
    /// Cada parceiro pode ter sua própria configuração para emitir NF de serviço
    /// </summary>
    public class CoworkingSefazConfiguration
    {
        public int Id { get; set; }
        /// <summary>ID do coworking/parceiro</summary>
        public int CoworkingId { get; set; }
        /// <summary>Estado (UF) para o qual esta configuração é válida (ex: ES, RJ, SP)</summary>
        public string State { get; set; } = "ES"; // Código UF de 2 letras
        /// <summary>Ambiente: Homologação ou Produção</summary>
        public string Environment { get; set; } = "Homologacao"; // "Homologacao" ou "Producao"
        
        // Credenciais do Parceiro
        /// <summary>CNPJ do parceiro</summary>
        public string PartnerCNPJ { get; set; } = string.Empty;
        /// <summary>Inscrição Municipal do parceiro</summary>
        public string PartnerMunicipalRegistration { get; set; } = string.Empty;
        /// <summary>Caminho do certificado digital (.pfx)</summary>
        public string? CertificatePath { get; set; }
        /// <summary>Senha do certificado digital</summary>
        public string? CertificatePassword { get; set; }
        /// <summary>Conteúdo do certificado (base64) - alternativa ao caminho</summary>
        public string? CertificateContent { get; set; }
        
        // Configurações de Serviço
        /// <summary>Código do serviço prestado (LC 116)</summary>
        public string ServiceCode { get; set; } = string.Empty;
        /// <summary>Descrição do serviço prestado</summary>
        public string ServiceDescription { get; set; } = "Prestação de serviços";
        /// <summary>CNAE do parceiro</summary>
        public string PartnerCNAE { get; set; } = string.Empty;
        /// <summary>Regime tributário (Simples Nacional, Lucro Presumido, etc.)</summary>
        public string TaxRegime { get; set; } = string.Empty;
        
        // Configurações de ISS
        /// <summary>Alíquota de ISS (%)</summary>
        public decimal ISSAliquot { get; set; }
        /// <summary>Município de incidência do ISS</summary>
        public string ISSMunicipality { get; set; } = string.Empty;
        
        // URLs da SEFAZ
        /// <summary>URL base da API SEFAZ (Homologação)</summary>
        public string? HomologationUrl { get; set; }
        /// <summary>URL base da API SEFAZ (Produção)</summary>
        public string? ProductionUrl { get; set; }
        
        // Configurações adicionais
        /// <summary>Token de autenticação (se necessário)</summary>
        public string? ApiToken { get; set; }
        /// <summary>Chave de API (se necessário)</summary>
        public string? ApiKey { get; set; }
        /// <summary>Timeout em segundos para requisições</summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        // Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Observações
        public string? Notes { get; set; }
        
        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}
