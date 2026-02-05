namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Flags numéricas usadas no body das requisições da API D4Sign (valores "0" ou "1").
    /// </summary>
    public static class D4SignApiFlags
    {
        /// <summary>Valor "0" (não / desabilitado).</summary>
        public const string No = "0";

        /// <summary>Valor "1" (sim / habilitado).</summary>
        public const string Yes = "1";
    }

    /// <summary>
    /// Ação do signatário no documento (createlist - act).
    /// </summary>
    public enum D4SignSignerAct
    {
        /// <summary>Não assina.</summary>
        None = 0,

        /// <summary>Deve assinar o documento.</summary>
        Sign = 1
    }

    /// <summary>
    /// Signatário é estrangeiro (createlist - foreign).
    /// </summary>
    public enum D4SignSignerForeign
    {
        /// <summary>Nacional.</summary>
        National = 0,

        /// <summary>Estrangeiro.</summary>
        Foreign = 1
    }

    /// <summary>
    /// Usar certificado ICP-Brasil (createlist - certificadoicpbr).
    /// </summary>
    public enum D4SignCertificadoIcpBr
    {
        /// <summary>Não utilizar.</summary>
        No = 0,

        /// <summary>Utilizar certificado ICP-Brasil.</summary>
        Yes = 1
    }

    /// <summary>
    /// Assinatura presencial (createlist - assinatura_presencial).
    /// </summary>
    public enum D4SignAssinaturaPresencial
    {
        No = 0,
        Yes = 1
    }

    /// <summary>
    /// Autenticação por documento (createlist - docauth).
    /// </summary>
    public enum D4SignDocAuth
    {
        No = 0,
        Yes = 1
    }

    /// <summary>
    /// Autenticação por documento e selfie (createlist - docauthandselfie).
    /// </summary>
    public enum D4SignDocAuthAndSelfie
    {
        No = 0,
        Yes = 1
    }

    /// <summary>
    /// Permitir upload pelo signatário (createlist - upload_allow).
    /// </summary>
    public enum D4SignUploadAllow
    {
        No = 0,
        Yes = 1
    }

    /// <summary>
    /// Enviar e-mail ao signatário ao enviar documento (sendtosigner - skip_email).
    /// </summary>
    public enum D4SignSkipEmail
    {
        /// <summary>Enviar e-mail ao signatário.</summary>
        SendEmail = 0,

        /// <summary>Não enviar e-mail.</summary>
        SkipEmail = 1
    }

    /// <summary>
    /// Fluxo de assinatura (sendtosigner - workflow): sequencial ou paralelo.
    /// </summary>
    public enum D4SignWorkflow
    {
        /// <summary>Signatários assinam em sequência.</summary>
        Sequential = 0,

        /// <summary>Signatários podem assinar em paralelo.</summary>
        Parallel = 1
    }
}
