namespace KlyonHubPlatform.Domain.Core.Enums
{
    public static class ClientOnboardingDocumentTypeExtensions
    {
        public static string ToDisplayString(this ClientOnboardingDocumentType type) => type switch
        {
            ClientOnboardingDocumentType.IdentityRgFront => "RG (frente)",
            ClientOnboardingDocumentType.IdentityRgBack => "RG (verso)",
            ClientOnboardingDocumentType.IdentityCpf => "CPF",
            ClientOnboardingDocumentType.IdentityCnh => "CNH",
            ClientOnboardingDocumentType.ProofOfResidence => "Comprovante de residência",
            _ => type.ToString()
        };

        public static bool IsIdentity(this ClientOnboardingDocumentType type) =>
            type is ClientOnboardingDocumentType.IdentityRgFront
                or ClientOnboardingDocumentType.IdentityRgBack
                or ClientOnboardingDocumentType.IdentityCpf
                or ClientOnboardingDocumentType.IdentityCnh;

        public static int ToReviewOrder(this ClientOnboardingDocumentType type) => type switch
        {
            ClientOnboardingDocumentType.IdentityRgFront => 0,
            ClientOnboardingDocumentType.IdentityRgBack => 1,
            ClientOnboardingDocumentType.IdentityCpf => 2,
            ClientOnboardingDocumentType.IdentityCnh => 3,
            ClientOnboardingDocumentType.ProofOfResidence => 4,
            _ => 99
        };
    }
}
