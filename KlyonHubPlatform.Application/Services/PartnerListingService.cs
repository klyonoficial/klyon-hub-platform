using KlyonHubPlatform.Application.DTOs.PartnerListing;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class PartnerListingService : IPartnerListingService
    {
        private static readonly CoworkingDocumentType[] PublicTrustTypes =
        {
            CoworkingDocumentType.OperatingLicense,
            CoworkingDocumentType.FireDepartmentLicense,
            CoworkingDocumentType.CNPJCard
        };

        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;
        private readonly ICoworkingService _coworkingService;

        public PartnerListingService(
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            ICoworkingService coworkingService)
        {
            _context = context;
            _storageFactory = storageFactory;
            _coworkingService = coworkingService;
        }

        public async Task<PartnerListingResult?> GetPartnerListingAsync(int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            if (coworkingAddressId <= 0)
                return null;

            var address = await _context.CoworkingAddresses.AsNoTracking()
                .Include(a => a.Coworking)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == coworkingAddressId && a.IsAvailable, cancellationToken);
            if (address == null)
                return null;

            var photos = await _context.CoworkingPhotos.AsNoTracking()
                .Where(p => p.CoworkingId == address.CoworkingId)
                .OrderByDescending(p => p.IsMain)
                .ThenBy(p => p.DisplayOrder)
                .ToListAsync(cancellationToken);

            var approvedDocs = await _context.CoworkingDocuments.AsNoTracking()
                .Where(d => d.CoworkingId == address.CoworkingId
                            && PublicTrustTypes.Contains(d.Type)
                            && d.IsValid
                            && d.Status == CoworkingDocumentStatus.Approved)
                .ToListAsync(cancellationToken);

            var trust = PublicTrustTypes.Select(type =>
            {
                var doc = approvedDocs.FirstOrDefault(d => d.Type == type);
                return new PartnerTrustDocumentDto
                {
                    Type = type,
                    DisplayName = type.ToDisplayString(),
                    IsAvailable = doc != null,
                    DocumentId = doc?.Id
                };
            }).ToList();

            var published = await _coworkingService.GetAvailableAddressesAsync(null, cancellationToken);

            return new PartnerListingResult
            {
                Address = address,
                Photos = photos,
                TrustDocuments = trust,
                IsSolePublishedAddress = published.Count == 1 && published[0].Id == address.Id
            };
        }

        public async Task<PartnerTrustDocumentFile?> GetPublicTrustDocumentAsync(
            int coworkingAddressId,
            CoworkingDocumentType type,
            CancellationToken cancellationToken = default)
        {
            if (!type.IsPublicTrustDocument())
                return null;

            var address = await _context.CoworkingAddresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == coworkingAddressId && a.IsAvailable, cancellationToken);
            if (address == null)
                return null;

            var document = await _context.CoworkingDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.CoworkingId == address.CoworkingId
                    && d.Type == type
                    && d.IsValid
                    && d.Status == CoworkingDocumentStatus.Approved,
                    cancellationToken);
            if (document == null || string.IsNullOrWhiteSpace(document.FilePath))
                return null;

            var storage = await _storageFactory.GetStorageServiceAsync();
            var bytes = await storage.GetFileAsync(document.FilePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            var contentType = ResolveContentType(document.FileName, document.FilePath);
            return new PartnerTrustDocumentFile
            {
                Content = bytes,
                FileName = string.IsNullOrWhiteSpace(document.FileName) ? $"{type.ToApiKey()}.pdf" : document.FileName,
                ContentType = contentType
            };
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
