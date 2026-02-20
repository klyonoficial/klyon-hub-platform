namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum FocusNFeStatus
    {
        ProcessandoAutorizacao,
        Autorizado,
        Cancelado,
        ErroAutorizacao
    }
    public enum FocusDocumentType
    {
        NFSe,
        NFe,
        NFCe
    }
    public enum FocusWebhookProcessingStatus
    {
        Received,
        Processing,
        Processed,
        Error,
        Ignored
    }
    public enum FocusNFeEnvironment
    {
        Homologacao,
        Producao
    }
    public enum NaturezaOperacao
    {
        TributacaoMunicipio = 1,
        TributacaoForaMunicipio = 2,
        Isencao = 3,
        Imune = 4,
        SuspensaJudicial = 5,
        SuspensaAdministrativa = 6
    }
    public enum RegimeEspecialTributacao
    {
        MicroempresaMunicipal = 1,
        Estimativa = 2,
        SociedadeProfissionais = 3,
        Cooperativa = 4,
        MEI = 5,
        MEEPPSimplesNacional = 6
    }
}