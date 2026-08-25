using KlyonHubPlatform.Application.DTOs.ClientOnboarding;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ClientOnboardingDocumentService : IClientOnboardingDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;
        private readonly IContractDocumentsWorkQueue _contractDocumentsWorkQueue;
        private readonly IContractHistoryService _contractHistoryService;
        private readonly IAuditService? _auditService;

        public ClientOnboardingDocumentService(
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            IContractDocumentsWorkQueue contractDocumentsWorkQueue,
            IContractHistoryService contractHistoryService,
            IAuditService? auditService = null)
        {
            _context = context;
            _storageFactory = storageFactory;
            _contractDocumentsWorkQueue = contractDocumentsWorkQueue;
            _contractHistoryService = contractHistoryService;
            _auditService = auditService;
        }

        public long MaxFileSizeBytes => ClientOnboardingDocumentRules.MaxFileSizeBytes;

        public bool IsAllowedContentType(string? contentType) =>
            ClientOnboardingDocumentRules.IsAllowedContentType(contentType);

        public async Task<ClientOnboardingPageResult> GetPageAsync(int contractId, int clientId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            var contract = await LoadContractAsync(contractId, cancellationToken);
            if (contract == null)
                return Fail("Contrato não encontrado.");
            if (!isAdmin && contract.ClientId != clientId)
                return Fail("Contrato não encontrado ou não pertence a você.");
            if (!await HasPaidPaymentAsync(contract, cancellationToken))
                return Fail("O pagamento deste contrato ainda não foi confirmado.");

            var documents = await _context.ClientOnboardingDocuments
                .AsNoTracking()
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(cancellationToken);

            var status = ClientOnboardingDocumentRules.ResolveSetStatus(contract, documents);
            var rejectionReason = contract.DocumentsRejectionReason
                ?? documents.Where(d => d.Status == ClientOnboardingDocumentStatus.Rejected && !string.IsNullOrWhiteSpace(d.RejectionReason))
                    .OrderByDescending(d => d.ReviewedAt)
                    .Select(d => d.RejectionReason)
                    .FirstOrDefault();
            var gapMessage = ClientOnboardingDocumentRules.GetSetGapMessage(documents);
            return new ClientOnboardingPageResult
            {
                Success = true,
                Contract = contract,
                Documents = documents,
                SetStatus = status,
                CanUpload = status != ClientOnboardingSetStatus.Approved && status != ClientOnboardingSetStatus.AwaitingApproval,
                CanViewGeneratedDocuments = contract.DocumentsApproved,
                IdentityPath = ClientOnboardingDocumentRules.InferIdentityPath(documents),
                IdentityGapMessage = gapMessage,
                RejectionReason = rejectionReason,
                Banner = ClientOnboardingDocumentRules.GetClientBanner(status, rejectionReason, gapMessage)
            };
        }

        public async Task<ClientOnboardingCommandResult> UploadSetAsync(ClientOnboardingSetUploadInput input, CancellationToken cancellationToken = default)
        {
            var gate = await EnsureCanUploadAsync(input.ContractId, input.ClientId, cancellationToken);
            if (gate.Error != null)
                return CommandFail(gate.Error);

            var latest = ClientOnboardingDocumentRules.LatestByType(gate.Existing);
            var setError = ClientOnboardingDocumentRules.ValidateSetSubmission(
                input.Path,
                ClientOnboardingDocumentRules.HasFile(input.RgFront),
                ClientOnboardingDocumentRules.HasFile(input.RgBack),
                ClientOnboardingDocumentRules.HasFile(input.Cpf),
                ClientOnboardingDocumentRules.HasFile(input.Cnh),
                ClientOnboardingDocumentRules.HasFile(input.ProofOfResidence),
                IsUsableExisting(latest, ClientOnboardingDocumentType.IdentityRgFront),
                IsUsableExisting(latest, ClientOnboardingDocumentType.IdentityRgBack),
                IsUsableExisting(latest, ClientOnboardingDocumentType.IdentityCpf),
                IsUsableExisting(latest, ClientOnboardingDocumentType.IdentityCnh),
                IsUsableExisting(latest, ClientOnboardingDocumentType.ProofOfResidence));
            if (setError != null)
                return CommandFail(setError);

            var filesToSave = FilesForSet(input);
            var totalError = ClientOnboardingDocumentRules.ValidateTotalSize(filesToSave.Select(f => (long)(f.Content?.Length ?? 0)));
            if (totalError != null)
                return CommandFail(totalError);

            foreach (var file in filesToSave)
            {
                var fileError = ClientOnboardingDocumentRules.ValidateFile(file.Content, file.FileName, file.ContentType);
                if (fileError != null)
                    return CommandFail($"{file.Type.ToDisplayString()}: {fileError}");
            }

            var storage = await _storageFactory.GetStorageServiceAsync();
            foreach (var file in filesToSave)
                await ReplaceDocumentAsync(storage, gate.Existing, file);

            await RemoveIdentityTypesAsync(
                storage,
                gate.Existing,
                ClientOnboardingDocumentRules.OppositeIdentityTypes(input.Path));

            if (!ClientOnboardingDocumentRules.HasRequiredSet(gate.Existing))
                return CommandFail("Envie o conjunto completo: identidade (CNH, ou frente e verso do RG e o CPF) e o comprovante de residência.");

            var uploadedTypes = ClientOnboardingDocumentRules.LatestByType(gate.Existing)
                .Values
                .Where(d => d.Status != ClientOnboardingDocumentStatus.Rejected)
                .Select(d => d.Type)
                .ToList();
            await _contractHistoryService.RecordDocumentsUploadedAsync(
                input.ContractId,
                gate.Contract!.Client?.UserId,
                uploadedTypes,
                cancellationToken);
            await MarkContractAwaitingReviewAsync(gate.Contract!, cancellationToken);
            return new ClientOnboardingCommandResult
            {
                Success = true,
                SuccessMessage = "Documentos enviados com sucesso. Aguarde a análise do parceiro ou da Klyon."
            };
        }

        public async Task<ClientOnboardingFileResult?> GetFileAsync(int documentId, int? clientId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            var document = await _context.ClientOnboardingDocuments
                .Include(d => d.Contract)
                    .ThenInclude(c => c.CoworkingAddress)
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document == null)
                return null;

            var allowed = isAdmin
                || (clientId.HasValue && document.ClientId == clientId.Value)
                || (coworkingId.HasValue && document.Contract.CoworkingAddress.CoworkingId == coworkingId.Value);
            if (!allowed)
                return null;

            var storage = await _storageFactory.GetStorageServiceAsync();
            var bytes = await storage.GetFileAsync(document.FilePath);
            if (bytes == null)
                return null;

            return new ClientOnboardingFileResult
            {
                Content = bytes,
                FileName = document.FileName,
                ContentType = string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType
            };
        }

        public async Task<List<ClientOnboardingReviewItem>> ListPendingForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            var contracts = await QueryReviewContracts()
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingId)
                .ToListAsync(cancellationToken);
            return MapReviewItems(contracts);
        }

        public async Task<List<ClientOnboardingReviewItem>> ListPendingForAdminAsync(CancellationToken cancellationToken = default)
        {
            var contracts = await QueryReviewContracts().ToListAsync(cancellationToken);
            return MapReviewItems(contracts);
        }

        public async Task<ClientOnboardingPageResult> GetReviewPageAsync(int contractId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            var contract = await LoadContractAsync(contractId, cancellationToken);
            if (contract == null)
                return Fail("Contrato não encontrado.");
            if (!isAdmin && (!coworkingId.HasValue || contract.CoworkingAddress.CoworkingId != coworkingId.Value))
                return Fail("Contrato não encontrado.");

            var documents = await _context.ClientOnboardingDocuments
                .Include(d => d.ReviewedByUser)
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(cancellationToken);

            var status = ClientOnboardingDocumentRules.ResolveSetStatus(contract, documents);
            var gapMessage = ClientOnboardingDocumentRules.GetSetGapMessage(documents);
            var rejectionReason = contract.DocumentsRejectionReason;
            return new ClientOnboardingPageResult
            {
                Success = true,
                Contract = contract,
                Documents = documents,
                SetStatus = status,
                CanUpload = false,
                CanViewGeneratedDocuments = contract.DocumentsApproved,
                IdentityPath = ClientOnboardingDocumentRules.InferIdentityPath(documents),
                IdentityGapMessage = gapMessage,
                RejectionReason = rejectionReason,
                Banner = ClientOnboardingDocumentRules.GetClientBanner(status, rejectionReason, gapMessage)
            };
        }

        public async Task<ClientOnboardingCommandResult> ApproveAsync(int contractId, int reviewerUserId, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            var contract = await LoadContractTrackedAsync(contractId, cancellationToken);
            if (contract == null)
                return CommandFail("Contrato não encontrado.");
            if (!isAdmin && (!coworkingId.HasValue || contract.CoworkingAddress.CoworkingId != coworkingId.Value))
                return CommandFail("Contrato não encontrado.");
            if (contract.DocumentsApproved)
                return new ClientOnboardingCommandResult { Success = true, SuccessMessage = "Documentos já estavam aprovados." };
            if (!await HasPaidPaymentAsync(contract, cancellationToken))
                return CommandFail("O pagamento deste contrato ainda não foi confirmado.");

            var documents = await _context.ClientOnboardingDocuments
                .Where(d => d.ContractId == contractId)
                .ToListAsync(cancellationToken);
            if (!ClientOnboardingDocumentRules.HasRequiredSet(documents.Where(d => d.Status != ClientOnboardingDocumentStatus.Rejected)))
                return CommandFail("O cliente ainda não enviou a identidade completa (frente e verso do RG e o CPF, ou somente a CNH) e o comprovante de residência.");

            var now = DateTime.UtcNow;
            foreach (var doc in documents.Where(d => d.Status != ClientOnboardingDocumentStatus.Rejected))
            {
                doc.Status = ClientOnboardingDocumentStatus.Approved;
                doc.ReviewedByUserId = reviewerUserId;
                doc.ReviewedAt = now;
                doc.RejectionReason = null;
                doc.UpdatedAt = now;
            }

            contract.DocumentsApproved = true;
            contract.DocumentsReviewedByUserId = reviewerUserId;
            contract.DocumentsReviewedAt = now;
            contract.DocumentsRejectionReason = null;
            contract.UpdatedAt = now;
            await _contractHistoryService.RecordDocumentsApprovedAsync(contract.Id, reviewerUserId, isAdmin, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    reviewerUserId,
                    "Documentos de onboarding aprovados",
                    "Contract",
                    contract.Id,
                    null,
                    System.Text.Json.JsonSerializer.Serialize(new { DocumentsApproved = true }),
                    cancellationToken: cancellationToken);
            }

            var party = ClientContractPartyRules.Evaluate(contract.Client);
            if (!party.IsComplete)
                return new ClientOnboardingCommandResult
                {
                    Success = true,
                    SuccessMessage = "Documentos aprovados. Complete os dados do contratante em Dados para o contrato para gerar o PDF."
                };

            await _contractDocumentsWorkQueue.EnqueueAsync(contract.Id, cancellationToken);
            return new ClientOnboardingCommandResult { Success = true, SuccessMessage = "Documentos aprovados. Contrato e declaração serão gerados em segundo plano." };
        }

        public async Task<ClientOnboardingCommandResult> RejectAsync(int contractId, int reviewerUserId, string reason, int? coworkingId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return CommandFail("Informe o motivo da rejeição.");

            var contract = await LoadContractTrackedAsync(contractId, cancellationToken);
            if (contract == null)
                return CommandFail("Contrato não encontrado.");
            if (!isAdmin && (!coworkingId.HasValue || contract.CoworkingAddress.CoworkingId != coworkingId.Value))
                return CommandFail("Contrato não encontrado.");

            var documents = await _context.ClientOnboardingDocuments
                .Where(d => d.ContractId == contractId)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var doc in documents.Where(d => d.Status == ClientOnboardingDocumentStatus.Pending))
            {
                doc.Status = ClientOnboardingDocumentStatus.Rejected;
                doc.ReviewedByUserId = reviewerUserId;
                doc.ReviewedAt = now;
                doc.RejectionReason = reason.Trim();
                doc.UpdatedAt = now;
            }

            contract.DocumentsApproved = false;
            contract.DocumentsReviewedByUserId = reviewerUserId;
            contract.DocumentsReviewedAt = now;
            contract.DocumentsRejectionReason = reason.Trim();
            contract.UpdatedAt = now;
            await _contractHistoryService.RecordDocumentsRejectedAsync(contract.Id, reviewerUserId, isAdmin, reason, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    reviewerUserId,
                    "Documentos de onboarding rejeitados",
                    "Contract",
                    contract.Id,
                    null,
                    System.Text.Json.JsonSerializer.Serialize(new { Reason = reason.Trim() }),
                    cancellationToken: cancellationToken);
            }

            return new ClientOnboardingCommandResult { Success = true, SuccessMessage = "Documentos rejeitados. O cliente poderá reenviar." };
        }

        public async Task<int> CountPendingForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            var items = await ListPendingForCoworkingAsync(coworkingId, cancellationToken);
            return items.Count(i => i.SetStatus == ClientOnboardingSetStatus.AwaitingApproval);
        }

        public async Task<int> CountPendingForAdminAsync(CancellationToken cancellationToken = default)
        {
            var items = await ListPendingForAdminAsync(cancellationToken);
            return items.Count(i => i.SetStatus == ClientOnboardingSetStatus.AwaitingApproval);
        }

        private IQueryable<Contract> QueryReviewContracts()
        {
            return _context.Contracts
                .AsNoTracking()
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(c => c.OnboardingDocuments)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(c => !c.DocumentsApproved
                            && c.Status == ContractStatus.Active
                            && c.OnboardingDocuments.Any());
        }

        private static List<ClientOnboardingReviewItem> MapReviewItems(List<Contract> contracts)
        {
            return contracts.Select(c =>
            {
                var status = ClientOnboardingDocumentRules.ResolveSetStatus(c, c.OnboardingDocuments);
                return new ClientOnboardingReviewItem
                {
                    Contract = c,
                    SetStatus = status,
                    PendingCount = c.OnboardingDocuments.Count(d => d.Status == ClientOnboardingDocumentStatus.Pending),
                    RejectedCount = c.OnboardingDocuments.Count(d => d.Status == ClientOnboardingDocumentStatus.Rejected),
                    LastUploadAt = c.OnboardingDocuments.Max(d => (DateTime?)d.CreatedAt)
                };
            })
            .Where(i => i.SetStatus == ClientOnboardingSetStatus.AwaitingApproval || i.SetStatus == ClientOnboardingSetStatus.Rejected)
            .OrderByDescending(i => i.LastUploadAt)
            .ToList();
        }

        private async Task<Contract?> LoadContractAsync(int contractId, CancellationToken cancellationToken) =>
            await _context.Contracts.AsNoTracking()
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(c => c.Service)
                .Include(c => c.Subscription)
                .Include(c => c.DocumentsReviewedByUser)
                .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        private async Task<Contract?> LoadContractTrackedAsync(int contractId, CancellationToken cancellationToken) =>
            await _context.Contracts
                .Include(c => c.Client)
                    .ThenInclude(cl => cl!.User)
                .Include(c => c.CoworkingAddress)
                .Include(c => c.Subscription)
                .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        private async Task<bool> HasPaidPaymentAsync(Contract contract, CancellationToken cancellationToken)
        {
            if (contract.Subscription == null)
                return false;
            return await _context.Payments.AnyAsync(
                p => p.SubscriptionId == contract.Subscription.Id && p.Status == PaymentStatus.Paid,
                cancellationToken);
        }

        private async Task<(Contract? Contract, List<ClientOnboardingDocument> Existing, string? Error)> EnsureCanUploadAsync(
            int contractId,
            int clientId,
            CancellationToken cancellationToken)
        {
            var contract = await LoadContractTrackedAsync(contractId, cancellationToken);
            if (contract == null || contract.ClientId != clientId)
                return (null, new List<ClientOnboardingDocument>(), "Contrato não encontrado ou não pertence a você.");
            if (contract.DocumentsApproved)
                return (null, new List<ClientOnboardingDocument>(), "Os documentos deste contrato já foram aprovados.");
            if (!await HasPaidPaymentAsync(contract, cancellationToken))
                return (null, new List<ClientOnboardingDocument>(), "O pagamento deste contrato ainda não foi confirmado.");

            var existing = await _context.ClientOnboardingDocuments
                .Where(d => d.ContractId == contractId)
                .ToListAsync(cancellationToken);
            var status = ClientOnboardingDocumentRules.ResolveSetStatus(contract, existing);
            if (status == ClientOnboardingSetStatus.AwaitingApproval)
                return (contract, existing, "Os documentos já foram enviados e aguardam aprovação.");

            return (contract, existing, null);
        }

        private async Task ReplaceDocumentAsync(
            IStorageService storage,
            List<ClientOnboardingDocument> existing,
            ClientOnboardingUploadInput input)
        {
            var contentType = ClientOnboardingDocumentRules.ResolveContentType(input.FileName, input.ContentType);
            var extension = Path.GetExtension(input.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = contentType == "application/pdf" ? ".pdf" : contentType == "image/png" ? ".png" : ".jpg";
            var storedName = $"{input.Type.ToString().ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var folder = $"client-onboarding/{input.ClientId}/{input.ContractId}";
            var filePath = await storage.SaveFileAsync(input.Content, storedName, folder);

            var previous = existing
                .Where(d => d.Type == input.Type)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();
            foreach (var doc in previous)
            {
                if (!string.IsNullOrWhiteSpace(doc.FilePath))
                {
                    try { await storage.DeleteFileAsync(doc.FilePath); } catch { /* ignore storage cleanup */ }
                }
                _context.ClientOnboardingDocuments.Remove(doc);
                existing.Remove(doc);
            }

            var created = new ClientOnboardingDocument
            {
                ClientId = input.ClientId,
                ContractId = input.ContractId,
                Type = input.Type,
                FileName = input.FileName,
                FilePath = filePath,
                ContentType = contentType,
                Status = ClientOnboardingDocumentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.ClientOnboardingDocuments.Add(created);
            existing.Add(created);
        }

        private async Task RemoveIdentityTypesAsync(
            IStorageService storage,
            List<ClientOnboardingDocument> existing,
            IEnumerable<ClientOnboardingDocumentType> types)
        {
            var typeSet = types.ToHashSet();
            if (typeSet.Count == 0)
                return;

            var toRemove = existing.Where(d => typeSet.Contains(d.Type)).ToList();
            foreach (var doc in toRemove)
            {
                if (!string.IsNullOrWhiteSpace(doc.FilePath))
                {
                    try { await storage.DeleteFileAsync(doc.FilePath); } catch { /* ignore storage cleanup */ }
                }
                _context.ClientOnboardingDocuments.Remove(doc);
                existing.Remove(doc);
            }
        }

        private async Task MarkContractAwaitingReviewAsync(Contract contract, CancellationToken cancellationToken)
        {
            contract.DocumentsApproved = false;
            contract.DocumentsRejectionReason = null;
            contract.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static List<ClientOnboardingUploadInput> FilesForSet(ClientOnboardingSetUploadInput input)
        {
            var files = new List<ClientOnboardingUploadInput>();
            if (input.Path == ClientOnboardingIdentityPath.Cnh)
            {
                AddFile(files, input.Cnh, input, ClientOnboardingDocumentType.IdentityCnh);
            }
            else
            {
                AddFile(files, input.RgFront, input, ClientOnboardingDocumentType.IdentityRgFront);
                AddFile(files, input.RgBack, input, ClientOnboardingDocumentType.IdentityRgBack);
                AddFile(files, input.Cpf, input, ClientOnboardingDocumentType.IdentityCpf);
            }

            AddFile(files, input.ProofOfResidence, input, ClientOnboardingDocumentType.ProofOfResidence);
            return files;
        }

        private static void AddFile(
            List<ClientOnboardingUploadInput> files,
            ClientOnboardingUploadInput? file,
            ClientOnboardingSetUploadInput input,
            ClientOnboardingDocumentType type)
        {
            if (!ClientOnboardingDocumentRules.HasFile(file))
                return;
            file!.ContractId = input.ContractId;
            file.ClientId = input.ClientId;
            file.Type = type;
            files.Add(file);
        }

        private static bool IsUsableExisting(
            IReadOnlyDictionary<ClientOnboardingDocumentType, ClientOnboardingDocument> latest,
            ClientOnboardingDocumentType type) =>
            latest.TryGetValue(type, out var doc) && doc.Status != ClientOnboardingDocumentStatus.Rejected;

        private static ClientOnboardingPageResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        private static ClientOnboardingCommandResult CommandFail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
}
