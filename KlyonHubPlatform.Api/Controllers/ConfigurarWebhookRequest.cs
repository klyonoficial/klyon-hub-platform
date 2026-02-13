namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// Request para configurar webhook
    /// </summary>
    public class ConfigurarWebhookRequest
    {
        public string WebhookUrl { get; set; } = string.Empty;
    }
}
