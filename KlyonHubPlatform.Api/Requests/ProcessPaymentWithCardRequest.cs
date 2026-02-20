namespace KlyonHubPlatform.Api.Requests
{
    public class ProcessPaymentWithCardRequest
    {
        public int PaymentId { get; set; }
        public PaymentCardInput? Card { get; set; }
        public int? Installments { get; set; }
    }
}