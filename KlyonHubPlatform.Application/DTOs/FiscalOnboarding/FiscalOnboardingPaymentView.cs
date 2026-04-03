namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    public class FiscalOnboardingPaymentView
    {
        public int PaymentId { get; set; }
        public string? PixCopyPaste { get; set; }
        public DateTime? PixExpiresAt { get; set; }
        public decimal Amount { get; set; }
        public string? TaxIdMasked { get; set; }
    }
}
