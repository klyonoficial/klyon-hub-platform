namespace KlyonHubPlatform.Api.Constants
{
    public static class RouteFilterValues
    {
        public static class PaymentStatusFilter
        {
            public const string Pending = "Pending";
            public const string AwaitingValidation = "AwaitingValidation";
        }
        public static class ApprovalStatusFilter
        {
            public const string UnderReview = "UnderReview";
        }
        public static class UserFilter
        {
            public const string Inactive = "inactive";
            public const string Unverified = "unverified";
        }
        public static class ContractStatusFilter
        {
            public const string Active = "Active";
        }
    }
}