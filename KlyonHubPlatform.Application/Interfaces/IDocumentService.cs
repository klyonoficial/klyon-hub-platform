using DocumentModel = KlyonHubPlatform.Domain.Entities.Document;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<DocumentModel> GenerateContractDocumentAsync(int contractId, bool regenerate = false);
        Task<bool> CanGenerateContractDocumentAsync(int contractId);
        Task<bool> CanRegenerateContractDocumentAsync(int contractId);
        Task EnsureContractDocumentAsync(int contractId);
        Task<bool> CanGenerateFiscalContractDocumentAsync(int contractId);
        Task EnsureFiscalContractDocumentAsync(int contractId);
        Task<DocumentModel> GenerateReceiptAsync(int paymentId);
        Task<DocumentModel> GenerateTaxDeclarationAsync(int contractId);
        Task<byte[]?> GetDocumentFileAsync(int documentId);
    }
}