namespace KlyonHubPlatform.Api.Requests
{
    /// <summary>Request para processar pagamento do endereço fiscal (subscription) com cartão.</summary>
    public class ProcessPaymentWithCardRequest
    {
        public int PaymentId { get; set; }
        public PaymentCardInput? Card { get; set; }
        public int? Installments { get; set; }
    }
}
