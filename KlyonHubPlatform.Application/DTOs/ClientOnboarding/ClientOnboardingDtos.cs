using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.DTOs.ClientOnboarding
{
    public class ClientOnboardingPageResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Contract? Contract { get; set; }
        public List<ClientOnboardingDocument> Documents { get; set; } = new();
        public ClientOnboardingSetStatus SetStatus { get; set; }
        public bool CanUpload { get; set; }
        public bool CanViewGeneratedDocuments { get; set; }
        public string? RejectionReason { get; set; }
        public ClientOnboardingIdentityPath IdentityPath { get; set; }
        public string? IdentityGapMessage { get; set; }
        public ClientOnboardingClientBanner Banner { get; set; } = new();
    }

    public enum ClientOnboardingBannerTone
    {
        Warning,
        Info,
        Danger
    }

    public class ClientOnboardingClientBanner
    {
        public ClientOnboardingSetStatus SetStatus { get; set; }
        public ClientOnboardingBannerTone Tone { get; set; } = ClientOnboardingBannerTone.Warning;
        public bool Visible { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public bool ShowActionButton { get; set; }
        public string ActionButtonLabel { get; set; } = "Enviar documentos";
    }

    public enum ClientOnboardingIdentityPath
    {
        RgAndCpf = 0,
        Cnh = 1
    }

    public enum ClientOnboardingSetStatus
    {
        NeedsUpload,
        AwaitingApproval,
        Rejected,
        Approved
    }

    public class ClientOnboardingUploadInput
    {
        public int ContractId { get; set; }
        public int ClientId { get; set; }
        public ClientOnboardingDocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

    public class ClientOnboardingSetUploadInput
    {
        public int ContractId { get; set; }
        public int ClientId { get; set; }
        public ClientOnboardingIdentityPath Path { get; set; }
        public ClientOnboardingUploadInput? RgFront { get; set; }
        public ClientOnboardingUploadInput? RgBack { get; set; }
        public ClientOnboardingUploadInput? Cpf { get; set; }
        public ClientOnboardingUploadInput? Cnh { get; set; }
        public ClientOnboardingUploadInput? ProofOfResidence { get; set; }
    }

    public class ClientOnboardingCommandResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
    }

    public class ClientOnboardingReviewItem
    {
        public Contract Contract { get; set; } = null!;
        public ClientOnboardingSetStatus SetStatus { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }
        public DateTime? LastUploadAt { get; set; }
    }

    public class ClientOnboardingFileResult
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
    }
}
