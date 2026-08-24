namespace KlyonHubPlatform.Application.DTOs.ContractDocuments
{
    public class ContractDocumentsEnsureResult
    {
        public bool DeclarationManualRetryAvailable { get; set; }
        public bool ContractManualRetryAvailable { get; set; }
        public string? DeclarationErrorMessage { get; set; }
        public string? ContractErrorMessage { get; set; }
    }
}
