using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasPixAutomaticAuthorizationListResponse
    {
        [JsonPropertyName("hasMore")]
        public bool HasMore { get; set; }

        [JsonPropertyName("data")]
        public List<AsaasPixAutomaticAuthorizationListItem>? Data { get; set; }
    }

    public class AsaasPixAutomaticAuthorizationListItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("contractId")]
        public string? ContractId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
