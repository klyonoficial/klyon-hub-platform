using DocumentModel = KlyonHubPlatform.Domain.Entities.Document;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<DocumentModel> GenerateContractDocumentAsync(int contractId);
        Task<DocumentModel> GenerateReceiptAsync(int paymentId);
        Task<DocumentModel> GenerateTaxDeclarationAsync(int contractId);
        Task<byte[]?> GetDocumentFileAsync(int documentId);
    }
}