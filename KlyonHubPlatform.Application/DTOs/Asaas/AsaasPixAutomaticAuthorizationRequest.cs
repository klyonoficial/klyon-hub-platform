using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    /// <summary>Request para criar autorização PIX Automático (MONTHLY ou ANNUALLY).</summary>
    public class AsaasPixAutomaticAuthorizationRequest
    {
        [JsonPropertyName("frequency")]
        public string Frequency { get; set; } = "ANNUALLY";

        [JsonPropertyName("contractId")]
        public string ContractId { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("customerId")]
        public string CustomerId { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonPropertyName("finishDate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FinishDate { get; set; }

        [JsonPropertyName("immediateQrCode")]
        public AsaasPixAutomaticImmediateQrCodeRequest ImmediateQrCode { get; set; } = null!;
    }

    public class AsaasPixAutomaticImmediateQrCodeRequest
    {
        [JsonPropertyName("originalValue")]
        public decimal OriginalValue { get; set; }

        [JsonPropertyName("expirationSeconds")]
        public int ExpirationSeconds { get; set; } = 86400;

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }
    }
}
