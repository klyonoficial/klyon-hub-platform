using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para enums D4Sign obterem o valor string esperado pela API.
    /// </summary>
    public static class D4SignEnumExtensions
    {
        /// <summary>Converte enum numérico (0/1) para o valor da API ("0"/"1").</summary>
        public static string ToApiValue(this D4SignSignerAct value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignSignerForeign value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignCertificadoIcpBr value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignAssinaturaPresencial value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignDocAuth value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignDocAuthAndSelfie value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignUploadAllow value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignSkipEmail value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignWorkflow value) => ((int)value).ToString();

        /// <summary>Retorna a URL base da API para o ambiente.</summary>
        public static string GetBaseUrl(this D4SignEnvironment environment)
        {
            return environment == D4SignEnvironment.Sandbox
                ? D4SignApiEndpoints.SandboxBaseUrl
                : D4SignApiEndpoints.ProductionBaseUrl;
        }
    }
}
