namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class CoworkingHistoryActionLabels
    {
        public const string Submitted = "Submitted";
        public const string ApprovalStatusChange = "ApprovalStatusChange";
        public const string ResubmitForApproval = "ResubmitForApproval";
        public const string ReturnedToReview = "ReturnedToReview";
        public const string SubmittedDisplay = "Cadastro enviado";
        public const string ApprovalStatusChangeDisplay = "Mudança de status";
        public const string ResubmitForApprovalDisplay = "Reenviado para análise";
        public const string ReturnedToReviewDisplay = "Retornou para análise";
        public static string GetDisplayLabel(string action) => action switch
        {
            Submitted => SubmittedDisplay,
            ApprovalStatusChange => ApprovalStatusChangeDisplay,
            ResubmitForApproval => ResubmitForApprovalDisplay,
            ReturnedToReview => ReturnedToReviewDisplay,
            _ => action
        };
    }
}