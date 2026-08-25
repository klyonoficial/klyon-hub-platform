using KlyonHubPlatform.Application.DTOs.Contracts;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractPartnerDocumentService : IContractPartnerDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;

        public ContractPartnerDocumentService(
            ApplicationDbContext context,
            StorageServiceFactory storageFactory)
        {
            _context = context;
            _storageFactory = storageFactory;
        }

        public async Task<IReadOnlyList<ContractPartnerDocumentDto>> ListAvailableForContractAsync(
            int contractId,
            int clientId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            var contract = await LoadAuthorizedContractAsync(contractId, clientId, isAdmin, cancellationToken);
            if (contract == null)
                return Array.Empty<ContractPartnerDocumentDto>();

            var coworkingId = contract.CoworkingAddress.CoworkingId;
            var now = DateTime.UtcNow;
            var docs = await QueryActivePartnerDocuments(coworkingId, now)
                .OrderBy(d => d.Type)
                .ToListAsync(cancellationToken);

            return docs
                .Select(d => new ContractPartnerDocumentDto
                {
                    DocumentId = d.Id,
                    Type = d.Type,
                    DisplayName = d.Type.ToDisplayString(),
                    FileName = d.FileName,
                    CreatedAt = d.CreatedAt
                })
                .ToList();
        }

        public async Task<ContractPartnerDocumentFileDto?> GetFileForContractAsync(
            int contractId,
            int documentId,
            int clientId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            var contract = await LoadAuthorizedContractAsync(contractId, clientId, isAdmin, cancellationToken);
            if (contract == null)
                return null;

            var now = DateTime.UtcNow;
            var document = await QueryActivePartnerDocuments(contract.CoworkingAddress.CoworkingId, now)
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document == null || string.IsNullOrWhiteSpace(document.FilePath))
                return null;

            var storage = await _storageFactory.GetStorageServiceAsync();
            var bytes = await storage.GetFileAsync(document.FilePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            return new ContractPartnerDocumentFileDto
            {
                Content = bytes,
                FileName = string.IsNullOrWhiteSpace(document.FileName)
                    ? $"{document.Type.ToApiKey()}.pdf"
                    : document.FileName,
                ContentType = ResolveContentType(document.FileName, document.FilePath)
            };
        }

        private async Task<Contract?> LoadAuthorizedContractAsync(
            int contractId,
            int clientId,
            bool isAdmin,
            CancellationToken cancellationToken)
        {
            if (contractId <= 0)
                return null;

            var query = _context.Contracts.AsNoTracking()
                .Include(c => c.CoworkingAddress)
                .Where(c => c.Id == contractId);

            if (!isAdmin)
            {
                if (clientId <= 0)
                    return null;
                query = query.Where(c => c.ClientId == clientId);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        private IQueryable<CoworkingDocument> QueryActivePartnerDocuments(int coworkingId, DateTime utcNow)
        {
            var types = CoworkingDocumentTypeExtensions.MandatoryProfileTypes;
            return _context.CoworkingDocuments.AsNoTracking()
                .Where(d => d.CoworkingId == coworkingId
                            && types.Contains(d.Type)
                            && d.IsValid
                            && d.Status == CoworkingDocumentStatus.Approved
                            && (d.ExpiresAt == null || d.ExpiresAt > utcNow)
                            && !string.IsNullOrWhiteSpace(d.FilePath));
        }

        private static string ResolveContentType(string fileName, string filePath)
        {
            var name = (fileName + " " + filePath).ToLowerInvariant();
            if (name.Contains(".png")) return "image/png";
            if (name.Contains(".jpg") || name.Contains(".jpeg")) return "image/jpeg";
            if (name.Contains(".webp")) return "image/webp";
            return "application/pdf";
        }
    }
}
