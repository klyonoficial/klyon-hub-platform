using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    /// <summary>
    /// Request para agendar/emitir nota fiscal no Asaas.
    /// Ref: https://docs.asaas.com/reference/agendar-nota-fiscal
    /// </summary>
    public class AsaasInvoiceRequest
    {
        [JsonPropertyName("payment")]
        public string Payment { get; set; } = string.Empty; // ID da cobrança

        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty; // ID do cliente

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
        public string Status { get; set; } = string.Empty; // SCHEDULED, AUTHORIZED, CANCELLED, etc.

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
