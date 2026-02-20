using KlyonHubPlatform.Domain.Core.Constants;
namespace KlyonHubPlatform.Domain.Core.Enums
{
    public static class D4SignEnumExtensions
    {
        public static string ToApiValue(this D4SignSignerAct value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignSignerForeign value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignCertificadoIcpBr value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignAssinaturaPresencial value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignDocAuth value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignDocAuthAndSelfie value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignUploadAllow value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignSkipEmail value) => ((int)value).ToString();
        public static string ToApiValue(this D4SignWorkflow value) => ((int)value).ToString();
        public static string GetBaseUrl(this D4SignEnvironment environment)
        {
            return environment == D4SignEnvironment.Sandbox
                ? D4SignApiEndpoints.SandboxBaseUrl
                : D4SignApiEndpoints.ProductionBaseUrl;
        }
    }
}