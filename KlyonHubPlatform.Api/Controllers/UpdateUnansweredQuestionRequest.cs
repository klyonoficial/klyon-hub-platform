namespace KlyonHubPlatform.Api.Controllers
{
    public class UpdateUnansweredQuestionRequest
    {
        public string Status { get; set; } = string.Empty;
        public int ReviewedBy { get; set; }
        public string? Notes { get; set; }
    }
}
