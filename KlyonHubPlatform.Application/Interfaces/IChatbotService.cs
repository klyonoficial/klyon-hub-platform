using KlyonHubPlatform.Application.DTOs;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IChatbotService
    {
        /// <summary>
        /// Processa uma mensagem do usuário e retorna a resposta do KlyonBoot
        /// </summary>
        Task<ChatResponseDto> ProcessMessageAsync(ChatRequestDto request);

        /// <summary>
        /// Inicia uma nova conversa
        /// </summary>
        Task<ConversationDto> StartConversationAsync(string sessionId, int? userId = null, string? ipAddress = null, string? userAgent = null);

        /// <summary>
        /// Obtém uma conversa ativa por session ID
        /// </summary>
        Task<ConversationDto?> GetActiveConversationAsync(string sessionId);

        /// <summary>
        /// Marca uma conversa como resolvida
        /// </summary>
        Task MarkConversationAsResolvedAsync(int conversationId, bool wasResolved);

        /// <summary>
        /// Encerra uma conversa
        /// </summary>
        Task EndConversationAsync(int conversationId);

        /// <summary>
        /// Busca na base de conhecimento (FAQ)
        /// </summary>
        Task<IEnumerable<FaqItemDto>> SearchFaqAsync(string query);

        /// <summary>
        /// Obtém estatísticas do dashboard
        /// </summary>
        Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Obtém perguntas não respondidas
        /// </summary>
        Task<IEnumerable<UnansweredQuestionDto>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null);

        /// <summary>
        /// Atualiza o status de uma pergunta não respondida
        /// </summary>
        Task UpdateUnansweredQuestionStatusAsync(int questionId, string status, int reviewedBy, string? notes = null);

        /// <summary>
        /// Obtém top intenções detectadas
        /// </summary>
        Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10);
    }
}
