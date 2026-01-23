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
        public string Brand { get; set; } = string.Empty; // Visa, Mastercard, etc.
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
    }
}

