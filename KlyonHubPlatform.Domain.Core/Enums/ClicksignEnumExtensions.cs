using KlyonHubPlatform.Domain.Core.Constants;
namespace KlyonHubPlatform.Domain.Core.Enums
{
    public static class ClicksignEnumExtensions
    {
        public static string GetBaseUrl(this ClicksignEnvironment environment)
        {
            return environment == ClicksignEnvironment.Sandbox
                ? ClicksignApiEndpoints.SandboxBaseUrl
                : ClicksignApiEndpoints.ProductionBaseUrl;
        }
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