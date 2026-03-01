namespace KlyonHubPlatform.Api.Requests
{
    public class ProcessPaymentWithCardRequest
    {
        public int PaymentId { get; set; }
        /// <summary>Quando informado e o cartão possui AsaasToken, a cobrança é feita com o cartão salvo (sem abrir o modal).</summary>
        public int? CreditCardId { get; set; }
        public PaymentCardInput? Card { get; set; }
        public int? Installments { get; set; }
    }
}