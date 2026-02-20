namespace KlyonHubPlatform.Api.Constants
{
    public static class SessionKeys
    {
        public const string UserId = "UserId";
        public const string UserEmail = "UserEmail";
        public const string UserName = "UserName";
        public const string Token = "Token";
        public const string MustChangePassword = "MustChangePassword";
        public const string ChangePasswordReturnUrl = "ChangePasswordReturnUrl";
        public const string DefaultChangePasswordReturnUrl = "/";
        public const string DefaultChangePasswordReturnUrlAfterLogin = "/Client/DashboardDetails";
    }
}