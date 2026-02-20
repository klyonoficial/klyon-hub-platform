namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    public class AsaasValidationResult
    {
        public bool IsValid { get; set; }
        public int? HttpStatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}