namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    /// <summary>Documento institucional da Klyon exibido no onboarding pós-pagamento (slug seguro para URL).</summary>
    public sealed record KlyonOnboardingPublicDocumentItem(string Slug, string Title, string FileName);
}
