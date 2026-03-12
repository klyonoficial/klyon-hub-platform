using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasPixAutomaticAuthorizationResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        [JsonPropertyName("encodedImage")]
        public string? EncodedImage { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("immediateQrCode")]
        public AsaasPixAutomaticImmediateQrCodeResponse? ImmediateQrCode { get; set; }
    }

    public class AsaasPixAutomaticImmediateQrCodeResponse
    {
        [JsonPropertyName("conciliationIdentifier")]
        public string? ConciliationIdentifier { get; set; }

        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; }
    }
}
