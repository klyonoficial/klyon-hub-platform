using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.FocusNFe
{
    public class FocusNFSeResponse
    {
        [JsonPropertyName("cnpj_prestador")]
        public string? CnpjPrestador { get; set; }
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }
        [JsonPropertyName("data_emissao")]
        public string? DataEmissao { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
        [JsonPropertyName("status_sefaz")]
        public string? StatusSefaz { get; set; }
        [JsonPropertyName("mensagem_sefaz")]
        public string? MensagemSefaz { get; set; }
    }
    public class FocusErro
    {
        [JsonPropertyName("codigo")]
        public string? Codigo { get; set; }
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }
        [JsonPropertyName("correcao")]
        public string? Correcao { get; set; }
    }
    public class FocusNFSeConsultaResponse
    {
        [JsonPropertyName("cnpj_prestador")]
        public string? CnpjPrestador { get; set; }
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }
        [JsonPropertyName("data_emissao")]
        public string? DataEmissao { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
        [JsonPropertyName("valor_total")]
        public decimal? ValorTotal { get; set; }
        [JsonPropertyName("data_cancelamento")]
        public string? DataCancelamento { get; set; }
        [JsonPropertyName("justificativa_cancelamento")]
        public string? JustificativaCancelamento { get; set; }
    }
    public class FocusWebhookPayload
    {
        [JsonPropertyName("cnpj_emitente")]
        public string? CnpjEmitente { get; set; }
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }
        [JsonPropertyName("tipo")]
        public string? Tipo { get; set; }
        [JsonPropertyName("evento")]
        public string? Evento { get; set; }
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
    }
    public class FocusNFSeCancelResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
    }
    public class FocusXmlResponse
    {
        public string? XmlContent { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}