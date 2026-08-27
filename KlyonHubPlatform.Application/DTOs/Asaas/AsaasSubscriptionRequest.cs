using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasSubscriptionRequest
    {
        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty;

        [JsonPropertyName("billingType")]
        public string BillingType { get; set; } = "CREDIT_CARD";

        /// <summary>MONTHLY ou YEARLY.</summary>
        [JsonPropertyName("cycle")]
        public string Cycle { get; set; } = "MONTHLY";

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("nextDueDate")]
        public string NextDueDate { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }

        [JsonPropertyName("creditCard")]
        public AsaasCreditCard? CreditCard { get; set; }

        [JsonPropertyName("creditCardHolderInfo")]
        public AsaasCreditCardHolderInfo? CreditCardHolderInfo { get; set; }

        [JsonPropertyName("creditCardToken")]
        public string? CreditCardToken { get; set; }

        [JsonPropertyName("remoteIp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteIp { get; set; }

        [JsonPropertyName("postalService")]
        public bool PostalService { get; set; } = false;
    }

    public class AsaasSubscriptionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("dateCreated")]
        public string? DateCreated { get; set; }

        [JsonPropertyName("customer")]
        public string? Customer { get; set; }

        [JsonPropertyName("billingType")]
        public string? BillingType { get; set; }

        [JsonPropertyName("cycle")]
        public string? Cycle { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("nextDueDate")]
        public string? NextDueDate { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }

    public class AsaasSubscriptionPaymentsListResponse
    {
        [JsonPropertyName("data")]
        public List<AsaasPaymentResponse>? Data { get; set; }

        [JsonPropertyName("totalCount")]
        public int? TotalCount { get; set; }
    }

    /// <summary>Body de PUT /subscriptions/{id} (troca billingType/valor e opcionalmente cobranças pendentes).</summary>
    public class AsaasSubscriptionUpdateRequest
    {
        [JsonPropertyName("billingType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BillingType { get; set; }

        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Value { get; set; }

        [JsonPropertyName("cycle")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Cycle { get; set; }

        [JsonPropertyName("nextDueDate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NextDueDate { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonPropertyName("updatePendingPayments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UpdatePendingPayments { get; set; }

        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Status { get; set; }

        [JsonPropertyName("creditCard")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AsaasCreditCard? CreditCard { get; set; }

        [JsonPropertyName("creditCardHolderInfo")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AsaasCreditCardHolderInfo? CreditCardHolderInfo { get; set; }

        [JsonPropertyName("creditCardToken")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CreditCardToken { get; set; }

        [JsonPropertyName("remoteIp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteIp { get; set; }
    }
}
