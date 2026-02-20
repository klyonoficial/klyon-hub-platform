namespace KlyonHubPlatform.Api.Requests
{
    public class ProcessCreditCardRequest
    {
        public int PurchaseId { get; set; }
        public Application.DTOs.PagBank.PagBankCard? Card { get; set; }
        public Application.DTOs.PagBank.PagBankAddress? BillingAddress { get; set; }
        public int? Installments { get; set; }
    }
}
