namespace KlyonHubPlatform.Application.DTOs.Payments
{
    public class CreditCardHolderBillingInput
    {
        public string? PostalCode { get; set; }
        public string? AddressNumber { get; set; }
        public bool AddressHasNoNumber { get; set; }
    }
}
