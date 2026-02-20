using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.FocusNFe
{
    public class FocusNFSeRequest
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;
        [JsonPropertyName("data_emissao")]
        public string DataEmissao { get; set; } = string.Empty;
        [JsonPropertyName("natureza_operacao")]
        public string NaturezaOperacao { get; set; } = "1";
        [JsonPropertyName("regime_especial_tributacao")]
        public string? RegimeEspecialTributacao { get; set; }
        [JsonPropertyName("optante_simples_nacional")]
        public bool OptanteSimplesNacional { get; set; } = true;
        [JsonPropertyName("incentivador_cultural")]
        public bool IncentivadorCultural { get; set; } = false;
        [JsonPropertyName("prestador")]
        public FocusPrestador Prestador { get; set; } = new();
        [JsonPropertyName("tomador")]
        public FocusTomador Tomador { get; set; } = new();
        [JsonPropertyName("servico")]
        public FocusServico Servico { get; set; } = new();
    }
    public class FocusPrestador
    {
        [JsonPropertyName("cnpj")]
        public string Cnpj { get; set; } = string.Empty;
        [JsonPropertyName("inscricao_municipal")]
        public string InscricaoMunicipal { get; set; } = string.Empty;
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;
    }
    public class FocusTomador
    {
        [JsonPropertyName("cnpj")]
        public string? Cnpj { get; set; }
        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }
        [JsonPropertyName("razao_social")]
        public string RazaoSocial { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }
        [JsonPropertyName("inscricao_municipal")]
        public string? InscricaoMunicipal { get; set; }
        [JsonPropertyName("endereco")]
        public FocusEndereco? Endereco { get; set; }
    }
    public class FocusEndereco
    {
        [JsonPropertyName("logradouro")]
        public string Logradouro { get; set; } = string.Empty;
        [JsonPropertyName("numero")]
        public string Numero { get; set; } = string.Empty;
        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }
        [JsonPropertyName("bairro")]
        public string Bairro { get; set; } = string.Empty;
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;
        [JsonPropertyName("uf")]
        public string Uf { get; set; } = string.Empty;
        [JsonPropertyName("cep")]
        public string Cep { get; set; } = string.Empty;
    }
    public class FocusServico
    {
        [JsonPropertyName("aliquota")]
        public decimal Aliquota { get; set; }
        [JsonPropertyName("base_calculo")]
        public decimal BaseCalculo { get; set; }
        [JsonPropertyName("valor_liquido")]
        public decimal ValorLiquido { get; set; }
        [JsonPropertyName("valor_servicos")]
        public decimal ValorServicos { get; set; }
        [JsonPropertyName("valor_iss")]
        public decimal ValorIss { get; set; }
        [JsonPropertyName("iss_retido")]
        public bool IssRetido { get; set; } = false;
        [JsonPropertyName("item_lista_servico")]
        public string ItemListaServico { get; set; } = string.Empty;
        [JsonPropertyName("codigo_tributario_municipio")]
        public string? CodigoTributarioMunicipio { get; set; }
        [JsonPropertyName("discriminacao")]
        public string Discriminacao { get; set; } = string.Empty;
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;
        [JsonPropertyName("codigo_cnae")]
        public string? CodigoCnae { get; set; }
        [JsonPropertyName("valor_deducoes")]
        public decimal? ValorDeducoes { get; set; }
        [JsonPropertyName("valor_pis")]
        public decimal? ValorPis { get; set; }
        [JsonPropertyName("valor_cofins")]
        public decimal? ValorCofins { get; set; }
        [JsonPropertyName("valor_inss")]
        public decimal? ValorInss { get; set; }
        [JsonPropertyName("valor_ir")]
        public decimal? ValorIr { get; set; }
        [JsonPropertyName("valor_csll")]
        public decimal? ValorCsll { get; set; }
        [JsonPropertyName("outras_retencoes")]
        public decimal? OutrasRetencoes { get; set; }
        [JsonPropertyName("desconto_incondicionado")]
        public decimal? DescontoIncondicionado { get; set; }
        [JsonPropertyName("desconto_condicionado")]
        public decimal? DescontoCondicionado { get; set; }
    }
    public class FocusNFSeCancelRequest
    {
        [JsonPropertyName("justificativa")]
        public string Justificativa { get; set; } = string.Empty;
    }
    public class FocusWebhookConfig
    {
        [JsonPropertyName("nfse")]
        public string? Nfse { get; set; }
        [JsonPropertyName("nfce")]
        public string? Nfce { get; set; }
        [JsonPropertyName("nfe")]
        public string? Nfe { get; set; }
    }
}