using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.PagBank
{
    public class OrderRequest
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public PagBankCustomer? Customer { get; set; }

        [JsonPropertyName("items")]
        public List<PagBankItem> Items { get; set; } = new List<PagBankItem>();

        [JsonPropertyName("shipping")]
        public PagBankShipping? Shipping { get; set; }

        [JsonPropertyName("qr_codes")]
        public List<PagBankQrCode>? QrCodes { get; set; }

        [JsonPropertyName("charges")]
        public List<PagBankCharge>? Charges { get; set; }
    }

    public class PagBankCustomer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("tax_id")]
        public string TaxId { get; set; } = string.Empty; // CPF ou CNPJ

        [JsonPropertyName("phone")]
        public PagBankPhone? Phone { get; set; }
    }

    public class PagBankPhone
    {
        [JsonPropertyName("country")]
        public string Country { get; set; } = "55";

        [JsonPropertyName("area")]
        public string Area { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;
    }

    public class PagBankItem
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("unit_amount")]
        public int UnitAmount { get; set; } // Valor em centavos
    }

    public class PagBankShipping
    {
        [JsonPropertyName("address")]
        public PagBankAddress? Address { get; set; }
    }

    public class PagBankAddress
    {
        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("complement")]
        public string? Complement { get; set; }

        [JsonPropertyName("locality")]
        public string Locality { get; set; } = string.Empty; // Bairro

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("region_code")]
        public string RegionCode { get; set; } = string.Empty; // UF

        [JsonPropertyName("country")]
        public string Country { get; set; } = "BRA";

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; } = string.Empty;
    }

    public class PagBankQrCode
    {
        [JsonPropertyName("amount")]
        public PagBankAmount Amount { get; set; } = new PagBankAmount();
    }

    public class PagBankAmount
    {
        [JsonPropertyName("value")]
        public int Value { get; set; } // Valor em centavos

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "BRL";
    }

    public class PagBankCharge
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PagBankAmount Amount { get; set; } = new PagBankAmount();

        [JsonPropertyName("payment_method")]
        public PagBankPaymentMethod PaymentMethod { get; set; } = new PagBankPaymentMethod();
    }

    public class PagBankPaymentMethod
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // CREDIT_CARD, DEBIT_CARD, BOLETO, PIX

        [JsonPropertyName("installments")]
        public int? Installments { get; set; }

        [JsonPropertyName("capture")]
        public bool Capture { get; set; } = true;

        [JsonPropertyName("card")]
        public PagBankCard? Card { get; set; }

        [JsonPropertyName("boleto")]
        public PagBankBoleto? Boleto { get; set; }
    }

    public class PagBankCard
    {
        [JsonPropertyName("encrypted")]
        public string? Encrypted { get; set; }

        [JsonPropertyName("security_code")]
        public string SecurityCode { get; set; } = string.Empty;

        [JsonPropertyName("holder")]
        public PagBankCardHolder Holder { get; set; } = new PagBankCardHolder();

        [JsonPropertyName("store")]
        public bool Store { get; set; } = false;
    }

    public class PagBankCardHolder
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class PagBankBoleto
    {
        [JsonPropertyName("due_date")]
        public string DueDate { get; set; } = string.Empty; // YYYY-MM-DD

        [JsonPropertyName("instruction_lines")]
        public PagBankBoletoInstructions? InstructionLines { get; set; }

        [JsonPropertyName("holder")]
        public PagBankBoletoHolder? Holder { get; set; }
    }

    public class PagBankBoletoInstructions
    {
        [JsonPropertyName("line_1")]
        public string? Line1 { get; set; }

        [JsonPropertyName("line_2")]
        public string? Line2 { get; set; }
    }

    public class PagBankBoletoHolder
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("tax_id")]
        public string TaxId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("address")]
        public PagBankBoletoHolderAddress? Address { get; set; }
    }

    // Endereço específico para holder do boleto - usa "region" ao invés de "region_code"
    public class PagBankBoletoHolderAddress
    {
        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("complement")]
        public string? Complement { get; set; }

        [JsonPropertyName("locality")]
        public string Locality { get; set; } = string.Empty; // Bairro

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty; // UF - usa "region" ao invés de "region_code"

        [JsonPropertyName("country")]
        public string Country { get; set; } = "BRA";

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; } = string.Empty;
    }
}
