using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.FocusNFe
{
    /// <summary>
    /// Resposta da emissão de NFSe via Focus NFe
    /// </summary>
    public class FocusNFSeResponse
    {
        /// <summary>CNPJ do prestador</summary>
        [JsonPropertyName("cnpj_prestador")]
        public string? CnpjPrestador { get; set; }

        /// <summary>Referência da nota (nosso ID)</summary>
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        /// <summary>Status da NFSe</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Número da NFSe</summary>
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        /// <summary>Código de verificação</summary>
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }

        /// <summary>Data de emissão</summary>
        [JsonPropertyName("data_emissao")]
        public string? DataEmissao { get; set; }

        /// <summary>URL para visualização da nota</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>Caminho do XML</summary>
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }

        /// <summary>Mensagem de status ou erro</summary>
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }

        /// <summary>Lista de erros (se houver)</summary>
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }

        /// <summary>Status do SEFAZ</summary>
        [JsonPropertyName("status_sefaz")]
        public string? StatusSefaz { get; set; }

        /// <summary>Mensagem do SEFAZ</summary>
        [JsonPropertyName("mensagem_sefaz")]
        public string? MensagemSefaz { get; set; }
    }

    /// <summary>
    /// Erro retornado pelo Focus NFe
    /// </summary>
    public class FocusErro
    {
        /// <summary>Código do erro</summary>
        [JsonPropertyName("codigo")]
        public string? Codigo { get; set; }

        /// <summary>Mensagem do erro</summary>
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }

        /// <summary>Correção sugerida</summary>
        [JsonPropertyName("correcao")]
        public string? Correcao { get; set; }
    }

    /// <summary>
    /// Resposta de consulta de NFSe
    /// </summary>
    public class FocusNFSeConsultaResponse
    {
        /// <summary>CNPJ do prestador</summary>
        [JsonPropertyName("cnpj_prestador")]
        public string? CnpjPrestador { get; set; }

        /// <summary>Referência da nota (nosso ID)</summary>
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        /// <summary>Status da NFSe: processando_autorizacao, autorizado, cancelado, erro_autorizacao</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Número da NFSe</summary>
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        /// <summary>Código de verificação</summary>
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }

        /// <summary>Data de emissão</summary>
        [JsonPropertyName("data_emissao")]
        public string? DataEmissao { get; set; }

        /// <summary>URL para visualização da nota</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>Caminho do XML</summary>
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }

        /// <summary>Mensagem</summary>
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }

        /// <summary>Lista de erros</summary>
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }

        /// <summary>Valor total dos serviços</summary>
        [JsonPropertyName("valor_total")]
        public decimal? ValorTotal { get; set; }

        /// <summary>Data do cancelamento (se cancelada)</summary>
        [JsonPropertyName("data_cancelamento")]
        public string? DataCancelamento { get; set; }

        /// <summary>Justificativa do cancelamento</summary>
        [JsonPropertyName("justificativa_cancelamento")]
        public string? JustificativaCancelamento { get; set; }
    }

    /// <summary>
    /// Webhook recebido do Focus NFe
    /// </summary>
    public class FocusWebhookPayload
    {
        /// <summary>CNPJ do emitente</summary>
        [JsonPropertyName("cnpj_emitente")]
        public string? CnpjEmitente { get; set; }

        /// <summary>Referência da nota (nosso ID)</summary>
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        /// <summary>Status da nota</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Número da nota</summary>
        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        /// <summary>Código de verificação</summary>
        [JsonPropertyName("codigo_verificacao")]
        public string? CodigoVerificacao { get; set; }

        /// <summary>URL para visualização</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>URL do XML</summary>
        [JsonPropertyName("caminho_xml_nota_fiscal")]
        public string? CaminhoXmlNotaFiscal { get; set; }

        /// <summary>Tipo do documento (nfse, nfe, nfce)</summary>
        [JsonPropertyName("tipo")]
        public string? Tipo { get; set; }

        /// <summary>Evento (autorizado, cancelado, etc.)</summary>
        [JsonPropertyName("evento")]
        public string? Evento { get; set; }

        /// <summary>Lista de erros (se houver)</summary>
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
    }

    /// <summary>
    /// Resposta de cancelamento de NFSe
    /// </summary>
    public class FocusNFSeCancelResponse
    {
        /// <summary>Status do cancelamento</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Mensagem</summary>
        [JsonPropertyName("mensagem")]
        public string? Mensagem { get; set; }

        /// <summary>Lista de erros</summary>
        [JsonPropertyName("erros")]
        public List<FocusErro>? Erros { get; set; }
    }

    /// <summary>
    /// Resposta de download de XML
    /// </summary>
    public class FocusXmlResponse
    {
        /// <summary>Conteúdo do XML</summary>
        public string? XmlContent { get; set; }

        /// <summary>Status da requisição</summary>
        public bool Success { get; set; }

        /// <summary>Mensagem de erro</summary>
        public string? ErrorMessage { get; set; }
    }
}
