namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    public class FiscalOnboardingStartResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public Guid? PublicPaymentToken { get; init; }
        public FiscalOnboardingStartBlockedKind BlockedKind { get; init; }

        public static FiscalOnboardingStartResult Ok(Guid publicToken) => new()
        {
            Success = true,
            PublicPaymentToken = publicToken,
            BlockedKind = FiscalOnboardingStartBlockedKind.None
        };

        public static FiscalOnboardingStartResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message,
            BlockedKind = FiscalOnboardingStartBlockedKind.None
        };

        public static FiscalOnboardingStartResult Blocked(FiscalOnboardingStartBlockedKind kind, string? message = null) => new()
        {
            Success = false,
            ErrorMessage = message,
            BlockedKind = kind
        };
    }
}
