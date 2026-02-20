using KlyonHubPlatform.Application.Interfaces;
using System.Text.RegularExpressions;
namespace KlyonHubPlatform.Application.Services
{
    public class AIIntentionService : IAIIntentionService
    {
        private readonly Dictionary<string, List<string>> _intentionKeywords = new()
        {
            { "cadastro_parceiro", new List<string> { "cadastr", "parceiro", "coworking", "registr", "inscrev", "como viro" } },
            { "cadastro_cliente", new List<string> { "cadastr", "cliente", "conta", "registr", "criar conta", "novo usuário" } },
            { "consultar_reserva", new List<string> { "reserva", "agendamento", "booking", "horário", "sala", "espaço" } },
            { "pagamento", new List<string> { "pagamento", "pagar", "boleto", "cartão", "pix", "cobrança", "fatura" } },
            { "endereco_fiscal", new List<string> { "endereço fiscal", "declaração", "fiscal", "domicílio", "sede" } },
            { "cancelamento", new List<string> { "cancelar", "desistir", "encerrar", "remover", "deletar" } },
            { "suporte_tecnico", new List<string> { "erro", "problema", "bug", "não funciona", "travou", "ajuda técnica" } },
            { "informacao_preco", new List<string> { "preço", "valor", "quanto custa", "plano", "taxa", "mensalidade" } },
            { "localizacao", new List<string> { "endereço", "onde fica", "localização", "como chegar", "mapa" } },
            { "horario_funcionamento", new List<string> { "horário", "abre", "fecha", "funcionamento", "atendimento" } },
            { "saudacao", new List<string> { "olá", "oi", "bom dia", "boa tarde", "boa noite", "hey", "hello" } },
            { "despedida", new List<string> { "tchau", "até logo", "obrigado", "valeu", "bye" } }
        };
        private readonly Dictionary<string, string> _intentionToCategory = new()
        {
            { "cadastro_parceiro", "Cadastro" },
            { "cadastro_cliente", "Cadastro" },
            { "consultar_reserva", "Reservas" },
            { "pagamento", "Pagamentos" },
            { "endereco_fiscal", "Endereço Fiscal" },
            { "cancelamento", "Suporte" },
            { "suporte_tecnico", "Suporte" },
            { "informacao_preco", "Informações" },
            { "localizacao", "Informações" },
            { "horario_funcionamento", "Informações" },
            { "saudacao", "Geral" },
            { "despedida", "Geral" }
        };
        public async Task<IntentionDetectionResult> DetectIntentionAsync(string message)
        {
            await Task.CompletedTask;
            var normalizedMessage = NormalizeText(message);
            var detectedIntentions = new Dictionary<string, decimal>();
            foreach (var intention in _intentionKeywords)
            {
                var score = 0m;
                var keywordCount = 0;
                foreach (var keyword in intention.Value)
                {
                    var normalizedKeyword = NormalizeText(keyword);
                    if (normalizedMessage.Contains(normalizedKeyword))
                    {
                        score += 1m;
                        keywordCount++;
                    }
                }
                if (keywordCount > 0)
                {
                    var confidence = Math.Min(score / 3m, 1m);
                    detectedIntentions[intention.Key] = confidence;
                }
            }
            if (detectedIntentions.Any())
            {
                var topIntention = detectedIntentions.OrderByDescending(x => x.Value).First();
                return new IntentionDetectionResult
                {
                    IntentionName = topIntention.Key,
                    Category = _intentionToCategory.GetValueOrDefault(topIntention.Key, "Geral"),
                    Confidence = topIntention.Value,
                    Entities = await ExtractEntitiesAsync(message)
                };
            }
            return new IntentionDetectionResult
            {
                IntentionName = "unknown",
                Category = "Geral",
                Confidence = 0.1m,
                Entities = await ExtractEntitiesAsync(message)
            };
        }
        public async Task<string> AnalyzeSentimentAsync(string message)
        {
            await Task.CompletedTask;
            var normalizedMessage = NormalizeText(message);
            var positiveWords = new[] { "obrigado", "agradeço", "excelente", "ótimo", "bom", "legal", "perfeito" };
            var positiveCount = positiveWords.Count(word => normalizedMessage.Contains(word));
            var negativeWords = new[] { "ruim", "péssimo", "problema", "erro", "não funciona", "insatisfeito", "horrível" };
            var negativeCount = negativeWords.Count(word => normalizedMessage.Contains(word));
            if (positiveCount > negativeCount)
                return "positive";
            else if (negativeCount > positiveCount)
                return "negative";
            else
                return "neutral";
        }
        public async Task<Dictionary<string, string>> ExtractEntitiesAsync(string message)
        {
            await Task.CompletedTask;
            var entities = new Dictionary<string, string>();
            var cpfPattern = @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}";
            var cnpjPattern = @"\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}";
            var cpfMatch = Regex.Match(message, cpfPattern);
            if (cpfMatch.Success)
                entities["cpf"] = cpfMatch.Value;
            var cnpjMatch = Regex.Match(message, cnpjPattern);
            if (cnpjMatch.Success)
                entities["cnpj"] = cnpjMatch.Value;
            var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
            var emailMatch = Regex.Match(message, emailPattern);
            if (emailMatch.Success)
                entities["email"] = emailMatch.Value;
            var phonePattern = @"(\(?\d{2}\)?\s?)?\d{4,5}-?\d{4}";
            var phoneMatch = Regex.Match(message, phonePattern);
            if (phoneMatch.Success)
                entities["phone"] = phoneMatch.Value;
            var datePattern = @"\d{1,2}\/\d{1,2}\/\d{2,4}";
            var dateMatch = Regex.Match(message, datePattern);
            if (dateMatch.Success)
                entities["date"] = dateMatch.Value;
            return entities;
        }
        public double CalculateSimilarity(string text1, string text2)
        {
            var normalized1 = NormalizeText(text1);
            var normalized2 = NormalizeText(text2);
            var distance = LevenshteinDistance(normalized1, normalized2);
            var maxLength = Math.Max(normalized1.Length, normalized2.Length);
            if (maxLength == 0)
                return 1.0;
            return 1.0 - ((double)distance / maxLength);
        }
        public async Task<string> SuggestCategoryAsync(string question)
        {
            var intention = await DetectIntentionAsync(question);
            return intention.Category;
        }
        #region Helper Methods
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            text = RemoveAccents(text);
            text = text.ToLowerInvariant();
            text = Regex.Replace(text, @"[^a-z0-9\s]", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }
        private string RemoveAccents(string text)
        {
            var withAccents = "áàãâäéèêëíìîïóòõôöúùûüçñÁÀÃÂÄÉÈÊËÍÌÎÏÓÒÕÔÖÚÙÛÜÇÑ";
            var withoutAccents = "aaaaaeeeeiiiiooooouuuucnAAAAAEEEEIIIIOOOOOUUUUCN";
            for (int i = 0; i < withAccents.Length; i++)
            {
                text = text.Replace(withAccents[i], withoutAccents[i]);
            }
            return text;
        }
        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;
            if (string.IsNullOrEmpty(target))
                return source.Length;
            int sourceLength = source.Length;
            int targetLength = target.Length;
            int[,] distance = new int[sourceLength + 1, targetLength + 1];
            for (int i = 0; i <= sourceLength; i++)
                distance[i, 0] = i;
            for (int j = 0; j <= targetLength; j++)
                distance[0, j] = j;
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }
            return distance[sourceLength, targetLength];
        }
        #endregion
    }
}