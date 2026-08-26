namespace KlyonHubPlatform.Application.DTOs.PromoCode
{
    public class PromoCodeApplyResult
    {
        public bool IsValid { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public int? PromoCodeId { get; set; }
        public string? PromoCodeSnapshot { get; set; }
        public string? ErrorMessage { get; set; }

        public static PromoCodeApplyResult Invalid(string message) => new()
        {
            IsValid = false,
            ErrorMessage = message
        };

        public static PromoCodeApplyResult Success(int promoCodeId, string codeSnapshot, decimal discount, decimal baseAmount) => new()
        {
            IsValid = true,
            PromoCodeId = promoCodeId,
            PromoCodeSnapshot = codeSnapshot,
            DiscountAmount = discount,
            FinalAmount = Math.Max(0, baseAmount - discount)
        };
    }
}
