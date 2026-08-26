using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Options;

namespace KlyonHubPlatform.Application.Services
{
    public class FiscalAddressChatService : IFiscalAddressChatService
    {
        /// <summary>Mensagem exibida ao abrir o chat: convite para o cliente perguntar (quem pergunta é o cliente).</summary>
        public const string InitialMessage = "Olá! Como posso ajudar com endereço fiscal? Envie sua pergunta.";
        private const string OffTopicReply = "Posso ajudar apenas com informações sobre endereço fiscal.";

        private readonly FiscalAddressChatOptions _options;
        private readonly ILogger<FiscalAddressChatService> _logger;

        public FiscalAddressChatService(
            IOptions<FiscalAddressChatOptions> options,
            ILogger<FiscalAddressChatService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetInitialMessage() => InitialMessage;

        public async Task<string> GetReplyAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return GetUnknownReply();

            // Respostas fixas para as perguntas mais comuns (garante resposta correta mesmo se a IA falhar)
            var faqReply = TryGetFaqReply(userMessage.Trim());
            if (faqReply != null)
                return faqReply;

            var apiKey = _options.OpenAIApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("FiscalAddressChat: OpenAIApiKey não configurada.");
                return GetUnknownReply();
            }

            var systemPrompt = BuildSystemPrompt();
            try
            {
                var client = new ChatClient(_options.Model, apiKey);
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };
                var chatOptions = new ChatCompletionOptions { MaxOutputTokenCount = 512 };
                var completion = await client.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var text = completion.Value?.Content?[0].Text?.Trim();
                return string.IsNullOrEmpty(text) ? GetUnknownReply() : text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FiscalAddressChat: Erro ao chamar OpenAI. {Message}", ex.Message);
                return GetUnknownReply();
            }
        }

        /// <summary>Retorna resposta da base de FAQs se a mensagem for reconhecida; caso contrário null.</summary>
        private string? TryGetFaqReply(string msg)
        {
            var t = msg.ToLowerInvariant();
            var whatsApp = string.IsNullOrWhiteSpace(_options.WhatsAppUrl) ? "https://wa.me/27989030369" : _options.WhatsAppUrl.Trim();

            if (ContainsAny(t, "como contratar", "contratar", "contrata"))
                return "Você pode contratar direto pela plataforma: acesse a listagem de endereços em várias cidades e finalize em poucos cliques. Comece em: /Client/ChooseLocation, ou fale conosco pelo WhatsApp se preferir: " + whatsApp;
            if (ContainsAny(t, "onde fica", "localização", "localizacao", "endereço", "endereco", "cidades", "estados"))
                return "Temos endereços fiscais em várias cidades: escolha estado e cidade em /Client/ChooseLocation. Também operamos no Shopping Moxuara, Cariacica/ES.";
            if (ContainsAny(t, "valor", "preço", "preco", "quanto custa", "valor do", "mensal", "anual"))
                return "O valor varia por endereço e cidade: consulte os preços na listagem (/Client/ChooseLocation). Exemplo Moxuara/Cariacica: R$ 718,80 à vista no Pix; ou parcelado no cartão.";
            if (ContainsAny(t, "o que está incluso", "o que inclui", "inclui", "incluso", "incluído"))
                return "Inclui uso do endereço fiscal, recebimento de correspondências e aviso quando houver entrega.";
            if (ContainsAny(t, "abrir empresa", "abertura", "registro da empresa"))
                return "Sim, o endereço pode ser usado para abertura e registro da empresa.";
            if (ContainsAny(t, "recebem correspondência", "correspondência", "recebem carta", "avisam quando chegar"))
                return "Sim, recebemos e avisamos quando chegar.";
            if (ContainsAny(t, "horário", "horario", "atendimento", "funcionamento", "abre", "fecha"))
                return "Atendimento das 09:00 às 18:00.";
            if (ContainsAny(t, "documentos", "documentação"))
                return "Para saber os documentos necessários, fale com nosso atendimento via WhatsApp.";
            if (ContainsAny(t, "tem contrato", "contrato"))
                return "Sim, o serviço é formalizado por contrato.";
            if (ContainsAny(t, "prazo mínimo", "prazo minimo", "mínimo de", "6 meses"))
                return "O prazo mínimo é de 6 meses.";
            if (ContainsAny(t, "visitar", "visita", "conhecer o local"))
                return "Sim, agende uma visita pelo WhatsApp.";
            if (ContainsAny(t, "sede", "sede da empresa", "endereço fiscal da empresa"))
                return "Sim, pode ser usado como endereço fiscal da empresa.";
            if (ContainsAny(t, "mei", "atendem mei"))
                return "Sim, atendemos MEI e outros tipos de empresa.";
            if (ContainsAny(t, "pagamento", "pagar", "forma de pagamento"))
                return "Detalhes de pagamento são informados pelo WhatsApp.";
            if (ContainsAny(t, "taxa de adesão", "adesão"))
                return "Consulte nosso atendimento via WhatsApp.";
            if (ContainsAny(t, "como recebo", "minhas correspondências", "recebo correspondência"))
                return "Você será avisado quando houver correspondência disponível.";
            if (ContainsAny(t, "imediato", "disponibilidade"))
                return "Consulte disponibilidade pelo WhatsApp.";
            if (ContainsAny(t, "cancelar", "cancelamento"))
                return "Informações sobre cancelamento são fornecidas pelo atendimento.";
            if (ContainsAny(t, "alterar dados", "mudar dados"))
                return "Consulte nosso atendimento via WhatsApp.";
            if (ContainsAny(t, "compartilhado", "compartilham"))
                return "Sim, o endereço pode ser utilizado por múltiplas empresas.";
            if (ContainsAny(t, "nota fiscal", "nf"))
                return "Sim, pode ser utilizado como endereço fiscal da empresa.";
            if (ContainsAny(t, "receita federal", "válido", "valido"))
                return "Sim, é válido para registro fiscal da empresa.";
            if (ContainsAny(t, "trabalham com endereço fiscal", "trabalha com endereço", "tem endereço fiscal", "têm endereço fiscal", "cariacica"))
                return "Sim, trabalhamos com endereço fiscal no Shopping Moxuara em Cariacica/ES. Deseja saber mais?";
            if (ContainsAny(t, "atendente", "falar com", "contato", "whatsapp"))
                return $"Fale conosco pelo WhatsApp: {whatsApp}";

            return null;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (var term in terms)
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private string GetUnknownReply() =>
            string.IsNullOrWhiteSpace(_options.WhatsAppUrl)
                ? "Não tenho essa informação no momento. Para mais detalhes, fale com nosso atendimento no WhatsApp."
                : $"Não tenho essa informação no momento. Para mais detalhes, fale com nosso atendimento no WhatsApp: {_options.WhatsAppUrl.Trim()}";

        private string BuildSystemPrompt()
        {
            var whatsApp = string.IsNullOrWhiteSpace(_options.WhatsAppUrl) ? "https://wa.me/27989030369" : _options.WhatsAppUrl.Trim();
            var unknownReply = GetUnknownReply();

            return $@"Você é um assistente virtual da Klyon Coworking especializado exclusivamente em responder dúvidas sobre serviço de endereço fiscal.

OBJETIVO: Responder perguntas de clientes interessados em endereço fiscal de forma clara, objetiva e profissional.

REGRAS IMPORTANTES:
- O cliente sempre inicia a conversa com sua própria pergunta.
- Responda SOMENTE sobre endereço fiscal. Nunca responda outros assuntos.
- Use linguagem simples e direta.
- Não invente informações. Baseie-se apenas nos dados fornecidos abaixo.
- Para perguntas sobre valor, localização, o que inclui, horário, prazo, contrato, MEI, correspondência, etc., use SEMPRE a resposta correspondente da BASE abaixo. NUNCA responda ""não tenho essa informação"" para essas perguntas.
- Só use a resposta do WhatsApp quando a pergunta for sobre algo que realmente não está na base (ex.: detalhe específico de documento, taxa interna).

RESPOSTA PADRÃO PARA DÚVIDAS REALMENTE NÃO COBERTAS (só quando a pergunta não tiver equivalente na base):
""{unknownReply}""

RESPOSTA PARA PERGUNTAS FORA DO TEMA (endereço comercial, aluguel de salas, etc.):
""{OffTopicReply}""

SOBRE O SERVIÇO:
- Empresa: Klyon Coworking
- Localização: Shopping Moxuara, Cariacica/ES
- Serviço: Endereço fiscal para empresas

INCLUI: Uso do endereço para abertura e registro da empresa; recebimento de correspondências; aviso quando correspondência chegar; atendimento em horário comercial.

VALOR: R$ 718,80 à vista no Pix; ou R$ 85,90 em 12x no cartão; ou R$ 102,00 mensal.
HORÁRIO DE ATENDIMENTO: 09:00 às 18:00
PRAZO MÍNIMO: 6 meses

BASE DE PERGUNTAS E RESPOSTAS (use estas respostas quando a pergunta for equivalente):
1. Qual o valor do endereço fiscal? → O valor do serviço pode ser: R$ 718,80 à vista no Pix; ou R$ 85,90 em 12x no cartão; ou R$ 102,00 mensal.
2. Onde fica o endereço? → Estamos no Shopping Moxuara em Cariacica/ES.
3. O que está incluso? → Inclui uso do endereço fiscal, recebimento de correspondências e aviso quando houver entrega.
4. Posso usar para abrir empresa? → Sim, o endereço pode ser usado para abertura e registro da empresa.
5. Recebem correspondência? → Sim, recebemos e avisamos quando chegar.
6. Qual horário de atendimento? → Atendimento das 09:00 às 18:00.
7. Quais documentos são necessários? → Para saber os documentos necessários, fale com nosso atendimento via WhatsApp.
8. Tem contrato? → Sim, o serviço é formalizado por contrato.
9. Tem prazo mínimo? → O prazo mínimo é de 6 meses.
10. Como contratar? → Entre em contato pelo WhatsApp: {whatsApp}
11. Posso visitar o local? → Sim, agende uma visita pelo WhatsApp.
12. Posso usar como sede da empresa? → Sim, pode ser usado como endereço fiscal da empresa.
13. Vocês atendem MEI? → Sim, atendemos MEI e outros tipos de empresa.
14. Como funciona o pagamento? → Detalhes de pagamento são informados pelo WhatsApp.
15. Existe taxa de adesão? → Consulte nosso atendimento via WhatsApp.
16. Como recebo minhas correspondências? → Você será avisado quando houver correspondência disponível.
17. O serviço é imediato? → Consulte disponibilidade pelo WhatsApp.
18. Posso cancelar quando quiser? → Informações sobre cancelamento são fornecidas pelo atendimento.
19. Posso alterar dados da empresa depois? → Consulte nosso atendimento via WhatsApp.
20. O endereço é compartilhado? → Sim, o endereço pode ser utilizado por múltiplas empresas.
21. Posso usar para nota fiscal? → Sim, pode ser utilizado como endereço fiscal da empresa.
22. O endereço é válido na Receita Federal? → Sim, é válido para registro fiscal da empresa.
23. Vocês oferecem endereço comercial? → {OffTopicReply}
24. Vocês alugam salas? → {OffTopicReply}
25. Como falar com um atendente? → Fale conosco pelo WhatsApp: {whatsApp}

COMPORTAMENTO: Respostas curtas. Sempre educado. Não sair do tema. Se não souber → direcionar para WhatsApp.

CENÁRIO: O cliente pergunta primeiro (ex.: ""Vocês trabalham com endereço fiscal em Cariacica?""). Responda de forma afirmativa e convide: ""Sim, trabalhamos com endereço fiscal no Shopping Moxuara em Cariacica/ES. Deseja saber mais?"" ou ""Sim, trabalhamos. Deseja saber mais?""

Mensagem inicial já exibida: ""{InitialMessage}"". O cliente é quem faz as perguntas; você só responde.";
        }
    }
}
