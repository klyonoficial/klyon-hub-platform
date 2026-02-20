using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IChatbotRepository
    {
        Task<IEnumerable<ChatFaqItem>> GetAllActiveFaqItemsAsync(CancellationToken cancellationToken = default);
        Task<ChatFaqItem?> GetFaqItemByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatFaqItem>> SearchFaqItemsAsync(string query, CancellationToken cancellationToken = default);
        Task<ChatFaqItem> CreateFaqItemAsync(ChatFaqItem faqItem, CancellationToken cancellationToken = default);
        Task UpdateFaqItemAsync(ChatFaqItem faqItem, CancellationToken cancellationToken = default);
        Task IncrementFaqUsageCountAsync(int faqItemId, CancellationToken cancellationToken = default);
        Task<ChatConversation> CreateConversationAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
        Task<ChatConversation?> GetConversationByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ChatConversation?> GetActiveConversationBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
        Task UpdateConversationAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatConversation>> GetRecentConversationsAsync(int take = 50, CancellationToken cancellationToken = default);
        Task<ChatMessage> CreateMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatMessage>> GetMessagesByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default);
        Task UpdateMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
        Task<ChatIntention> CreateIntentionAsync(ChatIntention intention, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatIntention>> GetIntentionsByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10, CancellationToken cancellationToken = default);
        Task<ChatUnansweredQuestion> CreateUnansweredQuestionAsync(ChatUnansweredQuestion question, CancellationToken cancellationToken = default);
        Task<ChatUnansweredQuestion?> GetUnansweredQuestionByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatUnansweredQuestion>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null, CancellationToken cancellationToken = default);
        Task UpdateUnansweredQuestionAsync(ChatUnansweredQuestion question, CancellationToken cancellationToken = default);
        Task<ChatUnansweredQuestion?> FindSimilarUnansweredQuestionAsync(string question, CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<object>> GetConversationMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    }
}