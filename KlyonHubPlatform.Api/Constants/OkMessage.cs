namespace KlyonHubPlatform.Api.Constants
{
    public static class OkMessage
    {
        public const string WebhookAlreadyProcessed = "Webhook já processado";
        public const string WebhookProcessed = "Webhook processado";
        public const string ErrorProcessed = "Erro processado";
        public const string WebhookProcessedSuccess = "Webhook processado com sucesso";
        public const string WebhookConfiguredSuccess = "Webhook configurado com sucesso";
        public const string WebhookSimulatedProcessed = "Webhook simulado processado";
        public const string WebhookSimulatedSuccess = "Webhook simulado com sucesso";
        public const string ConversationMarkedSuccess = "Conversa marcada com sucesso";
        public const string ConversationEndedSuccess = "Conversa encerrada com sucesso";
        public const string QuestionUpdatedSuccess = "Pergunta atualizada com sucesso";
        public const string FocusNFeTokenNotConfigured = "Token da API Focus NFe não configurado";
        public const string FocusNFeApiAvailable = "API Focus NFe disponível";
        public const string FocusNFeApiUnavailable = "API Focus NFe indisponível";
        public static object WebhookReceivedResponse => new { received = true };
    }
}