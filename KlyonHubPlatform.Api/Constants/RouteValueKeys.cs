namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Nomes dos parâmetros de rota/query usados em Url.Action e RedirectToAction (new { key = value }).
    /// Use estas constantes em vez de literais para evitar erros de digitação.
    /// </summary>
    public static class RouteValueKeys
    {
        public const string Id = "id";
        public const string Status = "status";
        public const string Page = "page";
        public const string PageSize = "pageSize";
        public const string Search = "search";
        public const string IsPartner = "isPartner";
        public const string ApprovalStatus = "approvalStatus";
        public const string CapacityAlert = "capacityAlert";
        public const string EmailVerified = "emailVerified";
        public const string NoteNumber = "noteNumber";
        public const string Client = "client";
        public const string FiscalNoteId = "fiscalNoteId";
        public const string DocumentId = "documentId";
        public const string PhotoId = "photoId";
        public const string CoworkingId = "coworkingId";
        public const string PaymentId = "paymentId";
        public const string ReturnUrl = "returnUrl";
    }
}
