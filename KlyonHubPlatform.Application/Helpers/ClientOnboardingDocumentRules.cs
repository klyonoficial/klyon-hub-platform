using KlyonHubPlatform.Application.DTOs.ClientOnboarding;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class ClientOnboardingDocumentRules
    {
        public const int MaxTotalSetSizeMb = 15;
        public const long MaxTotalSetSizeBytes = MaxTotalSetSizeMb * 1024L * 1024L;
        public const long MaxFileSizeBytes = MaxTotalSetSizeBytes;

        public const string FileTooLargeMessage = "O arquivo excede o tamanho máximo de 15 MB.";
        public const string TotalTooLargeMessage =
            "O envio excede 15 MB no total (identidade + comprovante). Reduza o tamanho dos arquivos e tente novamente.";

        public static readonly string[] AllowedContentTypes =
        {
            "application/pdf",
            "image/jpeg",
            "image/jpg",
            "image/png"
        };

        public static readonly ClientOnboardingDocumentType[] RgAndCpfTypes =
        {
            ClientOnboardingDocumentType.IdentityRgFront,
            ClientOnboardingDocumentType.IdentityRgBack,
            ClientOnboardingDocumentType.IdentityCpf
        };

        public const string IncompleteSetClientMessage =
            "Envie a frente e o verso do RG e o CPF, ou somente a CNH, e o comprovante de residência. O contrato PDF e a declaração só ficam disponíveis após a aprovação.";

        public const string GeneratedDocsAfterApprovalMessage =
            "O contrato PDF e a declaração só ficam disponíveis após a aprovação.";

        public static bool IsAllowedContentType(string? contentType) =>
            !string.IsNullOrWhiteSpace(contentType)
            && AllowedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase);

        public static string ResolveContentType(string fileName, string? uploadedContentType)
        {
            if (IsAllowedContentType(uploadedContentType))
                return uploadedContentType!.Trim();

            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => uploadedContentType ?? "application/octet-stream"
            };
        }

        public static string? ValidateFile(byte[]? content, string? fileName, string? contentType)
        {
            if (content == null || content.Length == 0)
                return "Selecione um arquivo.";
            if (content.Length > MaxFileSizeBytes)
                return FileTooLargeMessage;
            if (!IsAllowedContentType(ResolveContentType(fileName ?? string.Empty, contentType)))
                return "Envie um arquivo PDF, JPG ou PNG.";
            return null;
        }

        public static string? ValidateTotalSize(IEnumerable<long> fileSizes)
        {
            long total = 0;
            foreach (var size in fileSizes)
                total += size;
            if (total > MaxTotalSetSizeBytes)
                return TotalTooLargeMessage;
            return null;
        }

        public static bool HasFile(ClientOnboardingUploadInput? input) =>
            input?.Content != null && input.Content.Length > 0;

        /// <summary>
        /// Identidade completa: CNH sozinha, ou frente e verso do RG e o CPF juntos.
        /// </summary>
        public static bool HasIdentityCoverage(IEnumerable<ClientOnboardingDocument> documents)
        {
            var latest = LatestByType(documents);
            return IsUsable(latest, ClientOnboardingDocumentType.IdentityCnh)
                || HasUsableRgAndCpf(latest);
        }

        public static bool HasProofOfResidence(IEnumerable<ClientOnboardingDocument> documents)
        {
            var latest = LatestByType(documents);
            return IsUsable(latest, ClientOnboardingDocumentType.ProofOfResidence);
        }

        public static bool HasRequiredSet(IEnumerable<ClientOnboardingDocument> documents) =>
            HasIdentityCoverage(documents) && HasProofOfResidence(documents);

        public static ClientOnboardingIdentityPath InferIdentityPath(IEnumerable<ClientOnboardingDocument> documents)
        {
            var latest = LatestByType(documents);
            var hasCnh = IsUsable(latest, ClientOnboardingDocumentType.IdentityCnh);
            var hasRgFront = IsUsable(latest, ClientOnboardingDocumentType.IdentityRgFront);
            var hasRgBack = IsUsable(latest, ClientOnboardingDocumentType.IdentityRgBack);
            var hasCpf = IsUsable(latest, ClientOnboardingDocumentType.IdentityCpf);
            if (hasCnh)
                return ClientOnboardingIdentityPath.Cnh;
            if (hasRgFront || hasRgBack || hasCpf)
                return ClientOnboardingIdentityPath.RgAndCpf;
            return ClientOnboardingIdentityPath.RgAndCpf;
        }

        public static string? GetIdentityGapMessage(IEnumerable<ClientOnboardingDocument> documents)
        {
            if (HasIdentityCoverage(documents))
                return null;

            var latest = LatestByType(documents);
            var hasRgFront = IsUsable(latest, ClientOnboardingDocumentType.IdentityRgFront);
            var hasRgBack = IsUsable(latest, ClientOnboardingDocumentType.IdentityRgBack);
            var hasCpf = IsUsable(latest, ClientOnboardingDocumentType.IdentityCpf);
            if (!hasRgFront && !hasRgBack && !hasCpf)
                return "Envie a frente e o verso do RG e o CPF, ou somente a CNH.";
            if (!hasRgFront)
                return "Envie a frente do RG";
            if (!hasRgBack)
                return "Envie o verso do RG";
            if (!hasCpf)
                return "Falta enviar o CPF. Frente e verso do RG e o CPF são obrigatórios juntos.";
            return "Envie a frente e o verso do RG e o CPF, ou somente a CNH.";
        }

        public static string? GetSetGapMessage(IEnumerable<ClientOnboardingDocument> documents)
        {
            var identityGap = GetIdentityGapMessage(documents);
            var hasProof = HasProofOfResidence(documents);
            if (identityGap == null && hasProof)
                return null;
            if (identityGap != null && !hasProof)
                return identityGap.TrimEnd('.') + ". Envie também o comprovante de residência.";
            if (identityGap != null)
                return identityGap;
            return "Envie o comprovante de residência. Ele é obrigatório junto com a identidade.";
        }

        public static string? ValidateIdentitySubmission(
            ClientOnboardingIdentityPath path,
            bool hasRgFrontFile,
            bool hasRgBackFile,
            bool hasCpfFile,
            bool hasCnhFile,
            bool hasExistingUsableRgFront,
            bool hasExistingUsableRgBack,
            bool hasExistingUsableCpf,
            bool hasExistingUsableCnh)
        {
            if (path == ClientOnboardingIdentityPath.Cnh)
            {
                if (!hasCnhFile && !hasExistingUsableCnh)
                    return "Envie a CNH. Ela substitui o RG e o CPF.";
                return null;
            }

            var rgFrontOk = hasRgFrontFile || hasExistingUsableRgFront;
            var rgBackOk = hasRgBackFile || hasExistingUsableRgBack;
            var cpfOk = hasCpfFile || hasExistingUsableCpf;
            if (!rgFrontOk && !rgBackOk && !cpfOk)
                return "Envie a frente e o verso do RG e o CPF. Os três arquivos são obrigatórios.";
            if (!rgFrontOk)
                return "Envie a frente do RG";
            if (!rgBackOk)
                return "Envie o verso do RG";
            if (!cpfOk)
                return "Envie também o CPF. Frente e verso do RG e o CPF são obrigatórios juntos.";
            return null;
        }

        public static string? ValidateSetSubmission(
            ClientOnboardingIdentityPath path,
            bool hasRgFrontFile,
            bool hasRgBackFile,
            bool hasCpfFile,
            bool hasCnhFile,
            bool hasProofFile,
            bool hasExistingUsableRgFront,
            bool hasExistingUsableRgBack,
            bool hasExistingUsableCpf,
            bool hasExistingUsableCnh,
            bool hasExistingUsableProof)
        {
            var identityError = ValidateIdentitySubmission(
                path,
                hasRgFrontFile,
                hasRgBackFile,
                hasCpfFile,
                hasCnhFile,
                hasExistingUsableRgFront,
                hasExistingUsableRgBack,
                hasExistingUsableCpf,
                hasExistingUsableCnh);
            if (identityError != null)
                return identityError;
            if (!hasProofFile && !hasExistingUsableProof)
                return "Envie o comprovante de residência. Ele é obrigatório junto com a identidade.";
            return null;
        }

        public static ClientOnboardingDocumentType[] OppositeIdentityTypes(ClientOnboardingIdentityPath path) =>
            path == ClientOnboardingIdentityPath.Cnh
                ? RgAndCpfTypes
                : new[] { ClientOnboardingDocumentType.IdentityCnh };

        public static ClientOnboardingDocumentType[] OppositeIdentityTypes(ClientOnboardingDocumentType uploadedType)
        {
            if (uploadedType == ClientOnboardingDocumentType.IdentityCnh)
                return RgAndCpfTypes;
            if (uploadedType is ClientOnboardingDocumentType.IdentityRgFront
                or ClientOnboardingDocumentType.IdentityRgBack
                or ClientOnboardingDocumentType.IdentityCpf)
                return new[] { ClientOnboardingDocumentType.IdentityCnh };
            return Array.Empty<ClientOnboardingDocumentType>();
        }

        public static ClientOnboardingClientBanner GetClientBanner(
            ClientOnboardingSetStatus status,
            string? rejectionReason,
            string? gapMessage)
        {
            switch (status)
            {
                case ClientOnboardingSetStatus.Approved:
                    return new ClientOnboardingClientBanner
                    {
                        SetStatus = status,
                        Visible = false
                    };
                case ClientOnboardingSetStatus.AwaitingApproval:
                    return new ClientOnboardingClientBanner
                    {
                        SetStatus = status,
                        Tone = ClientOnboardingBannerTone.Info,
                        Visible = true,
                        Title = "Documentação em análise.",
                        Message = "Seus documentos foram enviados e estão aguardando aprovação do parceiro ou da Klyon. " + GeneratedDocsAfterApprovalMessage,
                        ShowActionButton = true,
                        ActionButtonLabel = "Acompanhar análise"
                    };
                case ClientOnboardingSetStatus.Rejected:
                    return new ClientOnboardingClientBanner
                    {
                        SetStatus = status,
                        Tone = ClientOnboardingBannerTone.Danger,
                        Visible = true,
                        Title = "Documentos rejeitados.",
                        Message = "Envie novamente os arquivos corrigidos. " + GeneratedDocsAfterApprovalMessage,
                        RejectionReason = rejectionReason,
                        ShowActionButton = true,
                        ActionButtonLabel = "Reenviar documentos"
                    };
                default:
                    var incompleteMessage = string.IsNullOrWhiteSpace(gapMessage)
                        ? IncompleteSetClientMessage
                        : gapMessage.TrimEnd('.') + ". " + GeneratedDocsAfterApprovalMessage;
                    return new ClientOnboardingClientBanner
                    {
                        SetStatus = ClientOnboardingSetStatus.NeedsUpload,
                        Tone = ClientOnboardingBannerTone.Warning,
                        Visible = true,
                        Title = "Documentos pendentes.",
                        Message = incompleteMessage,
                        ShowActionButton = true,
                        ActionButtonLabel = "Enviar documentos"
                    };
            }
        }

        public static ClientOnboardingSetStatus ResolveSetStatus(
            Contract contract,
            IEnumerable<ClientOnboardingDocument> documents)
        {
            if (contract.DocumentsApproved)
                return ClientOnboardingSetStatus.Approved;

            var list = documents.ToList();
            if (list.Count == 0)
                return ClientOnboardingSetStatus.NeedsUpload;

            var relevant = RelevantLatest(list);
            if (relevant.Any(d => d.Status == ClientOnboardingDocumentStatus.Rejected))
                return ClientOnboardingSetStatus.Rejected;

            if (HasRequiredSet(list) && relevant.All(d => d.Status == ClientOnboardingDocumentStatus.Pending || d.Status == ClientOnboardingDocumentStatus.Approved))
                return ClientOnboardingSetStatus.AwaitingApproval;

            if (!HasRequiredSet(list))
                return ClientOnboardingSetStatus.NeedsUpload;

            return ClientOnboardingSetStatus.AwaitingApproval;
        }

        public static Dictionary<ClientOnboardingDocumentType, ClientOnboardingDocument> LatestByType(
            IEnumerable<ClientOnboardingDocument> documents)
        {
            return documents
                .GroupBy(d => d.Type)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First());
        }

        /// <summary>
        /// Documentos que entram no status do conjunto: ignora RG/CPF se a CNH já cobre a identidade, e vice-versa.
        /// </summary>
        public static IReadOnlyList<ClientOnboardingDocument> RelevantLatest(IEnumerable<ClientOnboardingDocument> documents)
        {
            var latest = LatestByType(documents);
            var hasCnh = IsUsable(latest, ClientOnboardingDocumentType.IdentityCnh);
            var hasRgCpf = HasUsableRgAndCpf(latest);

            IEnumerable<ClientOnboardingDocumentType> types;
            if (hasCnh)
            {
                types = new[]
                {
                    ClientOnboardingDocumentType.IdentityCnh,
                    ClientOnboardingDocumentType.ProofOfResidence
                };
            }
            else if (hasRgCpf)
            {
                types = new[]
                {
                    ClientOnboardingDocumentType.IdentityRgFront,
                    ClientOnboardingDocumentType.IdentityRgBack,
                    ClientOnboardingDocumentType.IdentityCpf,
                    ClientOnboardingDocumentType.ProofOfResidence
                };
            }
            else
            {
                types = latest.Keys.OrderBy(t => t.ToReviewOrder());
            }

            return types
                .Where(latest.ContainsKey)
                .Select(t => latest[t])
                .ToList();
        }

        private static bool HasUsableRgAndCpf(
            IReadOnlyDictionary<ClientOnboardingDocumentType, ClientOnboardingDocument> latest) =>
            IsUsable(latest, ClientOnboardingDocumentType.IdentityRgFront)
            && IsUsable(latest, ClientOnboardingDocumentType.IdentityRgBack)
            && IsUsable(latest, ClientOnboardingDocumentType.IdentityCpf);

        private static bool IsUsable(
            IReadOnlyDictionary<ClientOnboardingDocumentType, ClientOnboardingDocument> latest,
            ClientOnboardingDocumentType type)
        {
            if (!latest.TryGetValue(type, out var doc))
                return false;
            return doc.Status != ClientOnboardingDocumentStatus.Rejected;
        }
    }
}
