using KlyonHubPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = Administrator)]
    [Route("admin/chatbot")]
    public class ChatbotAdminController : Controller
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotAdminController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        /// <summary>
        /// Dashboard principal do KlyonBoot
        /// </summary>
        [HttpGet("")]
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }

        /// <summary>
        /// Página de perguntas não respondidas
        /// </summary>
        [HttpGet("unanswered-questions")]
        public IActionResult UnansweredQuestions()
        {
            return View();
        }

        /// <summary>
        /// Página de análise de intenções
        /// </summary>
        [HttpGet("intentions")]
        public IActionResult Intentions()
        {
            return View();
        }

        /// <summary>
        /// Página de métricas e relatórios
        /// </summary>
        [HttpGet("metrics")]
        public IActionResult Metrics()
        {
            return View();
        }

        /// <summary>
        /// Página de gerenciamento de migrations
        /// </summary>
        [HttpGet("migrations")]
        public IActionResult DatabaseMigrations()
        {
            return View();
        }
    }
}
