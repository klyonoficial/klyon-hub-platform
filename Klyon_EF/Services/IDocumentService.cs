using KlyonEF.Models;
using DocumentModel = KlyonEF.Models.Document;

namespace KlyonEF.Services
{
    public interface IDocumentService
    {
        Task<DocumentModel> GenerateContractDocumentAsync(int contractId);
        Task<DocumentModel> GenerateReceiptAsync(int paymentId);
        Task<DocumentModel> GenerateTaxDeclarationAsync(int contractId);
        Task<byte[]?> GetDocumentFileAsync(int documentId);
    }
}
