using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Interfaces;
using System.Text.Json;

namespace KlyonHubPlatform.Application.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly IChatbotRepository _repository;
        private readonly IAIIntentionService _aiService;

        // Mensagens padrão do KlyonBoot
        private const string WELCOME_MESSAGE = "Olá! 😊 Sou o KlyonBoot, assistente oficial da plataforma Klyon Coworking. Como posso ajudar você hoje?";
        private const string FAREWELL_MESSAGE = "Foi um prazer ajudar! Se precisar de algo, estou por aqui. Até logo! 👋";
        private const string NO_ANSWER_MESSAGE = "No momento não tenho essa informação. Posso te direcionar para o atendimento humano se quiser. 🤝";
        private const decimal MINIMUM_CONFIDENCE = 0.6m; // 60% de confiança mínima

        public ChatbotService(IChatbotRepository repository, IAIIntentionService aiService)
        {
            _repository = repository;
            _aiService = aiService;
        }

        public async Task<ChatResponseDto> ProcessMessageAsync(ChatRequestDto request)
        {
            // 1. Buscar ou criar conversa
            var conversation = await _repository.GetActiveConversationBySessionIdAsync(request.SessionId);
            if (conversation == null)
            {
                conversation = new ChatConversation
                {
                    SessionId = request.SessionId,
                    UserId = request.UserId,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    StartedAt = DateTime.Now,
                    IsActive = true
                };
                conversation = await _repository.CreateConversationAsync(conversation);
            }

            // 2. Salvar mensagem do usuário
            var userMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                MessageType = "user",
                Content = request.Message,
                SentAt = DateTime.Now
            };
            userMessage = await _repository.CreateMessageAsync(userMessage);

            // 3. Detectar intenção com IA
            var intentionResult = await _aiService.DetectIntentionAsync(request.Message);

            // 4. Salvar intenção detectada
            if (intentionResult.Confidence >= 0.3m) // Salvar se tiver pelo menos 30% de confiança
            {
                var intention = new ChatIntention
                {
                    ConversationId = conversation.Id,
                    MessageId = userMessage.Id,
                    IntentionName = intentionResult.IntentionName,
                    Category = intentionResult.Category,
                    Confidence = intentionResult.Confidence,
                    Entities = intentionResult.Entities != null ? JsonSerializer.Serialize(intentionResult.Entities) : null,
                    DetectedAt = DateTime.Now
                };
                await _repository.CreateIntentionAsync(intention);
            }

            // 5. Verificar se é saudação ou despedida
            if (intentionResult.IntentionName == "saudacao")
            {
                return await CreateBotResponse(conversation.Id, userMessage.Id, WELCOME_MESSAGE, null, true, false);
            }

            if (intentionResult.IntentionName == "despedida")
            {
                await EndConversationAsync(conversation.Id);
                return await CreateBotResponse(conversation.Id, userMessage.Id, FAREWELL_MESSAGE, null, true, false);
            }

            // 6. Buscar resposta na base de conhecimento (FAQ)
            var faqItems = await _repository.SearchFaqItemsAsync(request.Message);
            
            ChatFaqItem? bestMatch = null;
            decimal bestScore = 0;

            foreach (var faqItem in faqItems)
            {
                // Calcular similaridade com a pergunta
                var similarity = _aiService.CalculateSimilarity(request.Message, faqItem.Question);
                
                // Verificar se a intenção bate com as intenções do FAQ
                var intentionMatch = faqItem.Intentions.Split(',')
                    .Any(i => i.Trim().Equals(intentionResult.IntentionName, StringComparison.OrdinalIgnoreCase));

                // Score combinado
                var score = (decimal)similarity * 0.7m + (intentionMatch ? 0.3m : 0m);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = faqItem;
                }
            }

            // 7. Se encontrou uma boa resposta no FAQ
            if (bestMatch != null && bestScore >= MINIMUM_CONFIDENCE)
            {
                // Incrementar contador de uso
                await _repository.IncrementFaqUsageCountAsync(bestMatch.Id);

                // Criar resposta do bot
                var response = await CreateBotResponse(
                    conversation.Id,
                    userMessage.Id,
                    bestMatch.Answer,
                    bestMatch.Id,
                    true,
                    false,
                    bestScore
                );

                response.IntentionDetected = intentionResult.IntentionName;
                return response;
            }

            // 8. Não encontrou resposta - registrar como pergunta não respondida
            var existingUnanswered = await _repository.FindSimilarUnansweredQuestionAsync(request.Message);
            
            if (existingUnanswered != null)
            {
                // Incrementar frequência
                existingUnanswered.Frequency++;
                await _repository.UpdateUnansweredQuestionAsync(existingUnanswered);
            }
            else
            {
                // Criar novo registro
                var unansweredQuestion = new ChatUnansweredQuestion
                {
                    ConversationId = conversation.Id,
                    MessageId = userMessage.Id,
                    Question = request.Message,
                    DetectedIntention = intentionResult.IntentionName,
                    SuggestedCategory = intentionResult.Category,
                    Status = UnansweredQuestionStatus.Pending,
                    Frequency = 1,
                    CreatedAt = DateTime.Now
                };
                await _repository.CreateUnansweredQuestionAsync(unansweredQuestion);
            }

            // 9. Retornar mensagem padrão de "não sei"
            var noAnswerResponse = await CreateBotResponse(
                conversation.Id,
                userMessage.Id,
                NO_ANSWER_MESSAGE,
                null,
                false,
                true,
                intentionResult.Confidence
            );

            noAnswerResponse.IntentionDetected = intentionResult.IntentionName;
            noAnswerResponse.SuggestHumanSupport = true;

            // Marcar conversa como não resolvida
            conversation.WasRedirectedToHuman = true;
            await _repository.UpdateConversationAsync(conversation);

            return noAnswerResponse;
        }

        public async Task<ConversationDto> StartConversationAsync(string sessionId, int? userId = null, string? ipAddress = null, string? userAgent = null)
        {
            // Validar sessionId
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));
            }

            var conversation = new ChatConversation
            {
                SessionId = sessionId,
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                StartedAt = DateTime.Now,
                IsActive = true
            };

            try
            {
                conversation = await _repository.CreateConversationAsync(conversation);
            }
            catch (Exception ex)
            {
                // Verificar se é erro de tabela não existente
                var errorMessage = ex.Message;
                var innerMessage = ex.InnerException?.Message ?? "";
                var fullStackTrace = ex.ToString();
                
                if (errorMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (errorMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) || 
                     errorMessage.Contains("não existe", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)) ||
                    innerMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (innerMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                     innerMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)) ||
                    fullStackTrace.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (fullStackTrace.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                     fullStackTrace.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        "Tabelas do KlyonBoot não encontradas. As tabelas do chatbot não foram criadas no banco de dados. Execute as migrations em /Admin/DatabaseManager.", ex);
                }
                
                throw new InvalidOperationException($"Erro ao criar conversa no banco de dados. Verifique se as tabelas do KlyonBoot foram criadas. Erro: {ex.Message}", ex);
            }

            // Enviar mensagem de boas-vindas
            var welcomeMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                MessageType = "bot",
                Content = WELCOME_MESSAGE,
                WasAnswered = true,
                SentAt = DateTime.Now
            };
            
            try
            {
                await _repository.CreateMessageAsync(welcomeMessage);
            }
            catch (Exception ex)
            {
                // Verificar se é erro de tabela não existente
                var errorMessage = ex.Message;
                var innerMessage = ex.InnerException?.Message ?? "";
                var fullStackTrace = ex.ToString();
                
                if (errorMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (errorMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) || 
                     errorMessage.Contains("não existe", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)) ||
                    innerMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (innerMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                     innerMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)) ||
                    fullStackTrace.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (fullStackTrace.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                     fullStackTrace.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        "Tabelas do KlyonBoot não encontradas. As tabelas do chatbot não foram criadas no banco de dados. Execute as migrations em /Admin/DatabaseManager.", ex);
                }
                
                throw new InvalidOperationException($"Erro ao criar mensagem de boas-vindas. Erro: {ex.Message}", ex);
            }

            // Buscar mensagens da conversa (incluindo a mensagem de boas-vindas que acabamos de criar)
            IEnumerable<ChatMessage> messages;
            try
            {
                messages = await _repository.GetMessagesByConversationIdAsync(conversation.Id);
            }
            catch (Exception ex)
            {
                // Se falhar ao buscar mensagens, usar lista vazia mas não falhar completamente
                messages = new List<ChatMessage> { welcomeMessage };
            }
            
            var dto = new ConversationDto
            {
                Id = conversation.Id,
                SessionId = conversation.SessionId,
                UserId = conversation.UserId,
                StartedAt = conversation.StartedAt,
                IsActive = conversation.IsActive,
                Messages = messages.Select(m => new MessageDto
                {
                    Id = m.Id,
                    MessageType = m.MessageType,
                    Content = m.Content,
                    WasAnswered = m.WasAnswered,
                    SentAt = m.SentAt
                }).ToList()
            };
            
            return dto;
        }

        public async Task<ConversationDto?> GetActiveConversationAsync(string sessionId)
        {
            var conversation = await _repository.GetActiveConversationBySessionIdAsync(sessionId);
            if (conversation == null)
                return null;

            return new ConversationDto
            {
                Id = conversation.Id,
                SessionId = conversation.SessionId,
                UserId = conversation.UserId,
                StartedAt = conversation.StartedAt,
                EndedAt = conversation.EndedAt,
                IsActive = conversation.IsActive,
                WasResolved = conversation.WasResolved,
                Messages = conversation.Messages.Select(m => new MessageDto
                {
                    Id = m.Id,
                    MessageType = m.MessageType,
                    Content = m.Content,
                    WasAnswered = m.WasAnswered,
                    SentAt = m.SentAt
                }).ToList()
            };
        }

        public async Task MarkConversationAsResolvedAsync(int conversationId, bool wasResolved)
        {
            var conversation = await _repository.GetConversationByIdAsync(conversationId);
            if (conversation != null)
            {
                conversation.WasResolved = wasResolved;
                await _repository.UpdateConversationAsync(conversation);
            }
        }

        public async Task EndConversationAsync(int conversationId)
        {
            var conversation = await _repository.GetConversationByIdAsync(conversationId);
            if (conversation != null)
            {
                conversation.IsActive = false;
                conversation.EndedAt = DateTime.Now;
                await _repository.UpdateConversationAsync(conversation);
            }
        }

        public async Task<IEnumerable<FaqItemDto>> SearchFaqAsync(string query)
        {
            var faqItems = await _repository.SearchFaqItemsAsync(query);
            return faqItems.Select(f => new FaqItemDto
            {
                Id = f.Id,
                Category = f.Category,
                Question = f.Question,
                Answer = f.Answer,
                UsageCount = f.UsageCount
            });
        }

        public async Task<Dictionary<string, object>> GetDashboardStatisticsAsync(DateTime? from = null, DateTime? to = null)
        {
            return await _repository.GetDashboardStatisticsAsync(from, to);
        }

        public async Task<IEnumerable<UnansweredQuestionDto>> GetUnansweredQuestionsAsync(string? status = null, int? minFrequency = null)
        {
            var questions = await _repository.GetUnansweredQuestionsAsync(status, minFrequency);
            return questions.Select(q => new UnansweredQuestionDto
            {
                Id = q.Id,
                Question = q.Question,
                DetectedIntention = q.DetectedIntention,
                SuggestedCategory = q.SuggestedCategory,
                Status = q.Status.ToString(),
                Frequency = q.Frequency,
                CreatedAt = q.CreatedAt,
                ReviewedByName = q.Reviewer?.FullName,
                ReviewedAt = q.ReviewedAt,
                ReviewNotes = q.ReviewNotes
            });
        }

        public async Task UpdateUnansweredQuestionStatusAsync(int questionId, string status, int reviewedBy, string? notes = null)
        {
            var question = await _repository.GetUnansweredQuestionByIdAsync(questionId);
            if (question != null && Enum.TryParse<UnansweredQuestionStatus>(status, true, out var statusEnum))
            {
                question.Status = statusEnum;
                question.ReviewedBy = reviewedBy;
                question.ReviewedAt = DateTime.Now;
                question.ReviewNotes = notes;
                await _repository.UpdateUnansweredQuestionAsync(question);
            }
        }

        public async Task<Dictionary<string, int>> GetTopIntentionsAsync(DateTime? from = null, DateTime? to = null, int top = 10)
        {
            return await _repository.GetTopIntentionsAsync(from, to, top);
        }

        #region Private Methods

        private async Task<ChatResponseDto> CreateBotResponse(
            int conversationId,
            int userMessageId,
            string responseText,
            int? faqItemId,
            bool wasAnswered,
            bool usedAI,
            decimal? confidence = null)
        {
            var botMessage = new ChatMessage
            {
                ConversationId = conversationId,
                MessageType = "bot",
                Content = responseText,
                FaqItemId = faqItemId,
                WasAnswered = wasAnswered,
                UsedAI = usedAI,
                ConfidenceScore = confidence,
                SentAt = DateTime.Now
            };

            botMessage = await _repository.CreateMessageAsync(botMessage);

            return new ChatResponseDto
            {
                ConversationId = conversationId,
                MessageId = botMessage.Id,
                Response = responseText,
                MessageType = "bot",
                WasAnswered = wasAnswered,
                UsedAI = usedAI,
                ConfidenceScore = confidence,
                SentAt = botMessage.SentAt
            };
        }

        #endregion
    }
}
