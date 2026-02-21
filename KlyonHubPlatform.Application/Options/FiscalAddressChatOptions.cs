namespace KlyonHubPlatform.Application.Options
{
    /// <summary>
    /// Configuração do chatbot de endereço fiscal (OpenAI e dados do serviço).
    /// </summary>
    public class FiscalAddressChatOptions
    {
        public const string SectionName = "FiscalAddressChat";

        /// <summary>Chave da API OpenAI.</summary>
        public string OpenAIApiKey { get; set; } = string.Empty;

        /// <summary>Modelo a usar (ex: gpt-4o-mini, gpt-4o).</summary>
        public string Model { get; set; } = "gpt-4o-mini";

        /// <summary>Valor do serviço de endereço fiscal (ex: R$ 99/mês).</summary>
        public string Valor { get; set; } = "A definir";

        /// <summary>Horário de atendimento (ex: 09:00 às 18:00).</summary>
        public string HorarioAtendimento { get; set; } = "09:00 às 18:00";

        /// <summary>Link do WhatsApp para direcionar o cliente quando não tiver a informação.</summary>
        public string WhatsAppUrl { get; set; } = "https://wa.me/27989030369";
    }
}
