namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Configurações da SEFAZ ES para emissão de notas fiscais
    /// </summary>
    public class SefazConfiguration
    {
        public int Id { get; set; }
        /// <summary>Estado (UF) para o qual esta configuração é válida (ex: ES, RJ, SP)</summary>
        public string State { get; set; } = "ES"; // Código UF de 2 letras
        /// <summary>Ambiente: Homologação ou Produção</summary>
        public string Environment { get; set; } = "Homologacao"; // "Homologacao" ou "Producao"
        
        // Credenciais da Plataforma (Klyon)
        /// <summary>CNPJ da plataforma</summary>
        public string PlatformCNPJ { get; set; } = string.Empty;
        /// <summary>Inscrição Municipal da plataforma</summary>
        public string PlatformMunicipalRegistration { get; set; } = string.Empty;
        /// <summary>Caminho do certificado digital (.pfx)</summary>
        public string? CertificatePath { get; set; }
        /// <summary>Senha do certificado digital</summary>
        public string? CertificatePassword { get; set; }
        /// <summary>Conteúdo do certificado (base64) - alternativa ao caminho</summary>
        public string? CertificateContent { get; set; }
        
        // Configurações de Serviço
        /// <summary>Código do serviço de intermediação (LC 116)</summary>
        public string IntermediationServiceCode { get; set; } = string.Empty;
        /// <summary>Descrição do serviço de intermediação</summary>
        public string IntermediationServiceDescription { get; set; } = "Intermediação de serviços";
        /// <summary>CNAE da plataforma</summary>
        public string PlatformCNAE { get; set; } = string.Empty;
        /// <summary>Regime tributário (Simples Nacional, Lucro Presumido, etc.)</summary>
        public string TaxRegime { get; set; } = string.Empty;
        
        // Configurações de ISS
        /// <summary>Alíquota de ISS (%)</summary>
        public decimal ISSAliquot { get; set; }
        /// <summary>Município de incidência do ISS</summary>
        public string ISSMunicipality { get; set; } = string.Empty;
        
        // URLs da SEFAZ ES
        /// <summary>URL base da API SEFAZ ES (Homologação)</summary>
        public string? HomologationUrl { get; set; }
        /// <summary>URL base da API SEFAZ ES (Produção)</summary>
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
    }
}

