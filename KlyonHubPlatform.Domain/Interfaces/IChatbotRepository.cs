using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Domain.Interfaces
{
    public interface IChatbotRepository
    {
        // FAQ Items
        Task<IEnumerable<ChatFaqItem>> GetAllActiveFaqItemsAsync();
        Task<ChatFaqItem?> GetFaqItemByIdAsync(int id);
        Task<IEnumerable<ChatFaqItem>> SearchFaqItemsAsync(string query);
        Task<ChatFaqItem> CreateFaqItemAsync(ChatFaqItem faqItem);
        Task UpdateFaqItemAsync(ChatFaqItem faqItem);
        Task IncrementFaqUsageCountAsync(int faqItemId);

        // Conversations
        Task<ChatConversation> CreateConversationAsync(ChatConversation conversation);
        Task<ChatConversation?> GetConversationByIdAsync(int id);
        Task<ChatConversation?> GetActiveConversationBySessionIdAsync(string sessionId);
        Task UpdateConversationAsync(ChatConversation conversation);
        Task<IEnumerable<ChatConversation>> GetRecentConversationsAsync(int take = 50);

        // Messages
        Task<ChatMessage> CreateMessageAsync(ChatMessage message);
        Task<IEnumerable<ChatMessage>> GetMessagesByConversationIdAsync(int conversationId);
        Task UpdateMessageAsync(ChatMessage message);

        // Intentions
        Task<ChatIntention> CreateIntentionAsync(ChatIntention intention);
        Task<IEnumerable<ChatIntention>> GetIntentionsByConversationIdAsync(int conversationId);
        Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10);

        // Unanswered Questions
        Task<ChatUnansweredQuestion> CreateUnansweredQuestionAsync(ChatUnansweredQuestion question);
        Task<ChatUnansweredQuestion?> GetUnansweredQuestionByIdAsync(int id);
        Task<IEnumerable<ChatUnansweredQuestion>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null);
        Task UpdateUnansweredQuestionAsync(ChatUnansweredQuestion question);
        Task<ChatUnansweredQuestion?> FindSimilarUnansweredQuestionAsync(string question);

        // Statistics
        Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null);
        Task<IEnumerable<object>> GetConversationMetricsAsync(DateTime? from = null, DateTime? to = null);
    }
}
