using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasCustomerRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        [JsonPropertyName("cpfCnpj")]
        public string CpfCnpj { get; set; } = string.Empty;
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }
        [JsonPropertyName("mobilePhone")]
        public string? MobilePhone { get; set; }
        [JsonPropertyName("address")]
        public string? Address { get; set; }
        [JsonPropertyName("addressNumber")]
        public string? AddressNumber { get; set; }
        [JsonPropertyName("complement")]
        public string? Complement { get; set; }
        [JsonPropertyName("province")]
        public string? Province { get; set; }
        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }
        [JsonPropertyName("city")]
        public string? City { get; set; }
        [JsonPropertyName("state")]
        public string? State { get; set; }
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
        /// <summary>
        /// Quando true, desabilita todas as notificações automáticas do Asaas para este cliente (e-mail, SMS).
        /// Mantemos true porque a comunicação com o cliente é gerenciada pela plataforma Klyon.
        /// </summary>
        [JsonPropertyName("notificationDisabled")]
        public bool NotificationDisabled { get; set; } = true;
    }
    public class AsaasCustomerResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("cpfCnpj")]
        public string? CpfCnpj { get; set; }
        [JsonPropertyName("dateCreated")]
        public string? DateCreated { get; set; }
    }
    public class AsaasCustomerListResponse
    {
        [JsonPropertyName("data")]
        public List<AsaasCustomerResponse>? Data { get; set; }
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }
}