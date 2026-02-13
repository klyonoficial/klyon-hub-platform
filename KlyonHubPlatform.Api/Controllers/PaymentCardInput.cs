namespace KlyonHubPlatform.Api.Controllers
{
    public class PaymentCardInput
    {
        public string? Number { get; set; }
        public string? ExpirationMonth { get; set; }
        public string? ExpirationYear { get; set; }
        public string? SecurityCode { get; set; }
        public string? HolderName { get; set; }
    }
}
