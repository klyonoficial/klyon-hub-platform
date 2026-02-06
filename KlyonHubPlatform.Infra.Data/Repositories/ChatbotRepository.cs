using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Infra.Data.Repositories
{
    public class ChatbotRepository : IChatbotRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatbotRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        #region FAQ Items

        public async Task<IEnumerable<ChatFaqItem>> GetAllActiveFaqItemsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ChatFaqItems
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.Category)
                .ToListAsync(cancellationToken);
        }

        public async Task<ChatFaqItem?> GetFaqItemByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ChatFaqItems.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<ChatFaqItem>> SearchFaqItemsAsync(string query, CancellationToken cancellationToken = default)
        {
            var lowerQuery = query.ToLower();
            return await _context.ChatFaqItems
                .Where(f => f.IsActive &&
                    (f.Question.ToLower().Contains(lowerQuery) ||
                     f.Keywords.ToLower().Contains(lowerQuery) ||
                     f.Answer.ToLower().Contains(lowerQuery)))
                .OrderByDescending(f => f.UsageCount)
                .ThenBy(f => f.DisplayOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<ChatFaqItem> CreateFaqItemAsync(ChatFaqItem faqItem, CancellationToken cancellationToken = default)
        {
            _context.ChatFaqItems.Add(faqItem);
            await _context.SaveChangesAsync(cancellationToken);
            return faqItem;
        }

        public async Task UpdateFaqItemAsync(ChatFaqItem faqItem, CancellationToken cancellationToken = default)
        {
            _context.ChatFaqItems.Update(faqItem);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task IncrementFaqUsageCountAsync(int faqItemId, CancellationToken cancellationToken = default)
        {
            var faqItem = await _context.ChatFaqItems.FindAsync(new object[] { faqItemId }, cancellationToken);
            if (faqItem != null)
            {
                faqItem.UsageCount++;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion

        #region Conversations

        public async Task<ChatConversation> CreateConversationAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
        {
            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync(cancellationToken);
            return conversation;
        }

        public async Task<ChatConversation?> GetConversationByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ChatConversations
                .Include(c => c.Messages)
                .Include(c => c.Intentions)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<ChatConversation?> GetActiveConversationBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatConversations
                .Include(c => c.Messages)
                .Include(c => c.Intentions)
                .Where(c => c.SessionId == sessionId && c.IsActive)
                .OrderByDescending(c => c.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task UpdateConversationAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
        {
            _context.ChatConversations.Update(conversation);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<ChatConversation>> GetRecentConversationsAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            return await _context.ChatConversations
                .Include(c => c.User)
                .Include(c => c.Messages)
                .OrderByDescending(c => c.StartedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Messages

        public async Task<ChatMessage> CreateMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);
            return message;
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatMessages
                .Include(m => m.FaqItem)
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            _context.ChatMessages.Update(message);
            await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region Intentions

        public async Task<ChatIntention> CreateIntentionAsync(ChatIntention intention, CancellationToken cancellationToken = default)
        {
            _context.ChatIntentions.Add(intention);
            await _context.SaveChangesAsync(cancellationToken);
            return intention;
        }

        public async Task<IEnumerable<ChatIntention>> GetIntentionsByConversationIdAsync(int conversationId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatIntentions
                .Where(i => i.ConversationId == conversationId)
                .OrderBy(i => i.DetectedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10, CancellationToken cancellationToken = default)
        {
            var query = _context.ChatIntentions.AsQueryable();

            if (from.HasValue)
                query = query.Where(i => i.DetectedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(i => i.DetectedAt <= to.Value);

            return await query
                .GroupBy(i => i.IntentionName)
                .Select(g => new { Intention = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(top)
                .ToDictionaryAsync(x => x.Intention, x => x.Count, cancellationToken);
        }

        #endregion

        #region Unanswered Questions

        public async Task<ChatUnansweredQuestion> CreateUnansweredQuestionAsync(ChatUnansweredQuestion question, CancellationToken cancellationToken = default)
        {
            _context.ChatUnansweredQuestions.Add(question);
            await _context.SaveChangesAsync(cancellationToken);
            return question;
        }

        public async Task<ChatUnansweredQuestion?> GetUnansweredQuestionByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ChatUnansweredQuestions
                .Include(q => q.Conversation)
                .Include(q => q.Message)
                .Include(q => q.Reviewer)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<ChatUnansweredQuestion>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null, CancellationToken cancellationToken = default)
        {
            var query = _context.ChatUnansweredQuestions
                .Include(q => q.Conversation)
                .Include(q => q.Reviewer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                var statusEnum = status.ToLowerInvariant() switch
                {
                    "pending" => UnansweredQuestionStatus.Pending,
                    "under_review" => UnansweredQuestionStatus.UnderReview,
                    "resolved" => UnansweredQuestionStatus.Resolved,
                    "added_to_faq" => UnansweredQuestionStatus.AddedToFaq,
                    _ => (UnansweredQuestionStatus?)null
                };
                if (statusEnum.HasValue)
                    query = query.Where(q => q.Status == statusEnum.Value);
            }

            if (minFrequency.HasValue)
                query = query.Where(q => q.Frequency >= minFrequency.Value);

            return await query
                .OrderByDescending(q => q.Frequency)
                .ThenByDescending(q => q.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateUnansweredQuestionAsync(ChatUnansweredQuestion question, CancellationToken cancellationToken = default)
        {
            _context.ChatUnansweredQuestions.Update(question);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<ChatUnansweredQuestion?> FindSimilarUnansweredQuestionAsync(string question, CancellationToken cancellationToken = default)
        {
            var lowerQuestion = question.ToLower();
            return await _context.ChatUnansweredQuestions
                .Where(q => q.Question.ToLower() == lowerQuestion)
                .OrderByDescending(q => q.Frequency)
                .FirstOrDefaultAsync(cancellationToken);
        }

        #endregion

        #region Statistics

        public async Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            var conversationQuery = _context.ChatConversations.AsQueryable();
            var messageQuery = _context.ChatMessages.AsQueryable();
            var unansweredQuery = _context.ChatUnansweredQuestions.AsQueryable();

            if (from.HasValue)
            {
                conversationQuery = conversationQuery.Where(c => c.StartedAt >= from.Value);
                messageQuery = messageQuery.Where(m => m.SentAt >= from.Value);
                unansweredQuery = unansweredQuery.Where(q => q.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                conversationQuery = conversationQuery.Where(c => c.StartedAt <= to.Value);
                messageQuery = messageQuery.Where(m => m.SentAt <= to.Value);
                unansweredQuery = unansweredQuery.Where(q => q.CreatedAt <= to.Value);
            }

            var totalConversations = await conversationQuery.CountAsync(cancellationToken);
            var resolvedConversations = await conversationQuery.CountAsync(c => c.WasResolved == true, cancellationToken);
            var redirectedToHuman = await conversationQuery.CountAsync(c => c.WasRedirectedToHuman, cancellationToken);
            var totalMessages = await messageQuery.CountAsync(cancellationToken);
            var answeredMessages = await messageQuery.CountAsync(m => m.WasAnswered, cancellationToken);
            var unansweredCount = await unansweredQuery.CountAsync(q => q.Status == UnansweredQuestionStatus.Pending, cancellationToken);
            var averageMessagesPerConversation = totalConversations > 0
                ? (decimal)totalMessages / totalConversations
                : 0;
            var resolutionRate = totalConversations > 0
                ? (decimal)resolvedConversations / totalConversations * 100
                : 0;

            return new Dictionary<string, object>
            {
                { "totalConversations", totalConversations },
                { "resolvedConversations", resolvedConversations },
                { "redirectedToHuman", redirectedToHuman },
                { "totalMessages", totalMessages },
                { "answeredMessages", answeredMessages },
                { "unansweredQuestions", unansweredCount },
                { "averageMessagesPerConversation", Math.Round(averageMessagesPerConversation, 2) },
                { "resolutionRate", Math.Round(resolutionRate, 2) }
            };
        }

        public async Task<IEnumerable<object>> GetConversationMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            var query = _context.ChatConversations.AsQueryable();

            if (from.HasValue)
                query = query.Where(c => c.StartedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.StartedAt <= to.Value);

            return await query
                .GroupBy(c => c.StartedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Count(),
                    Resolved = g.Count(c => c.WasResolved == true),
                    RedirectedToHuman = g.Count(c => c.WasRedirectedToHuman)
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync<object>(cancellationToken);
        }

        #endregion
    }
}
