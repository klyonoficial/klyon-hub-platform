using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para enums Clicksign obterem o valor string esperado pela API (JSON API).
    /// </summary>
    public static class ClicksignEnumExtensions
    {
        /// <summary>Retorna a URL base da API para o ambiente.</summary>
        public static string GetBaseUrl(this ClicksignEnvironment environment)
        {
            return environment == ClicksignEnvironment.Sandbox
                ? ClicksignApiEndpoints.SandboxBaseUrl
                : ClicksignApiEndpoints.ProductionBaseUrl;
        }

        /// <summary>Valor do status do envelope na API Clicksign.</summary>
        public static string ToApiValue(this ClicksignEnvelopeStatus status)
        {
            return status switch
            {
                ClicksignEnvelopeStatus.Draft => "draft",
                ClicksignEnvelopeStatus.Running => "running",
                ClicksignEnvelopeStatus.Closed => "closed",
                ClicksignEnvelopeStatus.Canceled => "canceled",
                _ => "draft"
            };
        }
    }
}
