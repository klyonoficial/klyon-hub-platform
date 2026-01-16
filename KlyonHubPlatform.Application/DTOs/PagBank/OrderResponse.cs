using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.PagBank
{
    public class OrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }

        [JsonPropertyName("charges")]
        public List<PagBankChargeResponse>? Charges { get; set; }

        [JsonPropertyName("qr_codes")]
        public List<PagBankQrCodeResponse>? QrCodes { get; set; }

        [JsonPropertyName("links")]
        public List<PagBankLink>? Links { get; set; }
    }

    public class PagBankChargeResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }

        [JsonPropertyName("amount")]
        public PagBankAmount Amount { get; set; } = new PagBankAmount();

        [JsonPropertyName("payment_method")]
        public PagBankPaymentMethodResponse? PaymentMethod { get; set; }

        [JsonPropertyName("links")]
        public List<PagBankLink>? Links { get; set; }
    }

    public class PagBankPaymentMethodResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("installments")]
        public int? Installments { get; set; }

        [JsonPropertyName("card")]
        public PagBankCardResponse? Card { get; set; }

        [JsonPropertyName("boleto")]
        public PagBankBoletoResponse? Boleto { get; set; }
    }

    public class PagBankCardResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("first_six_digits")]
        public string? FirstSixDigits { get; set; }

        [JsonPropertyName("last_four_digits")]
        public string? LastFourDigits { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("holder")]
        public PagBankCardHolder? Holder { get; set; }
    }

    public class PagBankBoletoResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        [JsonPropertyName("formatted_barcode")]
        public string? FormattedBarcode { get; set; }

        [JsonPropertyName("due_date")]
        public DateTime? DueDate { get; set; }

        [JsonPropertyName("instruction_lines")]
        public PagBankBoletoInstructions? InstructionLines { get; set; }

        [JsonPropertyName("holder")]
        public PagBankBoletoHolder? Holder { get; set; }

        [JsonPropertyName("links")]
        public List<PagBankLink>? Links { get; set; }
    }

    public class PagBankQrCodeResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PagBankAmount Amount { get; set; } = new PagBankAmount();

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("expiration_date")]
        public DateTime? ExpirationDate { get; set; }

        [JsonPropertyName("links")]
        public List<PagBankLink>? Links { get; set; }
    }

    public class PagBankLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;

        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("media")]
        public string? Media { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class PagBankWebhookNotification
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("resource")]
        public PagBankWebhookResource? Resource { get; set; }
    }

    public class PagBankWebhookResource
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PagBankAmount? Amount { get; set; }

        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }
    }
}
