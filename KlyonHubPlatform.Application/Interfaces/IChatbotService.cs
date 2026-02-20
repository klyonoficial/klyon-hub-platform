using KlyonHubPlatform.Application.DTOs;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IChatbotService
    {
        Task<ChatResponseDto> ProcessMessageAsync(ChatRequestDto request);
        Task<ConversationDto> StartConversationAsync(string sessionId, int? userId = null, string? ipAddress = null, string? userAgent = null);
        Task<ConversationDto?> GetActiveConversationAsync(string sessionId);
        Task MarkConversationAsResolvedAsync(int conversationId, bool wasResolved);
        Task EndConversationAsync(int conversationId);
        Task<IEnumerable<FaqItemDto>> SearchFaqAsync(string query);
        Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null);
        Task<IEnumerable<UnansweredQuestionDto>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null);
        Task UpdateUnansweredQuestionStatusAsync(int questionId, string status, int reviewedBy, string? notes = null);
        Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10);
    }
}