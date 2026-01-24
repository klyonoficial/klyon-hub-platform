using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        /// <summary>
        /// Inicia uma nova conversa com o KlyonBoot
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SessionId))
                {
                    return BadRequest(new { error = "SessionId é obrigatório" });
                }

                // Verificar se as tabelas do chatbot existem antes de tentar usar
                var context = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                try
                {
                    await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM ChatConversations LIMIT 1");
                }
                catch (Exception dbEx)
                {
                    var logger = HttpContext.RequestServices.GetService<ILogger<ChatbotController>>();
                    logger?.LogError(dbEx, "Tabelas do KlyonBoot não encontradas: {Message}", dbEx.Message);
                    
                    return StatusCode(500, new { 
                        error = "Tabelas do KlyonBoot não encontradas", 
                        details = "As tabelas do chatbot não foram criadas no banco de dados.",
                        solution = "Acesse /Admin/DatabaseManager e execute as migrations pendentes",
                        technicalDetails = dbEx.Message
                    });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                var conversation = await _chatbotService.StartConversationAsync(
                    request.SessionId,
                    request.UserId,
                    ipAddress,
                    userAgent
                );

                // Retornar resposta com mensagens
                return Ok(conversation);
            }
            catch (Exception ex)
            {
                // Log detalhado do erro
                var logger = HttpContext.RequestServices.GetService<ILogger<ChatbotController>>();
                logger?.LogError(ex, "Erro ao iniciar conversa: {Message}", ex.Message);
                
                // Verificar se é erro de tabela não existente
                var errorMessage = ex.Message;
                var innerMessage = ex.InnerException?.Message ?? "";
                var fullStackTrace = ex.ToString();
                
                // Verificar vários padrões de erro de tabela não existente
                if (errorMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (errorMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) || 
                     errorMessage.Contains("não existe", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase) ||
                     innerMessage.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                     (innerMessage.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                      innerMessage.Contains("Unknown table", StringComparison.OrdinalIgnoreCase))) ||
                    fullStackTrace.Contains("Table", StringComparison.OrdinalIgnoreCase) && 
                    (fullStackTrace.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                     fullStackTrace.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)))
                {
                    return StatusCode(500, new { 
                        error = "Tabelas do KlyonBoot não encontradas", 
                        details = "As tabelas do chatbot não foram criadas no banco de dados. Execute as migrations em /Admin/DatabaseManager",
                        solution = "Acesse /Admin/DatabaseManager e execute as migrations pendentes",
                        technicalDetails = errorMessage
                    });
                }
                
                if (errorMessage.Contains("banco de dados", StringComparison.OrdinalIgnoreCase) || 
                    errorMessage.Contains("database", StringComparison.OrdinalIgnoreCase) || 
                    errorMessage.Contains("tabelas", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(500, new { 
                        error = "Erro de banco de dados", 
                        details = errorMessage,
                        solution = "Verifique se as migrations foram aplicadas em /Admin/DatabaseManager",
                        technicalDetails = innerMessage
                    });
                }
                
                return StatusCode(500, new { 
                    error = "Erro ao iniciar conversa", 
                    details = errorMessage,
                    innerException = innerMessage
                });
            }
        }

        /// <summary>
        /// Processa uma mensagem do usuário e retorna a resposta do KlyonBoot
        /// </summary>
        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(new { error = "Mensagem não pode estar vazia" });

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                request.IpAddress = ipAddress;
                request.UserAgent = userAgent;

                var response = await _chatbotService.ProcessMessageAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao processar mensagem", details = ex.Message });
            }
        }

        /// <summary>
        /// Marca uma conversa como resolvida ou não
        /// </summary>
        [HttpPost("conversation/{conversationId}/resolve")]
        public async Task<IActionResult> ResolveConversation(int conversationId, [FromBody] ResolveRequest request)
        {
            try
            {
                await _chatbotService.MarkConversationAsResolvedAsync(conversationId, request.WasResolved);
                return Ok(new { message = "Conversa marcada com sucesso" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao marcar conversa", details = ex.Message });
            }
        }

        /// <summary>
        /// Encerra uma conversa
        /// </summary>
        [HttpPost("conversation/{conversationId}/end")]
        public async Task<IActionResult> EndConversation(int conversationId)
        {
            try
            {
                await _chatbotService.EndConversationAsync(conversationId);
                return Ok(new { message = "Conversa encerrada com sucesso" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao encerrar conversa", details = ex.Message });
            }
        }

        /// <summary>
        /// Busca no FAQ
        /// </summary>
        [HttpGet("faq/search")]
        public async Task<IActionResult> SearchFaq([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { error = "Query não pode estar vazia" });

                var results = await _chatbotService.SearchFaqAsync(query);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao buscar FAQ", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtém estatísticas do dashboard (Admin)
        /// </summary>
        [HttpGet("admin/statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var stats = await _chatbotService.GetDashboardStatisticsAsync(from, to);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao obter estatísticas", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtém perguntas não respondidas (Admin)
        /// </summary>
        [HttpGet("admin/unanswered-questions")]
        public async Task<IActionResult> GetUnansweredQuestions([FromQuery] string? status, [FromQuery] int? minFrequency)
        {
            try
            {
                var questions = await _chatbotService.GetUnansweredQuestionsAsync(status, minFrequency);
                return Ok(questions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao obter perguntas não respondidas", details = ex.Message });
            }
        }

        /// <summary>
        /// Atualiza o status de uma pergunta não respondida (Admin)
        /// </summary>
        [HttpPut("admin/unanswered-questions/{questionId}")]
        public async Task<IActionResult> UpdateUnansweredQuestion(int questionId, [FromBody] UpdateUnansweredQuestionRequest request)
        {
            try
            {
                await _chatbotService.UpdateUnansweredQuestionStatusAsync(
                    questionId,
                    request.Status,
                    request.ReviewedBy,
                    request.Notes
                );

                return Ok(new { message = "Pergunta atualizada com sucesso" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao atualizar pergunta", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtém top intenções detectadas (Admin)
        /// </summary>
        [HttpGet("admin/top-intentions")]
        public async Task<IActionResult> GetTopIntentions([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int top = 10)
        {
            try
            {
                var intentions = await _chatbotService.GetTopIntentionsAsync(from, to, top);
                return Ok(intentions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao obter intenções", details = ex.Message });
            }
        }

        /// <summary>
        /// Diagnóstico - Verifica se as tabelas do KlyonBoot existem
        /// </summary>
        [HttpGet("diagnostics/tables")]
        public async Task<IActionResult> CheckTables()
        {
            try
            {
                var context = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var tables = new List<string> { "ChatFaqItems", "ChatConversations", "ChatMessages", "ChatIntentions", "ChatUnansweredQuestions" };
                var results = new Dictionary<string, bool>();

                foreach (var table in tables)
                {
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync($"SELECT 1 FROM `{table}` LIMIT 1");
                        results[table] = true;
                    }
                    catch
                    {
                        results[table] = false;
                    }
                }

                var allExist = results.Values.All(x => x);
                return Ok(new
                {
                    allTablesExist = allExist,
                    tables = results,
                    message = allExist 
                        ? "Todas as tabelas do KlyonBoot existem" 
                        : "Algumas tabelas do KlyonBoot não existem. Execute as migrations em /Admin/DatabaseManager"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao verificar tabelas", details = ex.Message });
            }
        }
    }

    // DTOs para requests
    public class StartConversationRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
    }

    public class ResolveRequest
    {
        public bool WasResolved { get; set; }
    }

    public class UpdateUnansweredQuestionRequest
    {
        public string Status { get; set; } = string.Empty;
        public int ReviewedBy { get; set; }
        public string? Notes { get; set; }
    }
}
