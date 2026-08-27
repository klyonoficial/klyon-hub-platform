using System.Text.Json.Serialization;
namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasPaymentRequest
    {
        [JsonPropertyName("customer")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Customer { get; set; }
        [JsonPropertyName("customerData")]
        public AsaasCustomerData? CustomerData { get; set; }
        [JsonPropertyName("billingType")]
        public string BillingType { get; set; } = "PIX";
        [JsonPropertyName("value")]
        public decimal Value { get; set; }
        [JsonPropertyName("dueDate")]
        public string DueDate { get; set; } = string.Empty;
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
        [JsonPropertyName("installmentCount")]
        public int? InstallmentCount { get; set; }
        [JsonPropertyName("installmentValue")]
        public decimal? InstallmentValue { get; set; }
        /// <summary>IP de origem do cliente (obrigatório para cobrança com cartão). Documentação Asaas: "IP de onde o cliente está realizando a compra."</summary>
        [JsonPropertyName("remoteIp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteIp { get; set; }
        /// <summary>ID da autorização PIX Automático (para cobranças recorrentes via débito automático).</summary>
        [JsonPropertyName("pixAutomaticAuthorizationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PixAutomaticAuthorizationId { get; set; }
        /// <summary>
        /// Quando false, desabilita o envio automático de e-mail/postal pelo Asaas ao cliente sobre esta cobrança.
        /// Mantemos false porque a comunicação com o cliente é gerenciada pela própria plataforma Klyon.
        /// </summary>
        [JsonPropertyName("postalService")]
        public bool PostalService { get; set; } = false;
    }
    public class AsaasCustomerData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("cpfCnpj")]
        public string CpfCnpj { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
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
        public int? City { get; set; }
        [JsonPropertyName("state")]
        public string? State { get; set; }
    }
    public class AsaasCreditCard
    {
        [JsonPropertyName("holderName")]
        public string HolderName { get; set; } = string.Empty;
        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;
        [JsonPropertyName("expiryMonth")]
        public string ExpiryMonth { get; set; } = string.Empty;
        [JsonPropertyName("expiryYear")]
        public string ExpiryYear { get; set; } = string.Empty;
        [JsonPropertyName("ccv")]
        public string Ccv { get; set; } = string.Empty;
    }
    public class AsaasCreditCardHolderInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        [JsonPropertyName("cpfCnpj")]
        public string CpfCnpj { get; set; } = string.Empty;
        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }
        [JsonPropertyName("addressNumber")]
        public string? AddressNumber { get; set; }
        [JsonPropertyName("addressComplement")]
        public string? AddressComplement { get; set; }
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }
        [JsonPropertyName("mobilePhone")]
        public string? MobilePhone { get; set; }
    }

    /// <summary>Body de POST /payments/{id}/payWithCreditCard — captura imediata de cobrança já existente.</summary>
    public class AsaasPayWithCreditCardRequest
    {
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