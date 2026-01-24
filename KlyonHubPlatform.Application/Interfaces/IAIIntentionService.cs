using KlyonHubPlatform.Application.DTOs;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAIIntentionService
    {
        /// <summary>
        /// Detecta a intenção de uma mensagem do usuário
        /// </summary>
        Task<IntentionDetectionResult> DetectIntentionAsync(string message);

        /// <summary>
        /// Analisa o sentimento de uma mensagem
        /// </summary>
        Task<string> AnalyzeSentimentAsync(string message);

        /// <summary>
        /// Extrai entidades de uma mensagem
        /// </summary>
        Task<Dictionary<string, string>> ExtractEntitiesAsync(string message);

        /// <summary>
        /// Calcula similaridade entre duas strings (para matching de FAQ)
        /// </summary>
        double CalculateSimilarity(string text1, string text2);

        /// <summary>
        /// Sugere categoria para uma pergunta não respondida
        /// </summary>
        Task<string> SuggestCategoryAsync(string question);
    }

    public class IntentionDetectionResult
    {
        public string IntentionName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public Dictionary<string, string>? Entities { get; set; }
    }
}
