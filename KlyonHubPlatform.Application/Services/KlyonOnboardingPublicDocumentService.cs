using KlyonHubPlatform.Application.DTOs.FiscalOnboarding;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class KlyonOnboardingPublicDocumentService : IKlyonOnboardingPublicDocumentService
    {
        private readonly ApplicationDbContext _context;

        public KlyonOnboardingPublicDocumentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<KlyonOnboardingPublicDocumentItem>?> GetVisibleDocumentsForClientAsync(int userId, string webRootPath, CancellationToken cancellationToken = default)
        {
            if (!await IsPaidFiscalOnboardingClientAsync(userId, cancellationToken))
                return null;

            var now = DateTime.UtcNow;
            var folder = Path.Combine(webRootPath, KlyonOnboardingPublicDocumentCatalog.WwwrootRelativeFolder);
            var list = new List<KlyonOnboardingPublicDocumentItem>();

            foreach (var def in KlyonOnboardingPublicDocumentCatalog.Definitions)
            {
                if (def.ValidUntilUtc.HasValue && now > def.ValidUntilUtc.Value)
                    continue;
                var fullPath = Path.Combine(folder, def.FileName);
                if (!File.Exists(fullPath))
                    continue;
                list.Add(new KlyonOnboardingPublicDocumentItem(def.Slug, def.Title, def.FileName));
            }

            return list;
        }

        public async Task<(string FullPath, string DownloadFileName)?> TryGetDocumentFileForClientAsync(int userId, string slug, string webRootPath, CancellationToken cancellationToken = default)
        {
            if (!await IsPaidFiscalOnboardingClientAsync(userId, cancellationToken))
                return null;

            var def = KlyonOnboardingPublicDocumentCatalog.TryGetBySlug(slug);
            if (def == null)
                return null;

            if (def.ValidUntilUtc.HasValue && DateTime.UtcNow > def.ValidUntilUtc.Value)
                return null;

            var fullPath = Path.Combine(webRootPath, KlyonOnboardingPublicDocumentCatalog.WwwrootRelativeFolder, def.FileName);
            if (!File.Exists(fullPath))
                return null;

            return (fullPath, def.FileName);
        }

        /// <summary>Cliente criado pelo fluxo de onboarding fiscal após confirmação do pagamento da adesão (CNPJ existente ou empresa nova/CPF).</summary>
        private async Task<bool> IsPaidFiscalOnboardingClientAsync(int userId, CancellationToken cancellationToken)
        {
            var status = await _context.Clients.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => c.FiscalOnboardingStatus)
                .FirstOrDefaultAsync(cancellationToken);

            if (status == null)
                return false;

            return status.Value >= ClientFiscalOnboardingStatus.PagamentoAdesaoConfirmado;
        }
    }
}
