namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço do chatbot de endereço fiscal (Klyon Coworking - Shopping Moxuara).
    /// Responde apenas dúvidas sobre o serviço de endereço fiscal.
    /// </summary>
    public interface IFiscalAddressChatService
    {
        /// <summary>
        /// Retorna a mensagem inicial exibida ao abrir o chat.
        /// </summary>
        string GetInitialMessage();

        /// <summary>
        /// Processa a mensagem do usuário e retorna a resposta do assistente via IA.
        /// </summary>
        /// <param name="userMessage">Pergunta ou mensagem do usuário.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Resposta do chatbot.</returns>
        Task<string> GetReplyAsync(string userMessage, CancellationToken cancellationToken = default);
    }
}
