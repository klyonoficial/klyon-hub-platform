namespace KlyonHubPlatform.Application
{
    /// <summary>PDFs fixos em wwwroot/documents/klyon-onboarding/. Atualizar <see cref="ValidUntilUtc"/> quando renovar alvarás/IPTU.</summary>
    public static class KlyonOnboardingPublicDocumentCatalog
    {
        public const string WwwrootRelativeFolder = "documents/klyon-onboarding";

        public static IReadOnlyList<KlyonOnboardingPublicDocumentDefinition> Definitions { get; } =
            new KlyonOnboardingPublicDocumentDefinition[]
            {
                new(
                    Slug: "cartao-cnpj-klyon",
                    FileName: "cartao-cnpj-klyon.pdf",
                    Title: "Cartão CNPJ atualizado (Klyon)",
                    ValidUntilUtc: null),
                new(
                    Slug: "iptu-2026",
                    FileName: "iptu-2026.pdf",
                    Title: "IPTU 2026",
                    ValidUntilUtc: new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)),
                new(
                    Slug: "alvara-bombeiros",
                    FileName: "alvara-bombeiros.pdf",
                    Title: "Alvará dos bombeiros",
                    ValidUntilUtc: new DateTime(2026, 8, 11, 23, 59, 59, DateTimeKind.Utc)),
                new(
                    Slug: "alvara-funcionamento",
                    FileName: "alvara-funcionamento.pdf",
                    Title: "Alvará de funcionamento",
                    ValidUntilUtc: new DateTime(2028, 10, 2, 23, 59, 59, DateTimeKind.Utc)),
            };

        public static KlyonOnboardingPublicDocumentDefinition? TryGetBySlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;
            var s = slug.Trim();
            foreach (var d in Definitions)
            {
                if (string.Equals(d.Slug, s, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }
    }

    public sealed record KlyonOnboardingPublicDocumentDefinition(string Slug, string FileName, string Title, DateTime? ValidUntilUtc);
}
