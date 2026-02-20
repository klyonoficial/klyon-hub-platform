namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Valores de filtro usados em rotas (query string) para Admin e listagens.
    /// Use em Url.Action(..., new { status = RouteFilterValues.PaymentStatus.Pending }) em vez de literais.
    /// </summary>
    public static class RouteFilterValues
    {
        /// <summary>Valores de status de pagamento para filtro (Admin/Payments).</summary>
        public static class PaymentStatusFilter
        {
            public const string Pending = "Pending";
            public const string AwaitingValidation = "AwaitingValidation";
        }

        /// <summary>Valores de status de aprovação de coworking para filtro (Admin/Coworkings).</summary>
        public static class ApprovalStatusFilter
        {
            public const string UnderReview = "UnderReview";
        }

        /// <summary>Valores de filtro de usuários (Admin/Users).</summary>
        public static class UserFilter
        {
            public const string Inactive = "inactive";
            public const string Unverified = "unverified";
        }

        /// <summary>Valores de status de contrato para filtro (Admin/Contracts).</summary>
        public static class ContractStatusFilter
        {
            public const string Active = "Active";
        }
    }
}
