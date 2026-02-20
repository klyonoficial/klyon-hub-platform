namespace KlyonHubPlatform.Api.Requests
{
    public class StartConversationRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
    }
}
