namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAIIntentionService
    {
        Task<IntentionDetectionResult> DetectIntentionAsync(string message);
        Task<string> AnalyzeSentimentAsync(string message);
        Task<Dictionary<string, string>> ExtractEntitiesAsync(string message);
        double CalculateSimilarity(string text1, string text2);
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