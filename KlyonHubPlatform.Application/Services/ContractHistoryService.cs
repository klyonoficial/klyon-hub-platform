using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractHistoryService : IContractHistoryService
    {
        private readonly ApplicationDbContext _context;

        public ContractHistoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RecordDocumentsUploadedAsync(
            int contractId,
            int? actorUserId,
            IEnumerable<ClientOnboardingDocumentType> documentTypes,
            CancellationToken cancellationToken = default)
        {
            var typesText = string.Join(
                ", ",
                documentTypes
                    .Distinct()
                    .OrderBy(t => t.ToReviewOrder())
                    .Select(t => t.ToDisplayString()));
            if (string.IsNullOrWhiteSpace(typesText))
                typesText = "Identidade e comprovante de residência";

            await AddAsync(
                contractId,
                ContractHistoryActions.DocumentsUploaded,
                actorUserId,
                RoleNames.DisplayNames.Client,
                typesText,
                null,
                cancellationToken);
        }

        public async Task RecordProfileUpdatedForClientAsync(
            int clientId,
            int? actorUserId,
            CancellationToken cancellationToken = default)
        {
            var contractIds = await _context.Contracts
                .Where(c => c.ClientId == clientId
                    && c.Status != ContractStatus.Cancelled
                    && c.Status != ContractStatus.Expired)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            if (contractIds.Count == 0)
                return;

            var actorName = await ResolveActorNameAsync(actorUserId, cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var contractId in contractIds)
            {
                _context.ContractHistories.Add(new ContractHistory
                {
                    ContractId = contractId,
                    Action = ContractHistoryActions.ProfileUpdated,
                    Details = "Nome, endereço, telefone e CPF/CNPJ",
                    ActorUserId = actorUserId,
                    ActorName = actorName,
                    ActorRole = RoleNames.DisplayNames.Client,
                    ActionAt = now
                });
            }
        }

        public Task RecordDocumentsApprovedAsync(
            int contractId,
            int reviewerUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default) =>
            AddAsync(
                contractId,
                ContractHistoryActions.DocumentsApproved,
                reviewerUserId,
                ReviewerRole(isAdmin),
                null,
                null,
                cancellationToken);

        public Task RecordDocumentsRejectedAsync(
            int contractId,
            int reviewerUserId,
            bool isAdmin,
            string reason,
            CancellationToken cancellationToken = default) =>
            AddAsync(
                contractId,
                ContractHistoryActions.DocumentsRejected,
                reviewerUserId,
                ReviewerRole(isAdmin),
                null,
                reason,
                cancellationToken);

        public async Task RecordContractGeneratedAsync(
            int contractId,
            string contractNumber,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            var alreadyRecorded = await _context.ContractHistories
                .AsNoTracking()
                .AnyAsync(
                    h => h.ContractId == contractId && h.Action == ContractHistoryActions.ContractGenerated,
                    cancellationToken);
            if (alreadyRecorded)
                return;

            var details = string.IsNullOrWhiteSpace(description)
                ? $"Número {contractNumber}"
                : $"Número {contractNumber} · {description.Trim()}";
            await AddAsync(
                contractId,
                ContractHistoryActions.ContractGenerated,
                null,
                "Sistema",
                details,
                null,
                cancellationToken);
        }

        public async Task<List<ContractHistory>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.ContractHistories
                .AsNoTracking()
                .Where(h => h.ContractId == contractId)
                .OrderByDescending(h => h.ActionAt)
                .ToListAsync(cancellationToken);
        }

        private async Task AddAsync(
            int contractId,
            string action,
            int? actorUserId,
            string actorRole,
            string? details,
            string? reason,
            CancellationToken cancellationToken)
        {
            var actorName = await ResolveActorNameAsync(actorUserId, cancellationToken);
            _context.ContractHistories.Add(new ContractHistory
            {
                ContractId = contractId,
                Action = action,
                Details = details,
                RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                ActorUserId = actorUserId,
                ActorName = actorName,
                ActorRole = actorRole,
                ActionAt = DateTime.UtcNow
            });
        }

        private async Task<string?> ResolveActorNameAsync(int? actorUserId, CancellationToken cancellationToken)
        {
            if (!actorUserId.HasValue)
                return null;
            var name = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == actorUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string ReviewerRole(bool isAdmin) =>
            isAdmin ? RoleNames.DisplayNames.Administrator : RoleNames.DisplayNames.CoworkingPartner;
    }
}
