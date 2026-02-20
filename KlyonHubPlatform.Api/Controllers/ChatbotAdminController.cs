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
        [HttpGet("")]
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }
        [HttpGet("unanswered-questions")]
        public IActionResult UnansweredQuestions()
        {
            return View();
        }
        [HttpGet("intentions")]
        public IActionResult Intentions()
        {
            return View();
        }
        [HttpGet("metrics")]
        public IActionResult Metrics()
        {
            return View();
        }
        [HttpGet("migrations")]
        public IActionResult DatabaseMigrations()
        {
            return View();
        }
    }
}