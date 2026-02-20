namespace KlyonHubPlatform.Application.DTOs
{
    public class QuickClientRegistrationResult
    {
        public bool Success { get; set; }
        public string? FailureKey { get; set; }
        public string? FailureArgument { get; set; }
        public string? WarningKey { get; set; }
        public string? WarningArgument { get; set; }
        public string? ContractNumber { get; set; }
        public string? PixCode { get; set; }
        public int? PaymentId { get; set; }
        public bool RedirectToSuccessPage { get; set; }

        public static QuickClientRegistrationResult Fail(string failureKey, string? failureArgument = null) => new()
        {
            Success = false,
            FailureKey = failureKey,
            FailureArgument = failureArgument
        };

        public static QuickClientRegistrationResult Ok(string contractNumber, string? pixCode, int? paymentId, bool redirectToSuccessPage, string? warningKey = null, string? warningArgument = null) => new()
        {
            Success = true,
            ContractNumber = contractNumber,
            PixCode = pixCode,
            PaymentId = paymentId,
            RedirectToSuccessPage = redirectToSuccessPage,
            WarningKey = warningKey,
            WarningArgument = warningArgument
        };
    }
}
