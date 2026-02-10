using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    /// <summary>
    /// Request para criar cobrança no Asaas.
    /// Ref: https://docs.asaas.com/reference/criar-nova-cobranca
    /// billingType: PIX, BOLETO, CREDIT_CARD, DEBIT_CARD, UNDEFINED
    /// </summary>
    public class AsaasPaymentRequest
    {
        /// <summary>ID do cliente já cadastrado no Asaas. Não enviar quando usar customerData (documentação: use apenas um dos dois).</summary>
        [JsonPropertyName("customer")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Customer { get; set; }

        [JsonPropertyName("customerData")]
        public AsaasCustomerData? CustomerData { get; set; }

        [JsonPropertyName("billingType")]
        public string BillingType { get; set; } = "PIX"; // PIX, BOLETO, CREDIT_CARD

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("dueDate")]
        public string DueDate { get; set; } = string.Empty; // yyyy-MM-dd

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }

        /// <summary>Para cartão de crédito.</summary>
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

        /// <summary>Código IBGE do município (número). Asaas não aceita nome da cidade.</summary>
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
}
