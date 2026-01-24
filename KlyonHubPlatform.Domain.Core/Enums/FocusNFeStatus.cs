namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da NFSe no Focus NFe
    /// </summary>
    public enum FocusNFeStatus
    {
        /// <summary>NFSe em processamento de autorização</summary>
        ProcessandoAutorizacao,

        /// <summary>NFSe autorizada com sucesso</summary>
        Autorizado,

        /// <summary>NFSe cancelada</summary>
        Cancelado,

        /// <summary>Erro na autorização da NFSe</summary>
        ErroAutorizacao
    }

    /// <summary>
    /// Tipo de documento fiscal no Focus NFe
    /// </summary>
    public enum FocusDocumentType
    {
        /// <summary>Nota Fiscal de Serviço Eletrônica</summary>
        NFSe,

        /// <summary>Nota Fiscal Eletrônica</summary>
        NFe,

        /// <summary>Nota Fiscal de Consumidor Eletrônica</summary>
        NFCe
    }

    /// <summary>
    /// Status do processamento do webhook Focus NFe
    /// </summary>
    public enum FocusWebhookProcessingStatus
    {
        /// <summary>Webhook recebido mas não processado</summary>
        Received,

        /// <summary>Webhook em processamento</summary>
        Processing,

        /// <summary>Webhook processado com sucesso</summary>
        Processed,

        /// <summary>Erro no processamento do webhook</summary>
        Error,

        /// <summary>Webhook ignorado (duplicado ou inválido)</summary>
        Ignored
    }

    /// <summary>
    /// Ambiente do Focus NFe
    /// </summary>
    public enum FocusNFeEnvironment
    {
        /// <summary>Ambiente de homologação (testes)</summary>
        Homologacao,

        /// <summary>Ambiente de produção</summary>
        Producao
    }

    /// <summary>
    /// Natureza da operação para NFSe
    /// </summary>
    public enum NaturezaOperacao
    {
        /// <summary>Tributação no município</summary>
        TributacaoMunicipio = 1,

        /// <summary>Tributação fora do município</summary>
        TributacaoForaMunicipio = 2,

        /// <summary>Isenção</summary>
        Isencao = 3,

        /// <summary>Imune</summary>
        Imune = 4,

        /// <summary>Exigibilidade suspensa por decisão judicial</summary>
        SuspensaJudicial = 5,

        /// <summary>Exigibilidade suspensa por procedimento administrativo</summary>
        SuspensaAdministrativa = 6
    }

    /// <summary>
    /// Regime especial de tributação
    /// </summary>
    public enum RegimeEspecialTributacao
    {
        /// <summary>Microempresa municipal</summary>
        MicroempresaMunicipal = 1,

        /// <summary>Estimativa</summary>
        Estimativa = 2,

        /// <summary>Sociedade de profissionais</summary>
        SociedadeProfissionais = 3,

        /// <summary>Cooperativa</summary>
        Cooperativa = 4,

        /// <summary>MEI - Microempreendedor Individual</summary>
        MEI = 5,

        /// <summary>ME/EPP - Simples Nacional</summary>
        MEEPPSimplesNacional = 6
    }
}
