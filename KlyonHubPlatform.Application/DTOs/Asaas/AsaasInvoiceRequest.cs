using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasInvoiceRequest
    {
        [JsonPropertyName("payment")]
        public string Payment { get; set; } = string.Empty;
        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty;
        [JsonPropertyName("serviceDescription")]
        public string? ServiceDescription { get; set; }
        [JsonPropertyName("observations")]
        public string? Observations { get; set; }
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }
    public class AsaasInvoiceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("dateCreated")]
        public string? DateCreated { get; set; }
        [JsonPropertyName("payment")]
        public string? Payment { get; set; }
        [JsonPropertyName("customer")]
        public string? Customer { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("serviceDescription")]
        public string? ServiceDescription { get; set; }
        [JsonPropertyName("observations")]
        public string? Observations { get; set; }
        [JsonPropertyName("invoiceNumber")]
        public string? InvoiceNumber { get; set; }
        [JsonPropertyName("invoiceUrl")]
        public string? InvoiceUrl { get; set; }
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }
}