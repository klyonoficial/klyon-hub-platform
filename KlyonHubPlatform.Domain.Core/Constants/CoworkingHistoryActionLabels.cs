namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class CoworkingHistoryActionLabels
    {
        public const string Submitted = "Submitted";
        public const string ApprovalStatusChange = "ApprovalStatusChange";
        public const string ResubmitForApproval = "ResubmitForApproval";
        public const string ReturnedToReview = "ReturnedToReview";
        public const string CardPricesSubmitted = "CardPricesSubmitted";
        public const string CardPricesApproved = "CardPricesApproved";
        public const string CardPricesRejected = "CardPricesRejected";
        public const string AddressUpdated = "AddressUpdated";
        public const string SubmittedDisplay = "Cadastro enviado";
        public const string ApprovalStatusChangeDisplay = "Mudança de status";
        public const string ResubmitForApprovalDisplay = "Reenviado para análise";
        public const string ReturnedToReviewDisplay = "Retornou para análise";
        public const string CardPricesSubmittedDisplay = "Preços no cartão alterados (parceiro)";
        public const string CardPricesApprovedDisplay = "Preços no cartão aprovados (admin)";
        public const string CardPricesRejectedDisplay = "Preços no cartão reprovados (admin)";
        public const string AddressUpdatedDisplay = "Endereço alterado";
        public static string GetDisplayLabel(string action) => action switch
        {
            Submitted => SubmittedDisplay,
            ApprovalStatusChange => ApprovalStatusChangeDisplay,
            ResubmitForApproval => ResubmitForApprovalDisplay,
            ReturnedToReview => ReturnedToReviewDisplay,
            CardPricesSubmitted => CardPricesSubmittedDisplay,
            CardPricesApproved => CardPricesApprovedDisplay,
            CardPricesRejected => CardPricesRejectedDisplay,
            AddressUpdated => AddressUpdatedDisplay,
            _ => action
        };
    }
}