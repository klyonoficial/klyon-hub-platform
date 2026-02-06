namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da pergunta não respondida no chatbot
    /// </summary>
    public enum UnansweredQuestionStatus
    {
        Pending = 0,
        UnderReview = 1,
        Resolved = 2,
        AddedToFaq = 3
    }
}
