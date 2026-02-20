using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasPaymentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("dateCreated")]
        public string? DateCreated { get; set; }
        [JsonPropertyName("customer")]
        public string? Customer { get; set; }
        [JsonPropertyName("paymentLink")]
        public string? PaymentLink { get; set; }
        [JsonPropertyName("value")]
        public decimal Value { get; set; }
        [JsonPropertyName("netValue")]
        public decimal? NetValue { get; set; }
        [JsonPropertyName("billingType")]
        public string? BillingType { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("dueDate")]
        public string? DueDate { get; set; }
        [JsonPropertyName("confirmedDate")]
        public string? ConfirmedDate { get; set; }
        [JsonPropertyName("clientPaymentDate")]
        public string? ClientPaymentDate { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
        [JsonPropertyName("invoiceUrl")]
        public string? InvoiceUrl { get; set; }
        [JsonPropertyName("bankSlipUrl")]
        public string? BankSlipUrl { get; set; }
        [JsonPropertyName("invoiceNumber")]
        public string? InvoiceNumber { get; set; }
        [JsonPropertyName("deleted")]
        public bool? Deleted { get; set; }
    }
    public class AsaasPixQrCodeResponse
    {
        [JsonPropertyName("encodedImage")]
        public string? EncodedImage { get; set; }
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }
        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; }
    }
    public class AsaasIdentificationFieldResponse
    {
        [JsonPropertyName("identificationField")]
        public string? IdentificationField { get; set; }
        [JsonPropertyName("nossoNumero")]
        public string? NossoNumero { get; set; }
    }
}