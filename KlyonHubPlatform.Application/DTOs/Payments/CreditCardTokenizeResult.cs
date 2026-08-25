namespace KlyonHubPlatform.Application.DTOs.Payments
{
    public sealed class CreditCardTokenizeResult
    {
        public required string Token { get; init; }
        public required string PostalCode { get; init; }
        public required string AddressNumber { get; init; }
    }
}
