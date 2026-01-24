using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.FocusNFe
{
    /// <summary>
    /// DTO para emissão de NFSe via Focus NFe
    /// Documentação: https://focusnfe.com.br/doc/
    /// </summary>
    public class FocusNFSeRequest
    {
        /// <summary>Referência única da nota no sistema (nosso ID)</summary>
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;

        /// <summary>Data de emissão (formato: yyyy-MM-dd)</summary>
        [JsonPropertyName("data_emissao")]
        public string DataEmissao { get; set; } = string.Empty;

        /// <summary>Natureza da operação (1 = Tributação no município, 2 = Tributação fora do município, etc.)</summary>
        [JsonPropertyName("natureza_operacao")]
        public string NaturezaOperacao { get; set; } = "1";

        /// <summary>Regime especial de tributação (1 a 6)</summary>
        [JsonPropertyName("regime_especial_tributacao")]
        public string? RegimeEspecialTributacao { get; set; }

        /// <summary>Optante pelo Simples Nacional (true/false)</summary>
        [JsonPropertyName("optante_simples_nacional")]
        public bool OptanteSimplesNacional { get; set; } = true;

        /// <summary>Incentivador cultural (true/false)</summary>
        [JsonPropertyName("incentivador_cultural")]
        public bool IncentivadorCultural { get; set; } = false;

        /// <summary>Dados do prestador (emissor)</summary>
        [JsonPropertyName("prestador")]
        public FocusPrestador Prestador { get; set; } = new();

        /// <summary>Dados do tomador (cliente)</summary>
        [JsonPropertyName("tomador")]
        public FocusTomador Tomador { get; set; } = new();

        /// <summary>Dados do serviço</summary>
        [JsonPropertyName("servico")]
        public FocusServico Servico { get; set; } = new();
    }

    /// <summary>
    /// Dados do prestador (emissor da NFSe)
    /// </summary>
    public class FocusPrestador
    {
        /// <summary>CNPJ do prestador</summary>
        [JsonPropertyName("cnpj")]
        public string Cnpj { get; set; } = string.Empty;

        /// <summary>Inscrição municipal do prestador</summary>
        [JsonPropertyName("inscricao_municipal")]
        public string InscricaoMunicipal { get; set; } = string.Empty;

        /// <summary>Código do município do prestador (IBGE)</summary>
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dados do tomador (cliente)
    /// </summary>
    public class FocusTomador
    {
        /// <summary>CNPJ do tomador (se pessoa jurídica)</summary>
        [JsonPropertyName("cnpj")]
        public string? Cnpj { get; set; }

        /// <summary>CPF do tomador (se pessoa física)</summary>
        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        /// <summary>Razão social ou nome do tomador</summary>
        [JsonPropertyName("razao_social")]
        public string RazaoSocial { get; set; } = string.Empty;

        /// <summary>E-mail do tomador</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>Telefone do tomador</summary>
        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }

        /// <summary>Inscrição municipal do tomador</summary>
        [JsonPropertyName("inscricao_municipal")]
        public string? InscricaoMunicipal { get; set; }

        /// <summary>Endereço do tomador</summary>
        [JsonPropertyName("endereco")]
        public FocusEndereco? Endereco { get; set; }
    }

    /// <summary>
    /// Endereço
    /// </summary>
    public class FocusEndereco
    {
        /// <summary>Logradouro</summary>
        [JsonPropertyName("logradouro")]
        public string Logradouro { get; set; } = string.Empty;

        /// <summary>Número</summary>
        [JsonPropertyName("numero")]
        public string Numero { get; set; } = string.Empty;

        /// <summary>Complemento</summary>
        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }

        /// <summary>Bairro</summary>
        [JsonPropertyName("bairro")]
        public string Bairro { get; set; } = string.Empty;

        /// <summary>Código do município (IBGE)</summary>
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;

        /// <summary>UF</summary>
        [JsonPropertyName("uf")]
        public string Uf { get; set; } = string.Empty;

        /// <summary>CEP</summary>
        [JsonPropertyName("cep")]
        public string Cep { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dados do serviço prestado
    /// </summary>
    public class FocusServico
    {
        /// <summary>Alíquota do ISS (%)</summary>
        [JsonPropertyName("aliquota")]
        public decimal Aliquota { get; set; }

        /// <summary>Base de cálculo</summary>
        [JsonPropertyName("base_calculo")]
        public decimal BaseCalculo { get; set; }

        /// <summary>Valor líquido do serviço</summary>
        [JsonPropertyName("valor_liquido")]
        public decimal ValorLiquido { get; set; }

        /// <summary>Valor total do serviço</summary>
        [JsonPropertyName("valor_servicos")]
        public decimal ValorServicos { get; set; }

        /// <summary>Valor do ISS</summary>
        [JsonPropertyName("valor_iss")]
        public decimal ValorIss { get; set; }

        /// <summary>ISS retido (true/false)</summary>
        [JsonPropertyName("iss_retido")]
        public bool IssRetido { get; set; } = false;

        /// <summary>Código do item da lista de serviços (LC 116)</summary>
        [JsonPropertyName("item_lista_servico")]
        public string ItemListaServico { get; set; } = string.Empty;

        /// <summary>Código de tributação do município</summary>
        [JsonPropertyName("codigo_tributario_municipio")]
        public string? CodigoTributarioMunicipio { get; set; }

        /// <summary>Discriminação do serviço</summary>
        [JsonPropertyName("discriminacao")]
        public string Discriminacao { get; set; } = string.Empty;

        /// <summary>Código do município onde o serviço foi prestado (IBGE)</summary>
        [JsonPropertyName("codigo_municipio")]
        public string CodigoMunicipio { get; set; } = string.Empty;

        /// <summary>Código CNAE</summary>
        [JsonPropertyName("codigo_cnae")]
        public string? CodigoCnae { get; set; }

        /// <summary>Valor das deduções</summary>
        [JsonPropertyName("valor_deducoes")]
        public decimal? ValorDeducoes { get; set; }

        /// <summary>Valor do PIS</summary>
        [JsonPropertyName("valor_pis")]
        public decimal? ValorPis { get; set; }

        /// <summary>Valor do COFINS</summary>
        [JsonPropertyName("valor_cofins")]
        public decimal? ValorCofins { get; set; }

        /// <summary>Valor do INSS</summary>
        [JsonPropertyName("valor_inss")]
        public decimal? ValorInss { get; set; }

        /// <summary>Valor do IR</summary>
        [JsonPropertyName("valor_ir")]
        public decimal? ValorIr { get; set; }

        /// <summary>Valor do CSLL</summary>
        [JsonPropertyName("valor_csll")]
        public decimal? ValorCsll { get; set; }

        /// <summary>Outras retenções</summary>
        [JsonPropertyName("outras_retencoes")]
        public decimal? OutrasRetencoes { get; set; }

        /// <summary>Desconto incondicionado</summary>
        [JsonPropertyName("desconto_incondicionado")]
        public decimal? DescontoIncondicionado { get; set; }

        /// <summary>Desconto condicionado</summary>
        [JsonPropertyName("desconto_condicionado")]
        public decimal? DescontoCondicionado { get; set; }
    }

    /// <summary>
    /// DTO para cancelamento de NFSe
    /// </summary>
    public class FocusNFSeCancelRequest
    {
        /// <summary>Justificativa do cancelamento</summary>
        [JsonPropertyName("justificativa")]
        public string Justificativa { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para configuração de webhook do Focus NFe
    /// </summary>
    public class FocusWebhookConfig
    {
        /// <summary>URL para receber notificações de NFSe</summary>
        [JsonPropertyName("nfse")]
        public string? Nfse { get; set; }

        /// <summary>URL para receber notificações de NFCe</summary>
        [JsonPropertyName("nfce")]
        public string? Nfce { get; set; }

        /// <summary>URL para receber notificações de NFe</summary>
        [JsonPropertyName("nfe")]
        public string? Nfe { get; set; }
    }
}
