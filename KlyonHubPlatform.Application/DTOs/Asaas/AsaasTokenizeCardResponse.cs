using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasTokenizeCardResponse
    {
        [JsonPropertyName("creditCardToken")]
        public string? CreditCardToken { get; set; }
        [JsonPropertyName("creditCardNumber")]
        public string? CreditCardNumber { get; set; }
        [JsonPropertyName("creditCardBrand")]
        public string? CreditCardBrand { get; set; }
    }
}
