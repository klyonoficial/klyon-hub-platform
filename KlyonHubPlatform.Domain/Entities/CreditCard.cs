namespace KlyonHubPlatform.Domain.Entities
{
    public class CreditCard
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string LastFourDigits { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        /// <summary>Apelido para o cliente identificar o cartão (ex.: "Cartão pessoal", "Nubank").</summary>
        public string? Nickname { get; set; }
        /// <summary>Token Asaas para cobrança com cartão salvo (tokenização).</summary>
        public string? AsaasToken { get; set; }
        /// <summary>CEP do titular (8 dígitos) usado em creditCardHolderInfo na cobrança com token.</summary>
        public string? PostalCode { get; set; }
        /// <summary>Número do endereço do titular (ou S/N) usado em creditCardHolderInfo.</summary>
        public string? AddressNumber { get; set; }
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Client Client { get; set; } = null!;
    }
}